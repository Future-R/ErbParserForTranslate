using System;
using System.Collections.Generic;

// 解析写麻了，干脆之后弄个第三方解析库算了
// 先暴力处理括号，如果括号左边是英文，就当它是函数名，把这个token删掉
// 因为括号有可能涉及函数嵌套，我没存函数名真的不好处理
// 测试用例1【括号】：ARGS == "MAX" && LOCAL:1 < CFLAG:LOCAL:(ARG:1)
// 测试用例2【函数】：MATCH(食材選択,GETNUM(ITEM, "レタス")) && MATCH(食材選択,GETNUM(ITEM, "トマト")) && MATCH(食材選択,GETNUM(ITEM, "パン"))
// 测试用例3【三元】：ARG:1 == -1 ? MASTER # ARG:1
// 测试用例4【赋值】：MAXBASE:キャラ_ID:体力 += (CFLAG:キャラ_ID:401 * 10)
// 测试用例5【花括号】：!ENUMFILES("RESOURCES", @"{NO:ARG}*", 1) && !ENUMFILES("RESOURCES", @"%NAME:ARG%*", 1)
// 测试用例6【汉字符号】：FLAG:自动补给：装甲材料 = ITEM_OWN:JGV(\"ITEMID_装甲材料\")

class ExpressionParser
{
    public static (List<string> vari, List<string> text) Slash(string expression)
    {
        List<string> variables = new List<string>();
        List<string> constants = new List<string>();

        // 这是为了解决 PRINTV 'LV,ABL:欲望,'(,ABL:欲望 * 1,')
        if (expression.Contains("'(,") && expression.Contains(",')"))
        {
            expression = expression.Replace("'(,", "").Replace(",')", "").TrimStart('\'');
        }

        string temp = "";
        char lastChar = ' ';
        // 暂时只考虑匹配字符串边界，而不考虑匹配花括号和百分号括起的内容
        bool inString = false;
        for (int i = 0; i < expression.Length; i++)
        {
            // 空白符如果不在字符串内，将作为边界
            if (Char.IsWhiteSpace(expression[i]) && !inString)
            {
                if (temp != "")
                {
                    if (Char.IsLetter(temp[0]))
                    {
                        variables.Add(temp);
                    }
                    else
                    {
                        constants.Add(temp);
                    }
                    temp = "";
                }
                continue;
            }

            temp += expression[i];

            // 匹配到引号，判断为字符串边界
            if (expression[i] == '\"')
            {
                inString = !inString;
                if (!inString)
                {
                    constants.Add(temp);
                    temp = "";
                }
                continue;
            }

            if (!inString)
            {
                if (Configs.operators.Contains(temp))
                {
                    while (i <= expression.Length - 2 && Configs.operators.Contains(temp + expression[i + 1]))
                    {
                        temp += expression[++i];
                    }
                    // 左边如果是英文或数字，暴力判断为函数，把函数名从变量名列表中remove
                    if (temp == "(" && (IsEngChar(lastChar) || Char.IsDigit(lastChar)) && variables.Count > 0)
                    {
                        variables.RemoveAt(variables.Count - 1);
                    }
                    // 这里捕获到了完整的运算符，但翻译用不到，所以没做存储
                    //Console.WriteLine($"符号：{temp}");
                    temp = "";
                }
                // 为了方便，暂时不支持在变量名中使用数字了
                else if (IsAllowChar(expression[i]) && (i == expression.Length - 1 || !IsAllowChar(expression[i + 1])))
                {
                    // 捕获变量名
                    variables.Add(temp);
                    temp = "";
                }
                else if (Char.IsDigit(expression[i]) && !Configs.operators.Contains(temp) && (i == expression.Length - 1 || !Char.IsDigit(expression[i + 1])))
                {
                    // 捕获数字或字符串
                    constants.Add(temp);
                    temp = "";
                }
            }


            lastChar = expression[i];
        }
        return (vari: variables, text: constants);
    }
    public static bool IsEngChar(char c)
    {
        return (c >= 65 && c <= 90) || (c >= 97 && c <= 122);
    }

    public static bool IsAllowChar(char c)
    {
        // 下划线、日文点、字母和汉字、全角符号、中文和日文符号
        return c == '_' || c == '・'
            || char.IsLetter(c)
            || (c >= 0xFF00 && c <= 0xFFEF)
            || (c >= 0x3000 && c <= 0x303F)
            || (c >= 0x3300 && c <= 0x33FF)
            || (c >= 0x3040 && c <= 0x309F)
            || (c >= 0x30A0 && c <= 0x30FF);
    }

}
