﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;


/// <summary>
/// 静态工具类
/// </summary>
public static class Tools
{
    /// <summary>
    /// 【更暴力】匹配XX:YY:ZZ的纯英文+数字+下划线变量
    /// </summary>
    public static Regex engArrayFilter;

    /// <summary>
    /// 【更精细】匹配XX:YY:ZZ的系统变量
    /// </summary>
    public static Regex sysArrayFilter;

    /// <summary>
    /// 匹配末尾空格
    /// </summary>
    public static Regex lastNum;

    /// <summary>
    /// 匹配函数定义
    /// </summary>
    public static Regex dimFunction;

    /// <summary>
    /// 捕获RESULTS的右值，由于行内无法判断右值是否为字符串类型，只能通过此下策多少捞回一点
    /// </summary>
    public static Regex resultsCatch;

    /// <summary>
    /// 捕获RESULTS的右值，由于行内无法判断右值是否为字符串类型，只能通过此下策多少捞回一点
    /// </summary>
    public static Regex englishTextCatch;

    /// <summary>
    /// 匹配完全没有双字节的字符串
    /// </summary>
    public static Regex haventDoubleByte;

    /// <summary>
    /// 全局变量，似乎暂时用不到
    /// </summary>
    private static readonly string[] OriginVarName = new[]
    {
        "ARG", "ARGS",
        "LOCAL", "LOCALS",
        "RESULT", "RESULTS",
        "GLOBAL", "GLOBALS",
        "COUNT", "RAND", "CHARANUM"
    };

    /// <summary>
    /// 初始化工具类
    /// </summary>
    public static void Init()
    {
        string kw_eng = @"[()_a-zA-Z0-9]*";
        // 前面加上正负号，就顺便把纯数也包含进来一起判断了
        engArrayFilter = new Regex($@"^[-+]?{kw_eng}(:{kw_eng}){{0,2}}$", RegexOptions.Compiled);

        string kw___var__ = "(?:__(?:FILE|FUNCTION|INT_MAX|INT_MIN|LINE)__)";
        string kw_BASE = "(?:(?:DOWN|LOSE|MAX)?BASE)";
        string kw_CDFLAG = "(?:CDFLAG)";
        string kw_CDFLAGNAME = $"(?:{kw_CDFLAG}(?:NAME1|NAME2))";
        string kw_GAMEBASE = "(?:GAMEBASE_(?:ALLOWVERSION|AUTHOR|DEFAULTCHARA|GAMECODE|INFO|NOITEM|TITLE|VERSION|YEAR))";
        string kw_var_int1 = "(?:ARG|GLOBAL|LOCAL|RESULT)";
        string kw_var_int2 = "(?:ABL|BASE|CFLAG|CSTR|EQUIP|EX|EXP|FLAG|GLOBAL|ITEM|JUEL|MARK|MASTER|PALAM|SOURCE|STAIN|TALENT|TCVAR|TEQUIP|TFLAG)";
        string kw_var_str2 = "(?:SAVESTR|STR|TSTR)";
        string kw_var__S = "(?:{{kw_var_int1}}(?:S))";
        string kw_var__NAME = $"(?:(?:{kw_var_int2}|{kw_var_str2})(?:NAME))";
        string kw_num = @"\d{1,4}";
        string kw_var_string = $"(?:{kw_var__S}|{kw_var__NAME}|{kw_var_str2}|{kw_CDFLAGNAME}|CALLNAME|DRAWLINESTR|GLOBALSNAME|LASTLOAD_TEXT|MONEYLABEL|NAME|NICKNAME|SAVEDATA_TEXT|TRAINNAME|WINDOW_TITLE)";
        string kw_variable = $"(?:{kw_num}|{kw___var__}|{kw_var_int1}|{kw_var_int2}|{kw_var_string}|{kw_BASE}|{kw_CDFLAG}|{kw_GAMEBASE}|ASSI|ASSIPLAY|BOUGHT|CDOWN|CHARANUM|COUNT|CUP|DA|DAY|DB|DC|DD|DE|DITEMTYPE|DOWN|EJAC|EXPLV|GOTJUEL|ISASSI|ISTIMEOUT|ITEMPRICE|ITEMSALES|LASTLOAD_NO|LASTLOAD_VERSION|LINECOUNT|MONEY|NEXTCOM|NO|NOITEM|NOWEX|PBAND|PLAYER|PREVCOM|RAND|RANDDATA|PALAMLV|RELATION|SELECTCOM|TA|TARGET|TB|TIME|UP)";

        sysArrayFilter = new Regex($"^{kw_variable}(:{kw_variable}){{0,2}}$", RegexOptions.Compiled);

        lastNum = new Regex(@"(\d+)(?=\D*$)", RegexOptions.Compiled);

        dimFunction = new Regex(@"(\w+)\((.*?)\)", RegexOptions.Compiled);

        resultsCatch = new Regex(@"(?<=RESULTS:?.* = ).+", RegexOptions.Compiled);

        englishTextCatch = new Regex(@"^[a-zA-Z0-9_.%\(\),+\-\*\\ \[\]{}'""]+$", RegexOptions.Compiled);

        haventDoubleByte = new Regex(@"^[\x00-\xff]+$", RegexOptions.Compiled);
    }

    /// <summary>
    /// 判断字符串是否完全是ERA变量（XX:YY:ZZ形式）
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static bool IsArray(string text)
    {
        return Configs.forceFilter ? engArrayFilter.IsMatch(text) : sysArrayFilter.IsMatch(text);
    }

    /// <summary>
    /// 删除目录
    /// </summary>
    /// <param name="directoryPath"></param>
    public static void CleanDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, true);
        }
    }

    /// <summary>
    /// 获取用户输入
    /// </summary>
    /// <param name="prompt"></param>
    /// <returns></returns>
    public static string ReadLine(string prompt)
    {
        Console.WriteLine(prompt);
        return Console.ReadLine().Trim('"');
    }

    /// <summary>
    /// 获得文件相对于rootPath的路径
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public static string GetrelativePath(string filePath, string rootPath)
    {
        return filePath.Substring(rootPath.Length + 1);
    }

    public static IEnumerable<string> GetCSVValue(string line, bool banNumber = true)
    {
        // 先修剪行末注释
        var indexA = line.IndexOf(';');
        if (indexA == 0) return null;
        var contentWithoutComment = indexA != -1 ? line.Substring(0, indexA) : line;
        // 修剪括号注释
        var indexB = contentWithoutComment.IndexOf('(');
        contentWithoutComment = indexB != -1 ? contentWithoutComment.Substring(0, indexB) : contentWithoutComment;

        // 分割一行的内容
        var parts = contentWithoutComment.Split(',');

        // 提取除了第一列以外的内容
        IEnumerable<string> result = parts.Skip(1)
            // 筛除空成员
            .Where(text => !string.IsNullOrWhiteSpace(text));
        if (banNumber)
        {
            // 筛选纯数字
            result = result.Where(text => !int.TryParse(text.Trim(), out _));
        }
        return result.Select(s => s.TrimStart());
    }

    public static string GetResValue(string line, bool banNumber = true)
    {
        // 先修剪行末注释
        var indexA = line.IndexOf(';');
        if (indexA == 0) return null;
        var contentWithoutComment = indexA != -1 ? line.Substring(0, indexA) : line;
        // 修剪括号注释
        var indexB = contentWithoutComment.IndexOf('(');
        contentWithoutComment = indexB != -1 ? contentWithoutComment.Substring(0, indexB) : contentWithoutComment;

        // 分割一行的内容
        var parts = contentWithoutComment.Split(',');

        // 只提取第一列内容
        string result = parts.FirstOrDefault().Trim();

        return result;
    }


    public static string RemoveSkippedText(string input)
    {
        var result = new StringBuilder();
        int currentIndex = 0;

        while (currentIndex < input.Length)
        {
            int startIndex = input.IndexOf("[SKIPSTART]", currentIndex);
            if (startIndex == -1)
            {
                result.Append(input.Substring(currentIndex));
                break;
            }

            // 检测[SKIPSTART]所在行是否被注释
            if (IsLineCommented(input, startIndex))
            {
                currentIndex = startIndex + "[SKIPSTART]".Length;
                continue;
            }

            int endIndex = input.IndexOf("[SKIPEND]", startIndex);
            if (endIndex == -1)
            {
                result.Append(input.Substring(currentIndex));
                break;
            }

            // 追加未被跳过的内容
            result.Append(input, currentIndex, startIndex - currentIndex);
            currentIndex = endIndex + "[SKIPEND]".Length;
        }
        return result.ToString();
    }

    private static bool IsLineCommented(string input, int position)
    {
        // 查找行首位置
        int lineStart = input.LastIndexOf('\n', position) + 1;
        if (lineStart < 0) lineStart = 0;

        // 跳过行首空格/制表符后检查分号
        for (int i = lineStart; i <= position; i++)
        {
            if (i >= input.Length) return false;
            if (input[i] == ';' && (i == lineStart || char.IsWhiteSpace(input[i - 1])))
                return true;
            if (!char.IsWhiteSpace(input[i]))
                break;
        }
        return false;
    }

    /// <summary>
    /// 以非常谨慎的方式进行替换，效率比较低，但是很安全
    /// <br>单个文件中，已经被翻译过的部分会被标记，不再进行二次翻译</br>
    /// <br>虽然性能不是主要问题，不过之后应该会提供另一种效率更高的实现</br>
    /// <br>操作字符串还是太慢了，直接剔除被包含的JArray成员应该会快得多</br>
    /// </summary>
    /// <param name="gameContent"></param>
    /// <param name="jsonArray"></param>
    /// <returns></returns>
    [Obsolete("这个方法已经被弃用，因为它的效率很低。请改用ACReplace代替。")]
    public static string LockReplace(string gameContent, JArray jsonArray)
    {
        // 译文按original的长度从长到短排序，以此优先替换长文本，再替换短文本，大概率避免错序替换
        var dictObjs = jsonArray.ToObject<List<JObject>>()
            .Where(obj => (int)obj["stage"].ToObject(typeof(int)) > 0)
            .OrderByDescending(obj => obj["original"].ToString().Length);

        // 已翻译过的字符会被标记锁定，不会再被二次翻译，避免短词典在长词典的译文上工作
        List<int> lockIndices = new List<int>();

        // 遍历字典，进行替换
        foreach (var dictObj in dictObjs)
        {
            string key = dictObj["original"].ToString();
            string value = dictObj["translation"].ToString();

            int startIndex = 0;
            // 记录原文在目标字符串中的位置
            List<int> indices = new List<int>();

            // 查找目标字符串中所有原文的位置
            while ((startIndex = gameContent.IndexOf(key, startIndex)) != -1)
            {
                indices.Add(startIndex);
                startIndex += key.Length; // 移动到下一个可能的匹配位置
            }

            // 如果已经替换过，则跳过
            if (lockIndices.Exists(index => index < gameContent.Length))
            {
                continue;
            }

            // 替换目标字符串中的所有匹配项，并更新indices列表
            foreach (int index in indices)
            {
                gameContent = gameContent.Remove(index, key.Length).Insert(index, value);
                // 更新indices列表中的所有索引，因为替换操作改变了后面的索引
                for (int i = 0; i < indices.Count; i++)
                {
                    indices[i] += value.Length - key.Length;
                }
            }
        }

        return gameContent;
    }
    /// <summary>
    /// 使用字典树来一次遍历原文并替换
    /// </summary>
    /// <param name="gameContent"></param>
    /// <param name="jsonArray"></param>
    /// <returns></returns>
    [Obsolete("这个方法已经被弃用，虽然速度很快，但我总是排查不干净Bug。请改用RegexReplace代替。")]
    public static string ACReplace(string gameContent, JArray jsonArray)
    {
        // 译文按original的长度从长到短排序，以此优先替换长文本，再替换短文本，大概率避免错序替换
        var dictObjs = jsonArray.ToObject<List<JObject>>()
            .Where(obj => (int)obj["stage"].ToObject(typeof(int)) > 0)
            .OrderByDescending(obj => obj["original"].ToString().Length);
        AhoCorasick ahoCorasick = new AhoCorasick();
        foreach (var dictObj in dictObjs)
        {
            string key = dictObj["original"].ToString();
            string value = dictObj["translation"].ToString();

            ahoCorasick.AddPattern(key, value);
        }
        ahoCorasick.Build();
        return ahoCorasick.Process(gameContent);
    }

    public static string TransResConfig(string gameContent, JArray jsonArray)
    {
        Dictionary<string, string> dictObjs = jsonArray.ToObject<List<JObject>>()
            .Where(obj => obj.ContainsKey("stage") && (int)obj["stage"].ToObject(typeof(int)) > 0)
            .ToDictionary(obj => obj["original"].ToString(), obj => obj["translation"].ToString());

        // 将字符串按行分割
        string[] lines = gameContent.Contains("\r\n") ? gameContent.Split("\r\n".ToCharArray(), StringSplitOptions.None) : gameContent.Split("\n".ToArray(), StringSplitOptions.None);

        // 用于存储替换后的行
        List<string> replacedLines = new List<string>();

        foreach (var line in lines)
        {
            string transLine = line;
            // 检查行是否以分号开始或是否包含逗号
            int index = line.IndexOf(',');
            if (!line.StartsWith(";") && index > -1)
            {
                // 截取第一个逗号前的字符串
                string key = line.Substring(0, index).Trim();

                // 如果字典中存在该键，则进行替换
                if (dictObjs.ContainsKey(key))
                {
                    // 替换该行
                    transLine = dictObjs[key] + line.Substring(index);
                }
            }
            // 将处理后的行添加到列表中
            replacedLines.Add(transLine);
        }

        // 将列表中的行重新组合成字符串
        return string.Join(Environment.NewLine, replacedLines);
    }

    /// <summary>
    /// 正则匹配并替换
    /// </summary>
    /// <param name="gameContent"></param>
    /// <param name="jsonArray"></param>
    /// <returns></returns>
    public static string RegexReplace(string gameContent, JArray jsonArray)
    {
        gameContent = gameContent.Replace("\r\n", "\n").Replace("\r", "\n").Replace("<Tab>", "\t");
        Dictionary<string, string> replacements = new Dictionary<string, string>();
        var dictObjs = jsonArray.ToObject<List<JObject>>()
            .Where(obj => obj.ContainsKey("stage") && (int)obj["stage"].ToObject(typeof(int)) > 0)
            .OrderByDescending(obj => obj["original"].ToString().Length);
        foreach (var dictObj in dictObjs)
        {
            string key = dictObj["original"].ToString().Replace("\r\n", "\n").Replace("\r", "\n").Replace("\\n", "\n").Replace("\n\n", "\n").Replace("<Tab>", "\t");
            string value = dictObj["translation"].ToString().Replace("\r\n", "\n").Replace("\r", "\n").Replace("\\n", "\n").Replace("\n\n", "\n").Replace("<Tab>", "\t");

            if (!replacements.ContainsKey(key))
            {
                replacements.Add(key, value);
            }
        }

        // 创建正则表达式对象
        List<string> escapedKeys = new List<string>();
        foreach (string key in replacements.Keys)
        {
            escapedKeys.Add(Regex.Escape(key));
        }
        Regex regex = new Regex(string.Join("|", escapedKeys));

        // 使用正则表达式替换字符串中的所有匹配项
        return regex.Replace(gameContent, match =>
        {
            string value;
            if (replacements.TryGetValue(match.Value, out value))
            {
                return value.Replace("\\n", Environment.NewLine);
            }
            else
            {
                return match.Value.Replace("\\n", Environment.NewLine);
            }
        });
    }

    /// <summary>
    /// 寻找第一个空格，如果找不到，就找第一个全角空格
    /// <br>坑爹语言，居然能把全角空格当空格用</br>
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static int GetSpaceIndex(string context)
    {
        int value = context.IndexOf(' ');
        if (value == -1)
        {
            value = context.IndexOf('　');
        }
        return value;
    }

    public static (string left, string right) GetSlashStringCouple(string context, char chara)
    {
        int index = context.IndexOf(chara);
        if (index != -1)
        {
            string leftPart = context.Substring(0, index);
            string rightPart = context.Substring(index + 1);
            return (leftPart, rightPart);
        }
        return (null, null);
    }
    public static HashSet<string> GetEraGamesDirectories(string[] directories)
    {
        var eraGamesDir = new HashSet<string>();
        foreach (var directory in directories)
        {
            var dirs = GetEraGamesDirectories(directory);
            foreach (var dir in dirs)
            {
                eraGamesDir.Add(dir);
            }
        }
        return eraGamesDir;
    }
    public static HashSet<string> GetEraGamesDirectories(string rootPath)
    {
        var currentDir = new DirectoryInfo(rootPath);
        var eraGames = new HashSet<string>();
        var allDirs = currentDir.GetDirectories("*", SearchOption.AllDirectories);
        foreach (var dir in allDirs)
        {
            var name = dir.Name;
            if (!name.Equals("erb", StringComparison.OrdinalIgnoreCase) && !name.Equals("csv", StringComparison.OrdinalIgnoreCase)) continue;
            var parent = dir.Parent;
            if (parent is null)
            {
                continue;
            }
            eraGames.Add(parent.FullName);
        }
        return eraGames;
    }

    // 削除结尾的<Tab>
    // 忽然觉得转义Tab是个坏主意，根本没必要保证译文的代码结构和原文一样呀
    public static string TrimEndTabs(string input)
    {
        const string tabLiteral = "<Tab>";
        while (input.EndsWith(tabLiteral))
        {
            input = input.Substring(0, input.Length - tabLiteral.Length);
        }
        return input;
    }
}
