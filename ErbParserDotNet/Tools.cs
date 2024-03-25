using System;
using System.Collections.Generic;
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
    }
}
