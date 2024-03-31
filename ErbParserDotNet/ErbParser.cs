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
    List<string> lineList = new List<string>();
    List<string> varNameList = new List<string>();
    List<string> textList = new List<string>();

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
            lineList = content.Replace(Environment.NewLine, "\n").Replace("\t", " ").Split(new[] { "\n" }, StringSplitOptions.None).ToList();
            // 处理花括号合并多行代码
            lineList = mergeLines(lineList);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"读取文件失败：{ex.Message}");
            return;
        }

        foreach (string line in lineList)
        {
            string lineString = line.TrimStart();
            // 匹配注释，暂不考虑处理注释添加到行尾的傻逼情况
            // erb注释的优先级居然比print低，也就是说我捕获到';'也不能简单以';'分隔，还要判断前面有没有输出指令
            // 后续一切处理都是以注释已被筛掉为前提
            if (lineString.StartsWith(";")) continue;
            // 匹配函数
            // 只匹配了声明用的@
            else if (lineString.StartsWith("@"))
            {
                int start = lineString.IndexOf("(");
                int end = start != -1 ? lineString.IndexOf(")", start) : -1;
                // 函数在定义时要么只出现一对括号，要么没有括号而是用逗号划分
                // 左括号索引小于右括号索引，且左括号索引不为-1，说明匹配到正确的括号了
                if (start != -1 && start < end)
                {
                    string args = lineString.Substring(start + 1, end - start - 1);
                    var tokens = args.Split(',')
                        .Where(arg => !string.IsNullOrWhiteSpace(arg))
                        .Select(arg => arg.Trim());
                    // 参数Token里可能不止参数名，还可能有 参数名 = "初始值"，此时判断右值是否为字符串，是字符串的话扔到text里
                    foreach (var token in tokens)
                    {
                        string[] parts = token.Split('=');
                        varNameList.Add(parts[0].Trim());
                        if (parts.Length > 1 && !int.TryParse(parts[1].Trim(), out _))
                        {
                            textList.Add(parts[1].Trim());
                        }
                    }
                }
                else
                {
                    var enumer = lineString.Split(',')
                        .Where(arg => !string.IsNullOrWhiteSpace(arg))
                        .Select(arg => arg.Trim());
                    // enumer.FirstOrDefault()是函数名，不需要翻译，所以SKIP(1)
                    // 其它成员是参数名
                    foreach (var token in enumer.Skip(1))
                    {
                        string[] parts = token.Split('=');
                        varNameList.Add(parts[0].Trim());
                        if (parts.Length > 1 && !int.TryParse(parts[1].Trim(), out _))
                        {
                            textList.Add(parts[1].Trim());
                        }
                    }
                }
            }
            // 还是要把call拆出来，因为call后面很可能有form，还是交给解析比较好
            else if (lineString.StartsWith("CALL") || lineString.StartsWith("TRYCALL"))
            {
                int spIndex = lineString.IndexOf(" ");
                string rightValue = lineString.Substring(spIndex).Trim();
                var (vari, text) = ExpressionParser.Slash(rightValue);
                varNameList.AddRange(vari);
                textList.AddRange(text);
            }
            // 声明变量，此时只会出现至多一个等号，所以以单个等号为判断依据。
            else if (lineString.StartsWith("#DIM"))
            {
                int eqIndex = lineString.IndexOf("=");
                // 无等号的话，只声明不赋值。以空格分隔，最后一个元素是变量名。
                if (eqIndex == -1)
                {
                    var enumer = lineString.Split(' ')
                        .Where(arg => !string.IsNullOrWhiteSpace(arg));
                    // 大意了，怎么还有"#DIM Array,6"这种写法
                    var name = enumer.LastOrDefault();
                    int index = name.IndexOf(",");
                    if (index > -1)
                    {
                        name = name.Substring(0, index);
                    }
                    varNameList.Add(name);
                }
                // 有等号的话，还要额外获取右值。右值可能是数字、字符串，可能会用逗号分隔。
                // 字符串型可能会以"'="的方式赋值，不过变量在声明时无法引用，所以不必裁剪左值末尾的单引号
                else
                {
                    string leftValue = lineString.Substring(0, eqIndex).Trim();
                    string rightValue = lineString.Substring(eqIndex + 1).Trim();

                    var leftEnumer = leftValue.Split(' ')
                        .Where(arg => !string.IsNullOrWhiteSpace(arg));
                    varNameList.Add(leftEnumer.LastOrDefault());

                    // 类型推导，如果是数值型，那么右值不用翻译，continue掉
                    bool isIntegerValue = leftEnumer.FirstOrDefault() == "#DIM";
                    if (isIntegerValue) continue;
                    // 否则是字符串型，逗号切割，不trim掉双引号是为了方便后续做替换
                    else
                    {
                        var rightEnumer = rightValue.Split(',')
                        .Where(arg => !string.IsNullOrWhiteSpace(arg))
                        .Select(arg => arg.Trim().Trim('\"'));
                        textList.AddRange(rightEnumer);
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
                varNameList.Add(rightEnumer.FirstOrDefault());
            }
            // Switch-Case的Switch，右值拿去判别式解析
            else if (lineString.StartsWith("SELECTCASE "))
            {
                var rightValue = lineString.Substring(10).Trim();
                var (vari, text) = ExpressionParser.Slash(rightValue);
                varNameList.AddRange(vari);
                textList.AddRange(text);
            }
            // 匹配判别式……解析右值
            else if (lineString.StartsWith("IF ") || lineString.StartsWith("SIF ") || lineString.StartsWith("ELSEIF ") || lineString.StartsWith("CASE "))
            {
                int spIndex = lineString.IndexOf(" ");
                string rightValue = lineString.Substring(spIndex).Trim();
                var (vari, text) = ExpressionParser.Slash(rightValue);
                varNameList.AddRange(vari);
                textList.AddRange(text);
            }
            // 匹配返回，RETURN的右值一定是变量名，RETURNFORM和RETURNF将返回一个FORM解析的右值，不管了，统统扔去解析
            else if (lineString.StartsWith("RETURN"))
            {
                int spIndex = lineString.IndexOf(" ");
                if (spIndex != -1)
                {
                    string rightValue = lineString.Substring(spIndex).Trim();
                    var (vari, text) = ExpressionParser.Slash(rightValue);
                    varNameList.AddRange(vari);
                    textList.AddRange(text);
                }
            }
            // 匹配打印按钮，可能是PRINTBUTTON、PRINTBUTTONC、PRINTBUTTONLC
            // 空格取右值，右值以逗号分隔，参数1一定是字符串，参数2可能是纯数
            else if (lineString.StartsWith("PRINTBUTTON"))
            {
                int spIndex = lineString.IndexOf(" ");
                string rightValue = lineString.Substring(spIndex).Trim();
                int cmIndex = rightValue.IndexOf(",");
                if (cmIndex != -1 && cmIndex + 1 < rightValue.Length)
                {
                    string commaLeft = rightValue.Substring(0, cmIndex).Trim();
                    string commaRight = rightValue.Substring(cmIndex + 1).Trim();
                    textList.Add(commaLeft);
                    // 参数2如果是纯数就不翻译了
                    if (!int.TryParse(commaRight, out _))
                    {
                        textList.Add(commaRight);
                    }
                }
            }
            // 打印变量，右值一定是变量，但是'(,5,')这种怎么处理呢，不管了，拿去表达式解析
            else if (lineString.StartsWith("PRINTV") || lineString.StartsWith("PRINTS"))
            {
                int spIndex = lineString.IndexOf(" ");
                string rightValue = lineString.Substring(spIndex).TrimStart();
                var (vari, text) = ExpressionParser.Slash(rightValue);
                varNameList.AddRange(vari);
                textList.AddRange(text);
            }
            // HTML_PRINT，右值是FORM表达式，如果有英文引号，判断为文本，否则就是变量
            else if (lineString.StartsWith("HTML_PRINT "))
            {
                int spIndex = lineString.IndexOf(" ");
                string rightValue = lineString.Substring(spIndex).TrimStart();
                if (rightValue.Contains('"'))
                {
                    textList.Add(rightValue);
                }
                else
                {
                    varNameList.Add(rightValue);
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
                int spIndex = lineString.IndexOf(" ");
                if (spIndex == -1)
                {
                    continue;
                }
                string rightValue = lineString.Substring(spIndex).TrimStart();
                textList.Add(rightValue);
            }
            // 改变颜色，右值一般是变量或者R,G,B，直接扔去做表达式解析
            else if (lineString.StartsWith("SETCOLOR "))
            {
                int spIndex = lineString.IndexOf(" ");
                string rightValue = lineString.Substring(spIndex).TrimStart();
                var (vari, text) = ExpressionParser.Slash(rightValue);
                varNameList.AddRange(vari);
                textList.AddRange(text);
            }
            // 乘算 TIMES int, float，直接把右值拿去解析吧，也别管参数类型了，麻烦
            else if (lineString.StartsWith("TIMES "))
            {
                int spIndex = lineString.IndexOf(" ");
                string rightValue = lineString.Substring(spIndex).TrimStart();
                var (vari, text) = ExpressionParser.Slash(rightValue);
                varNameList.AddRange(vari);
                textList.AddRange(text);
            }
            // 末尾匹配
            // 变量自增自减少
            else if (lineString.EndsWith("++") || lineString.EndsWith("--"))
            {
                string varName = lineString.Substring(0, lineString.Length - 2).Trim();
                varNameList.Add(varName);
            }
            // 包含型匹配
            else
            {
                // 匹配赋值，左值一定是变量，整行拿去判别式解析试试
                if (lineString.Contains(" = "))
                {
                    var (vari, text) = ExpressionParser.Slash(lineString);
                    varNameList.AddRange(vari);
                    textList.AddRange(text);
                }
                // 匹配字符串'=赋值，左值一定是字符串变量，右值一定是字符串。左值拿去判别式解析试试
                if (lineString.Contains(" '= "))
                {
                    int eqIndex = lineString.IndexOf("=");
                    string leftValue = lineString.Substring(0, eqIndex - 1).Trim();
                    string rightValue = lineString.Substring(eqIndex + 1).Trim();
                    var (vari, text) = ExpressionParser.Slash(leftValue);
                    varNameList.AddRange(vari);
                    textList.AddRange(text);
                    if (!int.TryParse(rightValue, out _)) textList.Add(rightValue);
                }
                // 匹配+=和-=赋值，整行拿去做判别式解析试试
                if (lineString.Contains("+=") || lineString.Contains("-="))
                {
                    var (vari, text) = ExpressionParser.Slash(lineString);
                    varNameList.AddRange(vari);
                    textList.AddRange(text);
                }
            }
        }
    }

    // 合并重复成员，过滤变量和纯数字
    // 括号的特殊处理：没想好怎么处理，先按有括号就不拆的做法
    public List<string> VarNameListFilter(List<string> originalList)
    {
        return originalList.Distinct()
            .Where(token => !string.IsNullOrWhiteSpace(token) && !Tools.IsArray(token) && !IsNaturalNumber(token))
            .ToList();
    }
    // 合并重复成员，过滤纯英文和纯数字
    // 是否需要剔除当前文件的变量名，待观察
    public List<string> TextListFilter(List<string> originalList)
    {
        return originalList
            .Distinct()
            .Where(token => !string.IsNullOrWhiteSpace(token) && !Tools.IsArray(token) && !IsNaturalNumber(token))
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
            File.WriteAllText(Path.ChangeExtension(targetFile, ".json"), jsonContent);
        }
    }

    public void WriteJson(string targetFile, string relativePath, (List<string>, List<string>) referTuple)
    {
        // 解包元组，得到参考用的两个列表
        List<string> referVarNameList = referTuple.Item1;
        List<string> referTextList = referTuple.Item2;

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
    private void AddObjsForType(string type, List<string> originalList, List<JObject> objs, string relativePath)
    {
        if (type == "文本")
        {
            var newObjs = TextListFilter(originalList).Select((item, index) =>
            {
                return new JObject
                {
                    ["key"] = new StringBuilder(type)
                        .Append(Path.ChangeExtension(relativePath, ""))
                        .Append(index.ToString().PadLeft(5, '0'))
                        .ToString(),
                    ["original"] = item,
                    ["translation"] = ""
                };
            });
            objs.AddRange(newObjs);
        }
        else
        {
            var newObjs = VarNameListFilter(originalList).Select((item, index) =>
            {
                return new JObject
                {
                    ["key"] = new StringBuilder(type)
                        .Append(Path.ChangeExtension(relativePath, ""))
                        .Append(index.ToString().PadLeft(5, '0'))
                        .ToString(),
                    ["original"] = item,
                    ["translation"] = ""
                };
            });
            objs.AddRange(newObjs);
        }
    }

    private void AddObjsForType(string type, List<string> originalList, List<JObject> objs, string relativePath, List<string> referenceList)
    {
        // 条数相等不一定匹配，但不相等肯定不匹配
        if (originalList.Count == referenceList.Count)
        {
            var originObjs = type == "文本" ? TextListFilter(originalList) : VarNameListFilter(originalList);
            var referenceObjs = type == "文本" ? TextListFilter(referenceList) : VarNameListFilter(referenceList);
            // 二次检查
            if (originObjs.Count == referenceObjs.Count)
            {
                for (int index = 0; index < originObjs.Count; index++)
                {
                    // 如果对比发现，参考和原版不一致，那么把参考的值填进translation
                    if (originObjs[index] != referenceObjs[index])
                    {
                        objs.Add(new JObject
                        {
                            // 键值是 类型 + 相对路径(去除后缀) + 5位数字ID
                            ["key"] = new StringBuilder(type)
                            .Append(Path.ChangeExtension(relativePath, ""))
                            .Append(index.ToString().PadLeft(5, '0'))
                            .ToString(),
                            ["original"] = originObjs[index],
                            ["translation"] = referenceObjs[index]
                        });
                    }
                    else
                    {
                        objs.Add(new JObject
                        {
                            ["key"] = new StringBuilder(type)
                            .Append(Path.ChangeExtension(relativePath, ""))
                            .Append(index.ToString().PadLeft(5, '0'))
                            .ToString(),
                            ["original"] = originObjs[index],
                            ["translation"] = ""
                        });
                    }
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
    public (List<string> name, List<string> text) GetListTuple()
    {
        return (name: varNameList, text: textList);
    }

    public void DebugPrint()
    {
        Console.WriteLine("======变量名======");
        foreach (var item in VarNameListFilter(varNameList))
        {
            Console.WriteLine(item);
        }
        Console.WriteLine("======纯文本======");
        foreach (var item in TextListFilter(textList))
        {
            Console.WriteLine(item);
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

}