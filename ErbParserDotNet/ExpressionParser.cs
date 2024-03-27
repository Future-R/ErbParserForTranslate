﻿using System;
using System.Collections.Generic;

// 解析写麻了，干脆之后弄个第三方解析库算了
// 先暴力处理括号，如果括号左边是英文，就当它是函数名，把这个token删掉
// 因为括号有可能涉及函数嵌套，我没存函数名真的不好处理
// 测试用例1【括号】：ARGS == "MAX" && LOCAL:1 < CFLAG:LOCAL:(ARG:1)
// 测试用例2【函数】：MATCH(食材選択,GETNUM(ITEM, "レタス")) && MATCH(食材選択,GETNUM(ITEM, "トマト")) && MATCH(食材選択,GETNUM(ITEM, "パン"))
// 测试用例3【三元】：ARG:1 == -1 ? MASTER # ARG:1
// 测试用例4【赋值】：MAXBASE:キャラ_ID:体力 += (CFLAG:キャラ_ID:401 * 10)
// 测试用例5【花括号】：!ENUMFILES("RESOURCES", @"{NO:ARG}*", 1) && !ENUMFILES("RESOURCES", @"%NAME:ARG%*", 1)
class ExpressionParser
{
    static readonly List<string> operators = new List<string> { "(", ")", "?", "#", ">", "<", "==", "!=", ">=", "<=", "+=", "-=", "!", "&&", "||", "&", "|", ",", ":", "+", "-", "*", "/", "%", "{", "}", "TO" };
    public static (List<string> vari, List<string> text) Slash(string expression)
    {
        List<string> variables = new List<string>();
        List<string> constants = new List<string>();

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
                if (i < expression.Length - 1 &&
                    ((temp == "=" && expression[i + 1] == '=')
                    || (temp == "&" && expression[i + 1] == '&')
                    || (temp == "|" && expression[i + 1] == '|')
                    || (temp == ">" && expression[i + 1] == '=')
                    || (temp == "<" && expression[i + 1] == '=')
                    || (temp == "!" && expression[i + 1] == '=')
                    || (temp == "+" && expression[i + 1] == '=')
                    || (temp == "-" && expression[i + 1] == '=')
                    || (temp == "T" && expression[i + 1] == 'O')))
                {
                    temp += expression[++i];
                }

                if (operators.Contains(temp))
                {
                    // 左边如果是英文，暴力判断为函数，把函数名从变量名列表中remove
                    if (temp == "(" && Char.IsLetter(lastChar) && variables.Count > 0)
                    {
                        variables.RemoveAt(variables.Count - 1);
                    }
                    // 这里捕获到了完整的运算符，但翻译用不到，所以没做存储
                    //Console.WriteLine($"符号：{temp}");
                    temp = "";
                }
                else if ((Char.IsLetterOrDigit(expression[i]) || expression[i] == '_') && (i == expression.Length - 1 || !Char.IsLetterOrDigit(expression[i + 1]) && expression[i + 1] != '_'))
                {
                    // 捕获变量名
                    variables.Add(temp);
                    temp = "";
                }
                else if (Char.IsDigit(expression[i]) && !operators.Contains(temp) && (i == expression.Length - 1 || !Char.IsDigit(expression[i + 1])))
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
}
