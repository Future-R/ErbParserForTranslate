using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public class ERBParser
{
    // 正则匹配输出指令的右值
    private static readonly Regex EraOutputPattern = new Regex(
        @"(PRINT|PRINTSINGLE|PRINTC|PRINTDATA|PRINTBUTTON|PRINTPLAIN)(?:FORM|FORMS)?(?:K|D)?(?:L|W)?\s*(?<modifier>[|].*)?\s*(?<value>.*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 匹配系统变量，这些变量不会丢给译者翻译
    private static readonly string[] OriginVarName = new[]
    {
        "LOCAL", "LOCALS", "ARG", "ARGS", "RESULT", "RESULTS", "COUNT"
    };


    // 想了想，反正单纯只是提取文本，不需要Execute，所以只需要逐行提取，不需要保存结构
    List<string> lineList = new List<string>();
    List<string> varNameList = new List<string>();
    List<string> textList = new List<string>();

    public void ParseFile(string filePath)
    {
        try
        {
            string content = File.ReadAllText(filePath);
            lineList = content.Split(new[] { Environment.NewLine }, StringSplitOptions.None).ToList();
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
            else if (lineString.StartsWith("@"))
            {
                int start = lineString.IndexOf("(");
                int end = start != -1 ? lineString.IndexOf(")", start) : -1;
                // 函数在定义时要么只出现一对括号，要么没有括号而是用逗号划分
                // 左括号索引小于右括号索引，且左括号索引不为-1，说明匹配到正确的括号了
                if (start != -1 && start < end)
                {
                    string args = lineString.Substring(start + 1, end - start - 1);
                    varNameList.AddRange(args.Split(',')
                        .Where(arg => !string.IsNullOrWhiteSpace(arg))
                        .Select(arg => arg.Trim()));
                }
                else
                {
                    var enumer = lineString.Split(',')
                        .Where(arg => !string.IsNullOrWhiteSpace(arg))
                        .Select(arg => arg.Trim());
                    // enumer.FirstOrDefault()是函数名，不需要翻译
                    // 其它成员是参数名
                    varNameList.AddRange(enumer.Skip(1));
                }
            }
            // 声明变量，此时只会出现至多一个等号，所以以单个等号为判断依据。
            else if (lineString.StartsWith("#DIM"))
            {
                int eqIndex = lineString.IndexOf("=");
                // 无等号的话，只声明不赋值。以空格分隔，最后一个元素是变量名。
                if (eqIndex == -1)
                {
                    var enumer = lineString.Split(' ')
                        .Where(arg => !string.IsNullOrWhiteSpace(arg))
                        .Select(arg => arg.Trim());
                    varNameList.Add(enumer.LastOrDefault());
                }
                // 有等号的话，还要额外获取右值。右值可能是数字、字符串，可能会用逗号分隔。
                // 字符串型可能会以"'="的方式赋值，不过变量在声明时无法引用，所以不必裁剪左值末尾的单引号
                else
                {
                    string leftValue = lineString.Substring(0, eqIndex).Trim();
                    string rightValue = lineString.Substring(eqIndex + 1).Trim();

                    var leftEnumer = leftValue.Split(' ')
                        .Where(arg => !string.IsNullOrWhiteSpace(arg))
                        .Select(arg => arg.Trim());
                    varNameList.Add(leftEnumer.LastOrDefault());

                    // 类型推导，如果是数值型，那么右值不用翻译，continue掉
                    bool isIntegerValue = leftEnumer.FirstOrDefault() == "#DIM";
                    if (isIntegerValue) continue;
                    // 否则是字符串型，逗号切割，trim掉双引号，但不要trim掉引号里的空格
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
            // Switch-Case的Switch
            else if (lineString.StartsWith("SELECTCASE "))
            {
                var rightValue = lineString.Substring(10).Trim();
                varNameList.Add(rightValue);
            }
            // 右值不是数字就直接扔给译者
            else if (lineString.StartsWith("CASE "))
            {
                var rightValue = lineString.Substring(4).Trim();
                if (!int.TryParse(rightValue, out _)) textList.Add(rightValue);
            }
            else
            {
                // 匹配赋值，左值一定是变量，右值直接扔给译者算了
                if (lineString.Contains(" = "))
                {
                    int eqIndex = lineString.IndexOf("=");
                    string leftValue = lineString.Substring(0, eqIndex).Trim();
                    string rightValue = lineString.Substring(eqIndex + 1).Trim();
                    varNameList.Add(leftValue);
                    if (!int.TryParse(rightValue, out _)) textList.Add(rightValue);
                }
                // 匹配字符串'=赋值，左值一定是字符串变量，右值一定是字符串
                if (lineString.Contains(" '= "))
                {
                    int eqIndex = lineString.IndexOf("=");
                    string leftValue = lineString.Substring(0, eqIndex).Trim().TrimEnd('\'');
                    string rightValue = lineString.Substring(eqIndex + 1).Trim();
                    varNameList.Add(leftValue);
                    if (!int.TryParse(rightValue, out _)) textList.Add(rightValue);
                }
                // 匹配输出
                // 懒得手写了，用正则先跑起来再说
                else
                {
                    MatchCollection matches = EraOutputPattern.Matches(lineString);
                    foreach (Match match in matches)
                    {
                        string value = match.Groups["value"].Value.Trim();
                        //textList.Add("【正则】" + value);
                        textList.Add(value);
                    }
                }
            }
        }
    }

    // 将变量名按:拆分，剔除自然数，合并重复成员，剔除系统内置变量名
    List<string> VarNameListSplit(List<string> originalList)
    {
        List<string> splitList = new List<string>();
        foreach (string item in originalList)
        {
            splitList.AddRange(item.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries));
        }
        return splitList.Distinct().Where(s => !IsNaturalNumber(s)).Except(OriginVarName).ToList();
    }
    // 剔除系统内置变量名
    // 是否需要剔除当前文件的变量名，待观察
    List<string> TextListFilter(List<string> originalList)
    {
        return originalList.Except(OriginVarName).ToList();
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

        public void DebugPrint()
    {
        Console.WriteLine("======变量名======");
        foreach (var item in VarNameListSplit(varNameList))
        {
            Console.WriteLine(item);
        }
        Console.WriteLine("======纯文本======");
        foreach (var item in TextListFilter(textList))
        {
            Console.WriteLine(item);
        }
    }

}