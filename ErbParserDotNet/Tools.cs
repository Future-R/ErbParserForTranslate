using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


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
        string kw_eng = @"[_a-zA-Z0-9]\w*";
        engArrayFilter = new Regex($@"^{kw_eng}(:{kw_eng}){{0,2}}$", RegexOptions.Compiled);

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


    public static string LockReplace(string gameContent, JArray jsonArray)
    {
        // 译文按original的长度从长到短排序，以此优先替换长文本，再替换短文本，大概率避免错序替换
        var dictObjs = jsonArray.ToObject<List<JObject>>().OrderByDescending(obj => obj["original"].ToString().Length);

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
            if (indices.Exists(index => index < gameContent.Length))
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
}
