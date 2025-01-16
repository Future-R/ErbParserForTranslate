using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public class ERBParser
{
    // 想了想，反正单纯只是提取文本，不需要Execute，所以只需要逐行提取，不需要保存结构
    // 很后悔，刚接触era的时候不知道这语言是如此强上下文，屎山已经不想动了
    List<string> lineList = new List<string>();

    StringList varNameList = new StringList();
    StringList textList = new StringList();

    List<string> contexts = new List<string>();

    public class StringList
    {
        public List<(string key, string context)> lines = new List<(string, string)>();

        public void Add(string str, List<string> cxt)
        {
            lines.Add((str, string.Join("\n", cxt)));
        }

        public void AddRange(IEnumerable<string> strs, List<string> cxt)
        {
            foreach (var str in strs)
            {
                lines.Add((str, string.Join("\n", cxt)));
            }
        }

        public List<string> GetKeys()
        {
            return lines.Select(x => x.Item1).ToList();
        }
    }

    public void ParseFile(string filePath)
    {
        try
        {
            string content = File.ReadAllText(filePath, Configs.fileEncoding);
            // 一个暴力处理，如果同目录有ERH，则拼接到ERB前面
            if (File.Exists(Path.ChangeExtension(filePath, "ERH")))
            {
                content = File.ReadAllText(Path.ChangeExtension(filePath, "ERH"), Configs.fileEncoding) + "\n" + content;
            }
            // 这里处理了[SKIPSTART]～[SKIPEND]
            lineList = Tools.RemoveSkippedText(content.Replace(Environment.NewLine, "\n")).Split(new[] { "\n" }, StringSplitOptions.None).ToList();
            // 处理花括号合并多行代码
            lineList = mergeLines(lineList);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"读取文件失败：{ex.Message}");
            return;
        }

        for (int i = 0; i < lineList.Count; i++)
        {
            string line = lineList[i];
            // 先trim再转义tab，免得影响StartWith判断
            string lineString = line.Trim().Replace("\t", "<Tab>");
            // 匹配注释，注释可能在行尾
            // 注释的优先级居然比print低
            // Emuera.Net居然在SPLIT里用;
            int cmtIndex = lineString.IndexOf(';');
            switch (cmtIndex)
            {
                // 没有分号，正常通过
                case -1:
                    break;
                // 分号在首位，跳过这次循环
                case 0:
                    continue;
                // 分号不在首位，检查前面是否是PRINT，如果是，正常通过；如果不是，把分号后的内容移除
                default:
                    if (!lineString.StartsWith("PRINT") && !lineString.StartsWith("SPLIT"))
                    {
                        lineString = lineString.Substring(0, cmtIndex).TrimEnd();
                    }
                    break;
            }

            // 写入上下文
            contexts.Clear();
            if (i > 0)
            {
                contexts.Add(lineList[i - 1]);
            }
            contexts.Add(lineList[i]);
            if (i < lineList.Count - 1)
            {
                contexts.Add(lineList[i + 1]);
            }

            // 匹配函数
            // 只匹配了声明用的@
            if (lineString.StartsWith("@"))
            {
                // 函数在定义时要么只出现一对括号，要么没有括号而是用逗号划分
                if (lineString.Contains('(') && lineString.Contains(')'))
                {
                    simpleFuncExpression(lineString.Substring(1));
                }
                else
                {
                    var enumer = lineString.Split(',')
                        .Where(arg => !string.IsNullOrWhiteSpace(arg))
                        .Select(arg => arg.Trim());
                    // enumer.FirstOrDefault()是函数名，也可能需要翻译，但需要去除前面的@
                    // 其它成员是参数名
                    varNameList.Add(enumer.FirstOrDefault().TrimStart('@'), contexts);
                    foreach (var token in enumer.Skip(1))
                    {
                        string[] parts = token.Split('=');
                        varNameList.Add(parts[0].Trim(), contexts);
                        if (parts.Length > 1 && !int.TryParse(parts[1].Trim(), out _))
                        {
                            textList.Add(parts[1].Trim(), contexts);
                        }
                    }
                }
            }
            else if (lineString.StartsWith("#DEFINE"))
            {
                var parts = lineString.Split(new[] { ' ', '\t' })
                    .Where(str => !String.IsNullOrWhiteSpace(str) && !int.TryParse(str, out _));
                varNameList.AddRange(parts.Skip(1), contexts);
            }
            // 还是要把call拆出来，因为call后面很可能有form，还是交给解析比较好
            // call有两种形式，一种是CALL 函数名, 参数1, 参数2，另一种是CALL 函数名(参数1,参数2)
            // 之前的做法只考虑了第一种形式，现在要考虑第二种形式
            else if (lineString.StartsWith("CALL") || lineString.StartsWith("TRYCALL") || lineString.StartsWith("JUMP "))
            {
                int spIndex = Tools.GetSpaceIndex(lineString);
                string rightValue = lineString.Substring(spIndex).Trim();

                // 如果逗号的下标小于第一个左括号，则说明是CALL Func(Pram)形式，否则是CALL Func, Pram形式
                int cmIndex = rightValue.IndexOf(",");
                int lfIndex = rightValue.IndexOf("(");
                if (cmIndex < lfIndex)
                {
                    string functionName = rightValue.Substring(0, lfIndex);
                    rightValue = rightValue.Substring(lfIndex).TrimEnd(')');
                    if (Configs.mergeString)
                    {
                        string[] parts = rightValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var item in parts)
                        {
                            if (item.Contains('"'))
                            {
                                textList.Add(item, contexts);
                            }
                            else
                            {
                                var (vari, text) = ExpressionParser.Slash(item);
                                varNameList.AddRange(vari, contexts);
                                textList.AddRange(text, contexts);
                            }
                        }
                    }
                    else
                    {
                        var (vari, text) = ExpressionParser.Slash(rightValue);
                        varNameList.AddRange(vari, contexts);
                        textList.AddRange(text, contexts);
                    }
                }
                else
                {
                    // 如果要合并参数中的联立字符串
                    if (Configs.mergeString)
                    {
                        string[] parts = rightValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var item in parts)
                        {
                            if (item.Contains('"'))
                            {
                                textList.Add(item, contexts);
                            }
                            else
                            {
                                var (vari, text) = ExpressionParser.Slash(item);
                                varNameList.AddRange(vari, contexts);
                                textList.AddRange(text, contexts);
                            }
                        }
                    }
                    // 通常模式
                    else
                    {
                        var (vari, text) = ExpressionParser.Slash(rightValue);
                        varNameList.AddRange(vari, contexts);
                        textList.AddRange(text, contexts);
                    }
                }
            }
            // 声明变量，此时只会出现至多一个等号，所以以单个等号为判断依据。
            else if (lineString.StartsWith("#DIM"))
            {
                int eqIndex = lineString.IndexOf("=");
                // 无等号的话，只声明不赋值。以空格分隔取到右值
                // 右值开头可能有SAVEDATA、DYNAMIC、CONST、REF
                // 右值可能是Array，也可能是Array, 6，所以要用逗号分隔
                // 第一个元素是变量名，丢给变量名列表；第二个元素是参数，丢给文本列表
                if (eqIndex == -1)
                {
                    int spIndex = Tools.GetSpaceIndex(lineString);
                    // 筛掉可能的SAVEDATA
                    string rightValue = lineString.Substring(spIndex).Trim();
                    if (rightValue.StartsWith("GLOBAL "))
                    {
                        rightValue = rightValue.Substring(7);
                    }
                    else if (rightValue.StartsWith("GLOBALS "))
                    {
                        rightValue = rightValue.Substring(8);
                    }
                    if (rightValue.StartsWith("REF "))
                    {
                        rightValue = rightValue.Substring(4);
                    }
                    if (rightValue.StartsWith("CONST "))
                    {
                        rightValue = rightValue.Substring(6);
                    }
                    else if (rightValue.StartsWith("DYNAMIC "))
                    {
                        rightValue = rightValue.Substring(8);
                    }
                    else if (rightValue.StartsWith("SAVEDATA "))
                    {
                        rightValue = rightValue.Substring(9);
                    }
                    // 有逗号的话，左边扔去变量名，右边扔去文本；没有的话直接丢去变量名
                    int cmIndex = rightValue.IndexOf(',');
                    if (cmIndex != -1 && cmIndex + 1 < rightValue.Length)
                    {
                        string commaLeft = rightValue.Substring(0, cmIndex).Trim();
                        string commaRight = rightValue.Substring(cmIndex + 1).Trim();
                        varNameList.Add(commaLeft, contexts);
                        textList.Add(commaRight, contexts);
                    }
                    else
                    {
                        varNameList.Add(rightValue, contexts);
                    }
                }
                // 有等号的话，还要额外获取右值。右值可能是数字、字符串，可能会用逗号分隔。
                // 字符串型可能会以"'="的方式赋值，不过变量在声明时无法引用，所以不必裁剪左值末尾的单引号
                else
                {
                    string leftValue = lineString.Substring(0, eqIndex).Trim();
                    string rightValue = lineString.Substring(eqIndex + 1).Trim();

                    var leftEnumer = leftValue.Split(' ')
                        .Where(arg => !string.IsNullOrWhiteSpace(arg));
                    varNameList.Add(leftEnumer.LastOrDefault(), contexts);

                    // 类型推导，如果是数值型，那么右值不用翻译，continue掉
                    bool isIntegerValue = leftEnumer.FirstOrDefault() == "#DIM";
                    if (isIntegerValue) continue;
                    // 否则是字符串型，逗号切割，不trim掉双引号是为了方便后续做替换
                    else
                    {
                        var rightEnumer = rightValue.Split(',')
                        .Where(arg => !string.IsNullOrWhiteSpace(arg))
                        .Select(arg => arg.Trim().Trim('\"'));
                        textList.AddRange(rightEnumer, contexts);
                    }
                }
            }
            // 变量批量赋值，VARSET 变量名, 参数ABC
            else if (lineString.StartsWith("VARSET "))
            {
                var rightValue = lineString.Substring(6).Trim();
                var rightEnumer = rightValue.Split(',')
                        .Where(arg => !string.IsNullOrWhiteSpace(arg))
                        .Select(arg => arg.Trim());
                varNameList.AddRange(rightEnumer, contexts);
            }
            // SETVAR	string, any
            else if (lineString.StartsWith("SETVAR"))
            {
                var rightValue = lineString.Substring(6).Trim();
                int cmIndex = rightValue.IndexOf(",");
                if (cmIndex != -1 && cmIndex + 1 < rightValue.Length)
                {
                    string commaLeft = rightValue.Substring(0, cmIndex).Trim();
                    string commaRight = rightValue.Substring(cmIndex + 1).Trim();
                    varNameList.Add(commaLeft.TrimStart('@').Trim(), contexts);
                    // 参数2扔去解析
                    var (vari, text) = ExpressionParser.Slash(commaRight);
                    varNameList.AddRange(vari, contexts);
                    textList.AddRange(text, contexts);
                }
            }
            // .NET版新增了SQL语句
            else if (lineString.StartsWith("SQL_EXECUTE"))
            {
                int spIndex = Tools.GetSpaceIndex(lineString);
                string rightValue = lineString.Substring(spIndex).TrimStart().Replace("\r\n", "\n").Replace("\r", "\n");
                // SQL语句我就不解析了，直接全部拿出来翻译可能还更快
                textList.Add(rightValue, contexts);
            }
            // FOR循环 int,int,int，逗号分隔后全部送去变量名
            else if (lineString.StartsWith("FOR "))
            {
                var rightValue = lineString.Substring(4).Trim();
                var enumer = rightValue.Split(',')
                        .Where(arg => !string.IsNullOrWhiteSpace(arg))
                        .Select(arg => arg.Trim());
                varNameList.AddRange(enumer, contexts);
            }
            // Switch-Case的Switch，右值拿去判别式解析
            else if (lineString.StartsWith("SELECTCASE "))
            {
                var rightValue = lineString.Substring(10).Trim();
                var (vari, text) = ExpressionParser.Slash(rightValue);
                varNameList.AddRange(vari, contexts);
                textList.AddRange(text, contexts);
            }
            // 匹配CASE
            else if (lineString.StartsWith("CASE "))
            {
                int spIndex = Tools.GetSpaceIndex(lineString);
                string rightValue = lineString.Substring(spIndex).Trim();
                if (rightValue.StartsWith("\""))
                {
                    textList.Add(rightValue, contexts);
                }
                else
                {
                    var (vari, text) = ExpressionParser.Slash(rightValue);
                    varNameList.AddRange(vari, contexts);
                    textList.AddRange(text, contexts);
                }
            }
            // 匹配判别式……解析右值
            else if (lineString.StartsWith("IF ") || lineString.StartsWith("SIF ") || lineString.StartsWith("ELSEIF "))
            {
                int spIndex = Tools.GetSpaceIndex(lineString);
                string rightValue = lineString.Substring(spIndex).Trim();
                var (vari, text) = ExpressionParser.Slash(rightValue);
                varNameList.AddRange(vari, contexts);
                textList.AddRange(text, contexts);
            }
            // 匹配返回，RETURN的右值一定是变量名，RETURNFORM和RETURNF将返回一个FORM解析的右值，不管了，统统扔去解析
            else if (lineString.StartsWith("RETURN"))
            {
                int spIndex = Tools.GetSpaceIndex(lineString);
                if (spIndex != -1)
                {
                    string rightValue = lineString.Substring(spIndex).Trim();
                    var (vari, text) = ExpressionParser.Slash(rightValue);
                    varNameList.AddRange(vari, contexts);
                    textList.AddRange(text, contexts);
                }
            }
            // 匹配打印按钮，可能是PRINTBUTTON、PRINTBUTTONC、PRINTBUTTONLC
            // 空格取右值，右值以逗号分隔，参数1一定是字符串，参数2可能是纯数
            else if (lineString.StartsWith("PRINTBUTTON"))
            {
                int spIndex = Tools.GetSpaceIndex(lineString);
                string rightValue = lineString.Substring(spIndex).Trim();
                int cmIndex = rightValue.IndexOf(",");
                if (cmIndex != -1 && cmIndex + 1 < rightValue.Length)
                {
                    string commaLeft = rightValue.Substring(0, cmIndex).Trim();
                    string commaRight = rightValue.Substring(cmIndex + 1).Trim();
                    textList.Add(commaLeft, contexts);
                    // 参数2如果是纯数就不翻译了
                    if (!int.TryParse(commaRight, out _))
                    {
                        textList.Add(commaRight, contexts);
                    }
                }
            }
            else if (lineString.StartsWith("BAR ") || lineString.StartsWith("BARL "))
            {
                int spIndex = Tools.GetSpaceIndex(lineString);
                string rightValue = lineString.Substring(spIndex).TrimStart();
                var enumer = rightValue.Split(',')
                        .Where(arg => !string.IsNullOrWhiteSpace(arg))
                        .Select(arg => arg.Trim());
                if (enumer.Count() == 3)
                {
                    varNameList.AddRange(enumer, contexts);
                }
                else
                {
                    Console.WriteLine($"【警告】{rightValue}不是可解析的BAR");
                }
            }
            else if (lineString.StartsWith("GETNUM "))
            {
                int spIndex = Tools.GetSpaceIndex(lineString);
                string rightValue = lineString.Substring(spIndex).TrimStart();
                string[] param = rightValue.Split(',');
                if (param.Length == 2)
                {
                    varNameList.Add(param[0].Trim(), contexts);
                    textList.Add(param[1].Trim(), contexts);
                }
                else
                {
                    Console.WriteLine($"【警告】{rightValue}不是可解析的GETNUM");
                }
            }
            // 打印变量，右值一定是变量，但是'(,5,')这种怎么处理呢，如果是"'("开头，跳过
            else if (lineString.StartsWith("PRINTV") || lineString.StartsWith("PRINTS"))
            {
                int spIndex = Tools.GetSpaceIndex(lineString);
                string rightValue = lineString.Substring(spIndex).TrimStart();
                var (vari, text) = ExpressionParser.Slash(rightValue);
                varNameList.AddRange(vari, contexts);
                textList.AddRange(text, contexts);
            }
            // HTML_PRINT，右值是FORM表达式，如果有英文引号，判断为文本，否则就是变量
            else if (lineString.StartsWith("HTML_PRINT "))
            {
                int spIndex = Tools.GetSpaceIndex(lineString);
                string rightValue = lineString.Substring(spIndex).TrimStart();
                if (rightValue.Contains('"'))
                {
                    textList.Add(rightValue, contexts);
                }
                else
                {
                    varNameList.Add(rightValue, contexts);
                }
            }
            // PRINT图像、矩形和空格，不需要翻译
            else if (lineString.StartsWith("PRINT_IMG") || lineString.StartsWith("PRINT_RECT") || lineString.StartsWith("PRINT_SPACE"))
            {
                continue;
            }
            // 其它PRINT系，右值丢给译者
            else if (lineString.StartsWith("PRINT") || lineString.StartsWith("DATAFORM") || lineString.StartsWith("REUSELASTLINE"))
            {
                int spIndex = Tools.GetSpaceIndex(lineString);
                if (spIndex == -1)
                {
                    continue;
                }
                string rightValue = lineString.Substring(spIndex).TrimStart();
                textList.Add(rightValue, contexts);
            }
            // 改变颜色，右值一般是变量或者R,G,B，直接扔去做表达式解析
            else if (lineString.StartsWith("SETCOLOR "))
            {
                int spIndex = Tools.GetSpaceIndex(lineString);
                string rightValue = lineString.Substring(spIndex).TrimStart();
                var (vari, text) = ExpressionParser.Slash(rightValue);
                varNameList.AddRange(vari, contexts);
                textList.AddRange(text, contexts);
            }
            // SPLIT "日/月/火/水/木/金/土", "/", LOCALS
            // 虽然这里可以把前面的字符串拆开，但最好还是给译者保留一点自由
            else if (lineString.StartsWith("SPLIT "))
            {
                int spIndex = Tools.GetSpaceIndex(lineString);
                string rightValue = lineString.Substring(spIndex).TrimStart();
                int cmIndex = rightValue.LastIndexOf(",");

                textList.Add(rightValue.Substring(0, cmIndex).TrimStart(), contexts);
                varNameList.Add(rightValue.Substring(cmIndex + 1).TrimStart(), contexts);
            }
            // 乘算 TIMES int, float，参数1提出来，把参数2整个弃掉
            else if (lineString.StartsWith("TIMES "))
            {
                int spIndex = Tools.GetSpaceIndex(lineString);
                string rightValue = lineString.Substring(spIndex).TrimStart();
                int cmIndex = rightValue.IndexOf(",");
                string varName = rightValue.Substring(0, cmIndex).Trim();
                varNameList.Add(varName, contexts);
            }
            else if (lineString.StartsWith("PUTFORM "))
            {
                int spIndex = Tools.GetSpaceIndex(lineString);
                string rightValue = lineString.Substring(spIndex).TrimStart();
                textList.Add(rightValue, contexts);
            }
            // 末尾匹配
            // 变量自增自减少
            else if (lineString.EndsWith("++") || lineString.EndsWith("--"))
            {
                string varName = lineString.Substring(0, lineString.Length - 2).Trim();
                varNameList.Add(varName, contexts);
            }
            // 包含型匹配
            else
            {
                // 先匹配正则
                // 捕获results右值，一定是字符串
                Match match = Tools.resultsCatch.Match(lineString);
                if (match.Success)
                {
                    textList.Add(match.Value, contexts);
                    continue;
                }

                // 匹配字符串'=赋值，左值一定是字符串变量，右值一定是字符串。左值拿去判别式解析试试
                if (lineString.Contains(" '= "))
                {
                    int eqIndex = lineString.IndexOf("=");
                    string leftValue = lineString.Substring(0, eqIndex - 1).Trim();
                    string rightValue = lineString.Substring(eqIndex + 1).Trim();
                    var (vari, text) = ExpressionParser.Slash(leftValue);
                    varNameList.AddRange(vari, contexts);
                    textList.AddRange(text, contexts);
                    if (!int.TryParse(rightValue, out _)) textList.Add(rightValue, contexts);
                    continue;
                }
                // 匹配+=和-=赋值，整行拿去做判别式解析试试
                // 如果右值有引号，说明有字符串联立
                if (lineString.Contains("+=") || lineString.Contains("-=") || lineString.Contains("*=") || lineString.Contains("/=") || lineString.Contains("&=") || lineString.Contains("|="))
                {
                    if (Configs.mergeString)
                    {
                        var (left, right) = Tools.GetSlashStringCouple(lineString, '=');
                        if (right.Contains('"'))
                        {
                            varNameList.Add(left.Remove(left.Length - 1).Trim(), contexts);
                            textList.Add(right, contexts);
                            continue;
                        }
                    }

                    var (vari, text) = ExpressionParser.Slash(lineString);
                    varNameList.AddRange(vari, contexts);
                    textList.AddRange(text, contexts);
                    continue;
                }
                // 匹配赋值，左值一定是变量，直接添加；
                // 右值如果没有"{:%这4个符号，则应该是字符串，否则拿去解析；
                if (lineString.Contains("=") && !lineString.Contains("=="))
                {
                    int eqIndex = lineString.IndexOf("=");
                    string leftValue = lineString.Substring(0, eqIndex).Trim();
                    string rightValue = lineString.Substring(eqIndex + 1).Trim();

                    var (vari1, text1) = ExpressionParser.Slash(leftValue);
                    varNameList.AddRange(vari1, contexts);
                    textList.AddRange(text1, contexts);

                    // 如果等号右边有[]，判定为按钮，将按钮的内容拿去解析
                    int leftBracket = rightValue.IndexOf('[');
                    int rightBracket = rightValue.IndexOf(']');
                    bool rightHasBotton = leftBracket != -1 && leftBracket < rightBracket;
                    // 还是算了，不解析了
                    //if (rightHasBotton)
                    //{
                    //    var (vari2, text2) = ExpressionParser.Slash(rightValue.Substring(leftBracket + 1, rightBracket - leftBracket - 1));
                    //    varNameList.AddRange(vari2, contexts);
                    //    textList.AddRange(text2, contexts);
                    //}

                    // 名前是一个临时处理，暂时没想好配置在什么地方
                    bool rightIsString = leftValue.Contains("RESULTS") || leftValue.Contains("LOCALS") || leftValue.Contains("ARGS") || leftValue.Contains("STR:") || leftValue.Contains("名前") || rightHasBotton;
                    bool rightIsNumber = leftValue.Contains("LOCAL") || leftValue.Contains("RESULT") || leftValue.Contains("ARG") ||
                        leftValue.Contains("FLAG:") || leftValue.Contains("BASE:") || leftValue.Contains("ABL:") || leftValue.Contains("EXP:") ||
                        leftValue.Contains("GLOBAL:") || leftValue.Contains("ITEM:") || leftValue.Contains("JUEL:") || leftValue.Contains("MARK:") ||
                        leftValue.Contains("EX:") || leftValue.Contains("PALAM:") || leftValue.Contains("SOURCE:") || leftValue.Contains("STAIN:") ||
                        leftValue.Contains("TALENT:") || leftValue.Contains("EQUIP:") || leftValue.Contains("TRAIN:")
                        ;

                    if (!rightIsString &&
                        (
                            rightIsNumber ||
                            rightValue.StartsWith("@") ||
                            rightValue.Contains("\"") || rightValue.Contains("{") || rightValue.Contains(":") || rightValue.Contains("%") ||
                            rightValue.Contains("+") || rightValue.Contains("-") || rightValue.Contains("*") || rightValue.Contains("/")
                        ))
                    {
                        var (vari2, text2) = ExpressionParser.Slash(rightValue);
                        varNameList.AddRange(vari2, contexts);
                        textList.AddRange(text2, contexts);
                    }
                    else
                    {
                        textList.Add(rightValue, contexts);
                    }

                    continue;
                }
            }
        }
    }

    // 合并重复成员，过滤变量和纯数字
    // 括号的特殊处理：没想好怎么处理，先按有括号就不拆的做法
    public List<(string, string)> VarNameListFilter(List<(string, string)> originalList)
    {
        // 过滤掉空字符串和数组
        var filteredList = originalList
            .Where(token => !string.IsNullOrWhiteSpace(token.Item1) && !Tools.IsArray(token.Item1))
            .ToList();

        // 合并相同词条
        var tupleList = Configs.mergeSameText
            ? filteredList.GroupBy(t => t.Item1).Select(g => g.First()).ToList()
            : filteredList;

        return tupleList;
    }

    [Obsolete]
    public List<string> VarNameListFilter(List<string> originalList)
    {
        return originalList.Distinct()
            .Where(token => !string.IsNullOrWhiteSpace(token) && !Tools.IsArray(token))
            .ToList();
    }

    // 合并重复成员，过滤纯英文和纯数字
    // 是否需要剔除当前文件的变量名，待观察
    public List<(string, string)> TextListFilter(List<(string, string)> originalList)
    {
        // 过滤掉空字符串和数组
        var filteredList = originalList
            .Where(token => !string.IsNullOrWhiteSpace(token.Item1) && !Tools.IsArray(token.Item1))
            .ToList();

        // 合并相同词条
        var tupleList = Configs.mergeSameText
            ? filteredList.GroupBy(t => t.Item1).Select(g => g.First()).ToList()
            : filteredList;

        return tupleList;
    }

    [Obsolete]
    public List<string> TextListFilter(List<string> originalList)
    {
        return originalList
            .Distinct()
            .Where(token => !string.IsNullOrWhiteSpace(token) && !Tools.IsArray(token))
            .ToList();
    }

    /// <summary>
    /// 字符串可转化为int且大于等于0
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    bool IsNaturalNumber(string str)
    {
        return int.TryParse(str, out int n) && n >= 0;
    }
    /// <summary>
    /// 在本程序的目录下输出Json
    /// </summary>
    /// <param name="targetFile">完整路径</param>
    /// <param name="relativePath">相对路径</param>
    public void WriteJson(string targetFile, string relativePath)
    {
        var allObjs = new List<JObject>();

        AddObjsForType("变量", varNameList, allObjs, relativePath);
        AddObjsForType("文本", textList, allObjs, relativePath);

        if (allObjs.Count > 0)
        {
            string jsonContent = JsonConvert.SerializeObject(allObjs, Formatting.Indented);
            try
            {
                File.WriteAllText(Path.ChangeExtension(targetFile, ".json"), jsonContent);
            }
            catch (Exception)
            {
                Console.WriteLine("生成文件失败，可能是硬盘空间不足，请至少准备2G的硬盘空间");
                Console.ReadKey();
            }

        }
    }

    public void WriteJson(string targetFile, string relativePath, (StringList, StringList) referTuple)
    {
        // 解包元组，得到参考用的两个列表
        StringList referVarNameList = referTuple.Item1;
        StringList referTextList = referTuple.Item2;

        var allObjs = new List<JObject>();

        AddObjsForType("变量", varNameList, allObjs, relativePath, referVarNameList);
        AddObjsForType("文本", textList, allObjs, relativePath, referTextList);

        if (allObjs.Count > 0)
        {
            string jsonContent = JsonConvert.SerializeObject(allObjs, Formatting.Indented);
            File.WriteAllText(Path.ChangeExtension(targetFile, ".json"), jsonContent);
        }
    }

    // 键值是 类型 + 相对路径(去除后缀) + 四位数字ID
    // 类型有 变量 和 文本，将来替换的时候，如果变量没有翻译，会从其他已翻译的变量里找翻译
    // 将来还能做检查，如果有变量翻译的不一样，提前报warning
    private void AddObjsForType(string type, StringList originalList, List<JObject> objs, string relativePath)
    {
        if (type == "文本")
        {
            var newObjs = TextListFilter(originalList.lines).Select((item, index) =>
            {
                string original = item.Item1;
                int stage = 0;
                if (Configs.hideEngText && Tools.englishTextCatch.IsMatch(original))
                {
                    stage = -1;
                }
                return new JObject
                {
                    ["key"] = new StringBuilder(type)
                        .Append(Path.ChangeExtension(relativePath, ""))
                        .Append(index.ToString().PadLeft(5, '0'))
                        .ToString(),
                    ["original"] = original,
                    ["translation"] = "",
                    ["stage"] = stage,
                    ["context"] = item.Item2,
                };
            });
            objs.AddRange(newObjs);
        }
        // 追加屏蔽变量输出的功能
        else if (!Configs.hideVarOutput)
        {
            var newObjs = VarNameListFilter(originalList.lines).Select((item, index) =>
            {
                string original = item.Item1;
                int stage = 0;
                if (Configs.hideEngText && Tools.englishTextCatch.IsMatch(original))
                {
                    stage = -1;
                }
                return new JObject
                {
                    ["key"] = new StringBuilder(type)
                        .Append(Path.ChangeExtension(relativePath, ""))
                        .Append(index.ToString().PadLeft(5, '0'))
                        .ToString(),
                    ["original"] = item.Item1,
                    ["translation"] = "",
                    ["stage"] = stage,
                    ["context"] = item.Item2
                };
            });
            objs.AddRange(newObjs);
        }
    }

    private void AddObjsForType(string type, StringList originalList, List<JObject> objs, string relativePath, StringList referenceList)
    {
        // 条数相等不一定匹配，但不相等肯定不匹配
        if (originalList.lines.Count == referenceList.lines.Count)
        {
            var originObjs = type == "文本" ? TextListFilter(originalList.lines) : VarNameListFilter(originalList.lines);
            var referenceObjs = type == "文本" ? TextListFilter(referenceList.lines) : VarNameListFilter(referenceList.lines);
            // 二次检查
            if (originObjs.Count == referenceObjs.Count)
            {
                int index = 0;
                foreach (var (origin, reference) in originObjs.Zip(referenceObjs, (o, r) => (o, r)))
                {
                    // 如果对比发现，参考和原版不一致，那么把参考的值填进translation，并且设置stage为已翻译
                    bool isDiff = origin.Item1 != reference.Item1;
                    // 键值是 类型 + 相对路径(去除后缀) + 5位数字ID
                    string key = $"{type}{Path.ChangeExtension(relativePath, "")}{index++:D5}";
                    string original = origin.Item1;
                    string translation = isDiff ? reference.Item1 : "";
                    int stage = isDiff ? 1 : 0;
                    string context = origin.Item2;

                    // 过滤纯英文词条
                    if (Configs.hideEngText && Tools.englishTextCatch.IsMatch(original))
                    {
                        stage = -1;
                    }

                    objs.Add(new JObject
                    {
                        ["key"] = key,
                        ["original"] = original,
                        ["translation"] = translation,
                        ["stage"] = stage,
                        ["context"] = context
                    });
                }
                return;
            }
        }
        Console.WriteLine($"【警告】：跳过了填充{relativePath}{type}。");
        // 改用常规模式
        AddObjsForType(type, originalList, objs, relativePath);
    }
    /// <summary>
    /// 返回varNameList和textList
    /// </summary>
    /// <returns></returns>
    public (StringList name, StringList text) GetListTuple()
    {
        return (name: varNameList, text: textList);
    }

    public void DebugPrint()
    {
        Console.WriteLine("======变量名======");
        foreach (var item in VarNameListFilter(varNameList.lines))
        {
            Console.WriteLine($"{item.Item1}");
            Console.WriteLine();
        }
        Console.WriteLine("======纯文本======");
        foreach (var item in TextListFilter(textList.lines))
        {
            Console.WriteLine($"{item.Item1}");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// 合并花括号括起的行
    /// </summary>
    /// <returns></returns>
    List<string> mergeLines(List<string> inputList)
    {
        // 用于存储处理后的结果
        List<string> resultList = new List<string>();
        // 用于标记是否遇到开始大括号
        bool insideBraces = false;
        // 用于存储大括号内的字符串
        StringBuilder bracedContent = new StringBuilder();

        foreach (var item in inputList)
        {
            if (item == "{")
            {
                // 遇到开始大括号，标记为在大括号内
                insideBraces = true;
                // 清空之前的大括号内容
                bracedContent.Clear();
            }
            else if (item == "}")
            {
                // 遇到结束大括号，标记为不在大括号内
                insideBraces = false;
                // 将大括号内的内容添加到结果列表
                resultList.Add(bracedContent.ToString());
            }
            else if (insideBraces)
            {
                // 如果在大括号内，将当前项添加到大括号内容
                bracedContent.AppendLine(item);
            }
            else
            {
                // 如果不在大括号内，直接添加到结果列表
                resultList.Add(item);
            }
        }

        return resultList;
    }

    /// <summary>
    /// 仅用于定义function(arg,arg = "")形式的解析
    /// </summary>
    /// <param name="function"></param>
    public void simpleFuncExpression(string function)
    {
        Match match = Tools.dimFunction.Match(function);

        if (match.Success)
        {
            string funcName = match.Groups[1].Value;
            if (!Tools.engArrayFilter.IsMatch(funcName))
            {
                //Console.WriteLine("函数名: " + funcName);
                varNameList.Add(funcName, contexts);
            }

            string args = match.Groups[2].Value;
            string[] argArray = args.Split(',');

            for (int i = 0; i < argArray.Length; i++)
            {
                string arg = argArray[i].Trim();
                string[] argParts = arg.Split('=');

                string argName = argParts[0].Trim();
                if (!Tools.engArrayFilter.IsMatch(argName))
                {
                    //Console.WriteLine("参数名: " + argName);
                    varNameList.Add(argName, contexts);
                }


                if (argParts.Length > 1)
                {
                    string argV = argParts[1].Trim();
                    if (!Tools.engArrayFilter.IsMatch(argV))
                    {
                        if (argV[0] == '"')
                        {
                            if (argV.Length > 2)
                            {
                                //Console.WriteLine("参数值: " + argParts[1].Trim());
                                textList.Add(argV, contexts);
                            }
                        }
                        else
                        {
                            //Console.WriteLine("参数名: " + argParts[1].Trim());
                            varNameList.Add(argV, contexts);
                        }
                    }
                }
            }
        }
    }

}