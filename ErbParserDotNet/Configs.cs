using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public static class Configs
{
    public const string Version = "0.58a";
    public static HashSet<string> extensions { get; private set; }
    // 常用的有UTF-8、UTF-8 with BOM、Shift JIS
    public static Encoding fileEncoding { get; private set; }

    // 解析表达式需要的符号集合
    public static List<string> operators { get; private set; }

    // 允许担当变量名的字符
    public static List<string> var_operators { get; private set; }
    // 强力过滤变量名
    public static bool forceFilter = true;
    // 执行完毕后自动打开文件夹
    public static bool autoOpenFolder = false;

    // 刷完字典后参考CSV文件进行ERB全局替换，在项目初期可以解决一些内插变量名的错误，不是所有项目都需要的
    public static bool autoReplace = false;

    public static bool hideEngText = true;

    public static bool mergeSameText = true;

    public static bool hideVarOutput = false;

    public static bool mergeString = false;

    public static string[] autoReplaceRefer = new string[0];

    public static void Init()
    {
        // Config读取配置
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        if (!File.Exists(configPath))
        {
            var config = new
            {
                读取文件使用的编码 = "UTF8-BOM",
                强力过滤变量名 = true,
                执行完毕后自动打开文件夹 = true,
                变量自动修正 = true,
                隐藏英文词条 = true,
                合并同一文件的相同词条 = false,
                屏蔽变量输出 = false,
                合并联立字符串 = true,
                读取这些扩展名的游戏文件 = new string[] { ".csv", ".erb", ".erh" },
                允许构成变量名的字符 = new string[] { "☆", "♡", "∀", "←", "→" },
                需要处理的操作符 = new string[]
            {
                "(", ")", "{", "}",
                "?", "#",
                "=", "+=", "-=", "*=", "/=",
                ">", "<", "==", "!=", ">=", "<=",
                "&&", "||", "&", "|", "!",
                "+", "-", "*", "/", "%",
                "++", "--",
                ",", ":", "TO"
            },
                变量自动修正参考文件 = new string[]
            {
                "Abl", "Base", "CFlag", "CSTR", "Ex",
                "Exp", "Flag", "Global", "Item", "Juel",
                "Mark", "Nowex", "Palam", "Source", "Stain",
                "Talent", "TCVAR", "TEquip", "Tflag", "Train",
                "TSTR", "修正字典"
            },
            };

            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configPath, json);
            Console.WriteLine("首次运行，已生成配置文件，请按任意键继续！");
            Console.ReadKey();
        }
        string jsonContent = File.ReadAllText(configPath);
        JObject configs = JsonConvert.DeserializeObject<JObject>(jsonContent);

        extensions = new HashSet<string>(JsonConvert.DeserializeObject<string[]>(configs["读取这些扩展名的游戏文件"].ToString()));

        // Encoding.GetEncoding无法获取带BOM的UTF-8，这里做特殊处理
        string encoding = configs["读取文件使用的编码"].ToString();
        fileEncoding = encoding.Contains("BOM") ? new UTF8Encoding(true) : Encoding.GetEncoding(encoding);

        operators = configs["需要处理的操作符"].ToObject<List<string>>();

        var_operators = configs["允许构成变量名的字符"].ToObject<List<string>>();

        forceFilter = (bool)configs["强力过滤变量名"].ToObject(typeof(bool));
        autoOpenFolder = (bool)configs["执行完毕后自动打开文件夹"].ToObject(typeof(bool));
        autoReplace = (bool)configs["变量自动修正"].ToObject(typeof(bool));
        autoReplaceRefer = (string[])configs["变量自动修正参考文件"].ToObject(typeof(string[]));
        hideEngText = (bool)configs["隐藏英文词条"].ToObject(typeof(bool));
        mergeSameText = (bool)configs["合并同一文件的相同词条"].ToObject(typeof(bool));
        hideVarOutput = (bool)configs["屏蔽变量输出"].ToObject(typeof(bool));
        mergeString = (bool)configs["合并联立字符串"].ToObject(typeof(bool));
    }
}
