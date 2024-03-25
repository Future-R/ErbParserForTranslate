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
    public static Encoding fileEncoding { get; private set; }
    public static bool forceFilter = true;
    public static bool autoOpenFolder = false;

    public static void Init()
    {
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        if (File.Exists(configPath))
        {
            string jsonContent = File.ReadAllText(configPath);
            JObject configs = JsonConvert.DeserializeObject<JObject>(jsonContent);

            extensions = new HashSet<string>(JsonConvert.DeserializeObject<string[]>(configs["读取这些扩展名的游戏文件"].ToString()));
            fileEncoding = Encoding.GetEncoding(configs["读取文件使用的编码"].ToString());
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
