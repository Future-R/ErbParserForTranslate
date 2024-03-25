using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class Start
{
    // 之后从配置json里读取
    static readonly string[] erbExtensions = new string[] { ".erb", ".erh" };
    static readonly HashSet<string> extensions = new HashSet<string> { ".csv", ".CSV", ".erb", ".ERB", ".erh", ".ERH" };
    //public static readonly Encoding fileEncoding = Encoding.GetEncoding("EUC-JP");
    public static readonly Encoding fileEncoding = Encoding.GetEncoding("UTF-8");
    public static void Main()
    {
        string appPath = System.AppDomain.CurrentDomain.BaseDirectory;
        Tools.Init();

        while (true)
        {
            // 主菜单
            Console.WriteLine("请输入序号并回车（默认为0）：");
            Console.WriteLine("[0] - 用字典汉化游戏（WIP）\n[1] - 提取文本到字典\n[2] - 补充新版本条目到字典（TODO）\n[3] - 从已汉化本体中提取字典");
            string command = Console.ReadLine();
            switch (command)
            {
                case "0":
                    Translator();
                    break;
                case "1":
                    ReadFile(appPath);
                    break;
                case "2":
                    break;
                case "3":
                    ReadFile(appPath, true);
                    break;
                default:
                    Translator();
                    break;
            }

            Console.ReadKey();
            Console.Clear();
        }
    }

    /// <summary>
    /// 用字典汉化游戏
    /// </summary>
    static void Translator()
    {
        Console.WriteLine("请拖入需要汉化的游戏根目录（请做好备份）：");
        string gameDirectory = Console.ReadLine().Trim('"');
        Console.WriteLine("请拖入放置CSV和ERB目录的译文目录：");
        string transFileDirectory = Console.ReadLine().Trim('"');

        // 统计耗时
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        // 遍历所有译文JSON
        Parallel.ForEach(Directory.GetFiles(transFileDirectory, "*.json", SearchOption.AllDirectories), (jsonFile) =>
        {
            // 获取相对路径
            var relativePath = jsonFile.Substring(transFileDirectory.Length + 1);
            bool isCSV = relativePath.StartsWith("CSV");
            bool fileExist = false;
            // 得到输出路径
            var targetFile = Path.Combine(gameDirectory, relativePath);

            // 遍历所有可能的扩展，后悔之前多手把扩展截掉了，现在想改稍微有点麻烦
            foreach (var ext in extensions)
            {
                string newFile = Path.ChangeExtension(targetFile, ext);
                if (File.Exists(newFile))
                {
                    // 得到输出文件后缀
                    targetFile = newFile;
                    fileExist = true;
                    break;
                }
            }
            // 如果目标脚本不存在，跳过这一条并报错
            if (!fileExist)
            {
                if (isCSV)
                {
                    Console.WriteLine($"【错误】：没找到{targetFile}.CSV！");
                }
                else
                {
                    Console.WriteLine($"【错误】：没找到{targetFile}的ERB脚本！");
                }
            }
            else
            {
                // 读取pt译文
                string ptJsonContent = File.ReadAllText(jsonFile);
                // pt的json都是[起头的，如果不是，那就跳过这个文件
                if (!ptJsonContent.StartsWith("["))
                {
                    Console.WriteLine($"【错误】：{jsonFile}无法被正确解析！");
                }
                else
                {
                    JArray jsonArray = JArray.Parse(ptJsonContent);
                    // 读取游戏脚本
                    string scriptContent = File.ReadAllText(targetFile, fileEncoding);
                    // 译文按original的长度从长到短排序，以此优先替换长文本，再替换短文本，大概率避免错序替换
                    var dictObjs = jsonArray.ToObject<List<JObject>>().OrderByDescending(obj => obj["original"].ToString().Length);
                    foreach (JObject dictObj in dictObjs)
                    {
                        string key = dictObj["original"].ToString();
                        string value = dictObj["translation"].ToString();
                        // stage：-1已隐藏; 0未翻译；1已翻译；2有疑问；3已检查; 5已审核；9已锁定
                        if (dictObj["stage"].ToObject<int>() > 0)
                        {
                            scriptContent = scriptContent.Replace(key, value);
                        }
                    }
                    // 覆盖写入
                    File.WriteAllText(targetFile, scriptContent);
                }
            }
        });

        // 格式化并输出耗时
        stopwatch.Stop();
        TimeSpan ts = stopwatch.Elapsed;
        string elapsedTime = String.Format("{0:00}秒{1:00}",
            ts.Seconds,
            ts.Milliseconds / 10);
        Console.WriteLine("耗时：" + elapsedTime);
        Console.WriteLine("翻译已完成！");
    }
    /// <summary>
    /// 提取字典
    /// </summary>
    /// <param name="appPath">程序路径</param>
    /// <param name="merge">合并模式：需要先后拖入原版和汉化版路径</param>
    /// <exception cref="DirectoryNotFoundException"></exception>
    static void ReadFile(string appPath, bool merge = false)
    {
        // 清理上次生成的文件，必须放前面不然删得慢了碰到后面的多线程会报错
        if (Directory.Exists(Path.Combine(appPath, "CSV"))) Directory.Delete(Path.Combine(appPath, "CSV"), true);
        if (Directory.Exists(Path.Combine(appPath, "ERB"))) Directory.Delete(Path.Combine(appPath, "ERB"), true);

        string mergePath = "";
        if (merge)
        {
            Console.WriteLine("请拖入已经汉化的游戏根目录（作为翻译参考）：");
            mergePath = Console.ReadLine().Trim('"');
        }

        Console.WriteLine("请拖入需要汉化的游戏根目录：");
        string path = Console.ReadLine().Trim('"');

        string csvDirectory = Path.Combine(path, "CSV");
        string erbDirectory = Path.Combine(path, "ERB");

        // 统计耗时
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        if (Directory.Exists(csvDirectory))
        {
            // 获取所有csv文件
            var csvNames = Directory.GetFiles(csvDirectory, "*.csv", SearchOption.AllDirectories);
            Parallel.ForEach(csvNames, csvName =>
            {
                // 获取相对路径
                var relativePath = csvName.Substring(path.Length + 1);
                // 得到输出路径
                var targetFile = Path.Combine(appPath, relativePath);
                CSVParser parser = new CSVParser();
                parser.ParseFile(csvName);

                // 输出Json
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                if (merge)
                {
                    var referencePath = Path.Combine(mergePath, relativePath);
                    if (File.Exists(referencePath))
                    {
                        CSVParser referenceParser = new CSVParser();
                        referenceParser.ParseFile(referencePath);
                        parser.WriteJson(targetFile, relativePath, referenceParser.GetList());
                    }
                    else
                    {
                        Console.WriteLine($"【警告】：未能找到{referencePath}！");
                        parser.WriteJson(targetFile, relativePath);
                    }
                }
                else
                {
                    parser.WriteJson(targetFile, relativePath);
                }
            });
        }
        else
        {
            throw new DirectoryNotFoundException($"找不到CSV目录: {csvDirectory}");
        }
        if (Directory.Exists(erbDirectory))
        {
            // 获取所有erb和erh文件
            var erbNames = Directory.EnumerateFiles(erbDirectory, "*.*", SearchOption.AllDirectories)
            .Where(file => erbExtensions.Any(x => file.EndsWith(x, StringComparison.OrdinalIgnoreCase)));

            Parallel.ForEach(erbNames, erbName =>
            {
                // 获取相对路径
                var relativePath = erbName.Substring(path.Length + 1);
                // 得到输出路径
                var targetFile = Path.Combine(appPath, relativePath);
                ERBParser parser = new ERBParser();
                parser.ParseFile(erbName);
                //parser.DebugPrint();

                // 输出Json
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                if (merge)
                {
                    var referencePath = Path.Combine(mergePath, relativePath);
                    if (File.Exists(referencePath))
                    {
                        ERBParser referenceParser = new ERBParser();
                        referenceParser.ParseFile(referencePath);
                        parser.WriteJson(targetFile, relativePath, referenceParser.GetListTuple());
                    }
                    else
                    {
                        Console.WriteLine($"【警告】：未能找到{referencePath}！");
                        parser.WriteJson(targetFile, relativePath);
                    }
                }
                else
                {
                    parser.WriteJson(targetFile, relativePath);
                }
            });
        }
        else
        {
            throw new DirectoryNotFoundException($"找不到ERB目录: {erbDirectory}");
        }

        // 格式化并输出耗时
        stopwatch.Stop();
        TimeSpan ts = stopwatch.Elapsed;
        string elapsedTime = String.Format("{0:00}秒{1:00}",
            ts.Seconds,
            ts.Milliseconds / 10);
        Console.WriteLine("耗时：" + elapsedTime);
        Console.WriteLine("已将生成的JSON放置在此程序目录下");
    }
}

