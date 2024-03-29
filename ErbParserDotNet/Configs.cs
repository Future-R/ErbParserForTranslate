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
    public static HashSet<string> extensions { get; private set; }
    // 常用的有UTF-8、UTF-8 with BOM、Shift JIS
    public static Encoding fileEncoding { get; private set; }

    // 解析表达式需要的符号集合
    public static List<string> operators { get; private set; }
    // 强力过滤变量名
    public static bool forceFilter = true;
    // 执行完毕后自动打开文件夹
    public static bool autoOpenFolder = false;

    public static void Init()
    {
        // Config读取配置
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        if (File.Exists(configPath))
        {
            string jsonContent = File.ReadAllText(configPath);
            JObject configs = JsonConvert.DeserializeObject<JObject>(jsonContent);

            extensions = new HashSet<string>(JsonConvert.DeserializeObject<string[]>(configs["读取这些扩展名的游戏文件"].ToString()));

            // Encoding.GetEncoding无法获取带BOM的UTF-8，这里做特殊处理
            string encoding = configs["读取文件使用的编码"].ToString();
            fileEncoding = encoding.Contains("BOM") ? new UTF8Encoding(true) : Encoding.GetEncoding(encoding);

            operators = configs["需要处理的操作符"].ToObject<List<string>>();

            forceFilter = (bool)configs["强力过滤变量名"].ToObject(typeof(bool));
            autoOpenFolder = (bool)configs["执行完毕后自动打开文件夹"].ToObject(typeof(bool));
        }
        else
        {
            Console.WriteLine("【错误】：缺少config.json配置文件！");
            Console.ReadKey();
            Environment.Exit(0);
        }
    }
}
