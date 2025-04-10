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
using Sharprompt;
using UtfUnknown;
using static ERBParser;
using static System.Net.Mime.MediaTypeNames;

public static class Start
{
    // 之后从配置json里读取
    static readonly string[] erbExtensions = new string[] { ".erb", ".erh" };

    public static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string appPath     =AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var    currentPath = Directory.GetCurrentDirectory().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Console.OutputEncoding = Encoding.UTF8;
        // 读取config.json配置
        Configs.Init();
        // 主要是预编译正则
        Tools.Init();
        Console.Title = $"字典工具{Configs.Version}";
        
        var isEquals = appPath.Equals(currentPath, StringComparison.OrdinalIgnoreCase);
        var directories =isEquals ? new string[] { appPath } : new string[] { appPath, currentPath };

        while (true)
        {
            // 主菜单
            string menuString =
@"
请输入序号并回车（默认为0）：
[ 0] - 用字典汉化游戏
[ 1] - 从游戏中提取PT字典（初次提取）
[ 2] - 补充新版本条目到字典（本体更新时提取）
[ 3] - 从已汉化本体中提取字典（本体和汉化版版本相同才能使用）
[ 4] - 将PT字典转换成MTool字典
[ 5] - 将MTool机翻导入PT字典
[ 6] - 查重，填充未翻译，警告不一致翻译
[ 7] - Era传统字典转PT字典
[ 8] - 将所有文件转换为UTF-8编码
[ 9] - 从单文件提取PT字典
[10] - 设置
[11] - 访问项目主页";
            string command = Tools.ReadLine(menuString);
            switch (command)
            {
                case "0":
                    Translator();
                    break;
                case "1":
                    ReadFile(appPath);
                    break;
                case "2":
                    versionUpdate(appPath);
                    break;
                case "3":
                    ReadFile(appPath, true);
                    break;
                case "4":
                    PT字典转Mtool字典();
                    break;
                case "5":
                    机翻导入();
                    break;
                case "6":
                    FillTranz();
                    break;
                //case "7":
                //    暴力修正();
                //    break;
                case "7":
                    EraDictParser.二级菜单();
                    break;
                case "8":
                    ConvertToUtf8(directories);
                    break;
                case "9":
                    SingleParser();
                    break;
                case "10":
                    Settings();
                    break;
                case "11":
                    Process.Start("https://github.com/Future-R/ErbParserForTranslate");
                    break;
                case "999":
                    Test.Debug();
                    break;
                default:
                    Translator();
                    break;
            }

            Console.ReadKey();
            Console.Clear();
        }
    }

    private static void ConvertToUtf8(string[] directories)
    {
        var eraGameDirs = Tools.GetEraGamesDirectories(directories);
        if (eraGameDirs.Count == 0)
        {
            Console.WriteLine("未找到任何Era游戏目录！");
            Console.WriteLine($"请把era游戏目录拖到以下目录：{Environment.NewLine}{string.Join(Environment.NewLine, directories)}");
            return;
        }
        
        var appPath = Prompt.Select("请选择一个目录",eraGameDirs);
        Console.WriteLine($"正在转换{appPath}下的所有文件为UTF-8编码……");
        var files = Directory.GetFiles(appPath, "*.*", SearchOption.AllDirectories).Where(file=>file.EndsWith(".erb", StringComparison.OrdinalIgnoreCase) ||
                                                                                                file.EndsWith(".erh", StringComparison.OrdinalIgnoreCase) ||
                                                                                                file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));
        Parallel.ForEach(files, filepath =>
        {
            var cdet =CharsetDetector.DetectFromFile(filepath);
            
            var resultDetected  = cdet.Detected;
            var encoding = Encoding.Default;
            if (resultDetected != null) {
                bool 日本人可能会用的编码 = false;
              
                switch (resultDetected.EncodingName)
                {
                    case "iso-2022-jp":
                    case "shift-jis":
                    case "euc-jp":
                    case "ascii":
                    case "utf-16le":
                    case "utf-16be":
                        日本人可能会用的编码 = true;
                        break;
                    default:
                        日本人可能会用的编码 = false;
                        break;
                }
                string warn = resultDetected.Confidence < 0.666 && !日本人可能会用的编码 ? "【警告】" : string.Empty;
                Console.WriteLine($"{warn}编码: {resultDetected.EncodingName}, 可信: {resultDetected.Confidence}, {Path.GetFileName(filepath)}");
                encoding = 日本人可能会用的编码 ? resultDetected.Encoding : Encoding.GetEncoding("Shift-JIS");
            }
             
            if (Equals(encoding, Encoding.UTF8) || Equals(encoding,Encoding.Default)) return;
            
            var content = File.ReadAllText(filepath, encoding);
            File.WriteAllText(filepath, content, Encoding.UTF8);
        });
        Console.WriteLine("转换完成！");
        Console.ReadKey();
    }
    static void Settings()
    {
        Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"));
        Console.WriteLine("请编辑打开的Config文件并保存，保存后才能生效。");
        Console.ReadKey();
        Configs.Init();
    }

    static void SingleParser()
    {
        string 文件 = Tools.ReadLine("请拖入需要提取字典的单个文件");
        string menuString =
@"请问该文件是什么类型：
[0] - ERB或ERH脚本
[1] - CSV目录下的CSV
[2] - Resource目录下的CSV
[3] - XML配置文件
[4] - 果然还是算了，让我返回主菜单吧";
        string command = Tools.ReadLine(menuString);
        Timer.Start();
        switch (command)
        {
            case "0":
                ERBParser erbp = new ERBParser();
                erbp.ParseFile(文件);
                erbp.WriteJson(文件, "");
                break;
            case "1":
                CSVParser csvp = new CSVParser();
                csvp.ParseFile(文件);
                csvp.WriteJson(文件, "");
                break;
            case "2":
                RESParser resp = new RESParser();
                resp.ParseFile(文件);
                resp.WriteJson(文件, "");
                break;
            case "3":
                XMLParser xmlp = new XMLParser();
                xmlp.ParseFile(文件);
                xmlp.WriteJson(文件, "");
                break;
            default:
                return;
        }
        Console.WriteLine($"已将PT字典生成到相同目录");
        Timer.Stop();
        //if (Configs.autoOpenFolder)
        //{
        //    Process.Start(Path.GetPathRoot(文件));
        //}
    }

    static void PT字典转Mtool字典()
    {
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        Tools.CleanDirectory(Path.Combine(baseDirectory, "CSV"));
        Tools.CleanDirectory(Path.Combine(baseDirectory, "ERB"));

        string menuString =
@"请问是完整导出还是仅导出未翻译部分：
[0] - 仅导出未翻译部分（减少机翻配额消耗）
[1] - 完完整整导出（方便迁移到其它字典工具）
[2] - 果然还是算了，让我返回主菜单吧";
        string command = Tools.ReadLine(menuString);
        bool 完整导出 = false;
        switch (command)
        {
            case "0":
                break;
            case "1":
                完整导出 = true;
                break;
            default:
                return;
        }

        string PT目录 = Tools.ReadLine("请拖入pt目录");
        var jsonFiles = Directory.GetFiles(PT目录, "*.json", SearchOption.AllDirectories);
        Timer.Start();
        foreach (var jsonFile in jsonFiles)
        {
            string json输入 = File.ReadAllText(jsonFile);
            // pt的json都是[起头的
            if (!json输入.StartsWith("["))
            {
                break;
            }
            JArray jsonArray = JArray.Parse(json输入);
            JObject 输出obj = new JObject();

            foreach (var jobj in jsonArray.ToObject<List<JObject>>())
            {
                string 原文 = jobj["original"].ToString();
                // 大于0表示已翻译
                if (jobj.ContainsKey("stage") && (int)jobj["stage"].ToObject(typeof(int)) > 0)
                {
                    if (完整导出)
                    {
                        输出obj[原文] = jobj["translation"].ToString();
                    }
                }
                else
                {
                    输出obj[原文] = 原文;
                }
            }
            string json输出 = JsonConvert.SerializeObject(输出obj, Formatting.Indented);

            // 获取相对路径
            var relativePath = Tools.GetrelativePath(jsonFile, PT目录);
            // 得到输出路径
            var targetFile = Path.Combine(baseDirectory, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
            File.WriteAllText(targetFile, json输出);
        }
        Timer.Stop();
        Console.WriteLine("已将生成的JSON放置在此程序目录下");
        if (Configs.autoOpenFolder)
        {
            Process.Start(baseDirectory);
        }
    }

    static void 机翻导入()
    {
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        Tools.CleanDirectory(Path.Combine(baseDirectory, "CSV"));
        Tools.CleanDirectory(Path.Combine(baseDirectory, "ERB"));

        string PT目录 = Tools.ReadLine("请拖入人力译文PT字典目录：");
        string[] PT文件名组 = Directory.GetFiles(PT目录, "*.json", SearchOption.AllDirectories);

        string MT目录 = Tools.ReadLine("请拖入MTool机翻字典目录（M字典文件名要和原PT字典完全一致，也兼容附加的_translated）：");
        string[] ERA文件名组 = Directory.GetFiles(MT目录, "*.json", SearchOption.AllDirectories);

        Timer.Start();
        // 把mtooljson提前读出来备好，免得每看到一个未翻译都要读一遍文件
        Dictionary<string, JObject> 机翻字典 = new Dictionary<string, JObject>();
        foreach (var erajson in ERA文件名组)
        {
            string 文件名 = Path.GetFileName(erajson);
            string json输入 = File.ReadAllText(erajson);
            if (json输入.StartsWith("{"))
            {
                JObject jobj = JObject.Parse(json输入);
                // 兼容末尾的_translated.
                机翻字典.Add(文件名.Replace("_translated.", "."), jobj);
            }
        }


        foreach (var jsonFile in PT文件名组)
        {
            string jsonContent = File.ReadAllText(jsonFile);
            // pt的json都是[起头的
            if (!jsonContent.StartsWith("["))
            {
                Console.WriteLine("【警告】可能错将Mtool字典当成PT字典导入！");
                break;
            }
            JArray jsonArray = JArray.Parse(jsonContent);

            string 文件名 = Path.GetFileName(jsonFile);

            if (!机翻字典.ContainsKey(文件名))
            {
                Console.WriteLine($"【警告】找不到名为{文件名}的机翻字典！");
                continue;
            }

            List<JObject> PTobj = jsonArray.ToObject<List<JObject>>();

            for (int i = 0; i < PTobj.Count; i++)
            {
                string 原文 = PTobj[i]["original"].ToString();
                if (!PTobj[i].ContainsKey("stage") || (int)PTobj[i]["stage"].ToObject(typeof(int)) == 0)
                {
                    // 如果未翻译，且机翻字典里有对应条目，则用机翻字典覆盖
                    if (机翻字典[文件名].ContainsKey(原文))
                    {
                        PTobj[i]["translation"] = 机翻字典[文件名][原文];
                        PTobj[i]["stage"] = 1;
                    }
                }
            }
            // 获取相对路径
            var relativePath = Tools.GetrelativePath(jsonFile, PT目录);
            // 得到输出路径
            var targetFile = Path.Combine(baseDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile));

            string json输出 = JsonConvert.SerializeObject(PTobj, Formatting.Indented);
            File.WriteAllText(targetFile, json输出);
        }
        Timer.Stop();
        Console.WriteLine("已生成到此应用根目录！");
        if (Configs.autoOpenFolder)
        {
            Process.Start(baseDirectory);
        }
    }

    // 虽然说要检查变量名，但具体要怎么给用户交互呢？
    // 不断弹出选项让用户输入序号还是？
    // 百分号和花括号要检查吗？
    // 还是做一个纯显示的检查，让用户自己去PZ上改呢？
    // 总之要先遍历所有字典到内存里
    // 然后开一个变量名字典
    // CSV直接扔进去
    // 遇到变量键值就trim掉引号，往里扔
    // 遇到成对的百分号和成对的花括号就trim掉百分号和花括号扔去解析（如果是嵌套就算了）
    // 啊不行，感觉行不通，放弃了，打游戏
    // 因为有拼接存在，放弃检查变量名，改成检查重复项
    static void FillTranz()
    {
        string directoryPath = Tools.ReadLine("请拖入PT字典目录（务必备份）：");
        // 获取目录中所有JSON文件
        var jsonFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories);

        Timer.Start();
        // 遍历所有JSON文件，得到待处理对象和已翻译词典
        Dictionary<string, JArray> 待处理对象 = new Dictionary<string, JArray>();
        Dictionary<string, string> 已翻译字典 = new Dictionary<string, string>();
        foreach (var jsonFile in jsonFiles)
        {
            string jsonContent = File.ReadAllText(jsonFile);
            JArray jsonArray = JArray.Parse(jsonContent);
            待处理对象.Add(jsonFile, jsonArray);

            // 将每个JObject添加到列表中
            foreach (JObject jobj in jsonArray.ToObject<List<JObject>>())
            {
                // 有疑问的词条也应该被批量翻译吗，只要也标注为有疑问就好？
                //if (jobj.ContainsKey("stage") && (int)jobj["stage"].ToObject(typeof(int)) > 0)
                if (jobj.ContainsKey("stage") && jobj["stage"].ToString() == "1")
                {
                    string 原文 = jobj["original"].ToString();
                    string 译文 = jobj["translation"].ToString();
                    if (已翻译字典.ContainsKey(原文))
                    {
                        if (已翻译字典[原文] != 译文)
                        {
                            Console.WriteLine($"【警告】“{原文}”被翻译为多个版本！");
                        }
                    }
                    else
                    {
                        if (原文.StartsWith("\"") && !译文.StartsWith("\""))
                        {
                            Console.WriteLine($"【警告】“{原文}”的引号似乎被忽略了！");
                        }
                        if (原文.EndsWith("\"") && !译文.EndsWith("\""))
                        {
                            Console.WriteLine($"【警告】“{原文}”的引号似乎被忽略了！");
                        }

                        if ((原文.Count(c => c == '%') >= 2) && (译文.Count(c => c == '%') < 2))
                        {
                            Console.WriteLine($"【警告】“{原文}”的百分号似乎缺失了！");
                        }

                        if (原文.Contains('{') && !译文.Contains('{'))
                        {
                            Console.WriteLine($"【警告】“{原文}”的左花括号似乎缺失了！");
                        }
                        if (原文.Contains('}') && !译文.Contains('}'))
                        {
                            Console.WriteLine($"【警告】“{原文}”的右花括号似乎缺失了！");
                        }
                        已翻译字典.Add(原文, 译文);
                    }
                }
            }
        }
        Timer.Stop();
        Console.WriteLine("字典读取完成！开始替换未翻译词条！");
        Timer.Start();
        foreach (var 条目 in 待处理对象)
        {
            bool 需要输出 = false;
            List<JObject> 新文件 = new List<JObject>();
            foreach (JObject jobj in 条目.Value.ToObject<List<JObject>>())
            {
                string 原文 = jobj["original"].ToString();
                if (jobj.ContainsKey("stage") && jobj["stage"].ToString() == "0")
                {
                    // 天麻临时特殊处理1
                    Match IMGSRC = Regex.Match(原文, $"^\"<img src='(.*?)'>\"$");
                    if (已翻译字典.ContainsKey(原文))
                    {
                        Console.WriteLine($"【翻译】{已翻译字典[原文]}");
                        jobj["translation"] = 已翻译字典[原文];
                        jobj["stage"] = 1;
                        需要输出 = true;
                    }
                    // 天麻临时特殊处理1
                    else if (IMGSRC.Success && 已翻译字典.ContainsKey(IMGSRC.Groups[1].Value))
                    {
                        Console.WriteLine($"【天麻】{已翻译字典[IMGSRC.Groups[1].Value]}");
                        jobj["translation"] = $"\"<img src='{已翻译字典[IMGSRC.Groups[1].Value]}'>\"";
                        jobj["stage"] = 1;
                        需要输出 = true;
                    }
                    // 引号括起的也要拿去和不括起的比较
                    else if (已翻译字典.ContainsKey(原文.Trim('"')))
                    {
                        Console.WriteLine($"【引号】{已翻译字典[原文.Trim('"')]}");
                        jobj["translation"] = $"\"{已翻译字典[原文.Trim('"')]}\"";
                        jobj["stage"] = 1;
                        需要输出 = true;
                    }
                    // 百分号括起的也要拿去和不括起的比较
                    else if (原文.StartsWith("%") && 原文.EndsWith("%") && 已翻译字典.ContainsKey(原文.Trim('%')))
                    {
                        Console.WriteLine($"【百分】{已翻译字典[原文.Trim('%')]}");
                        jobj["translation"] = $"%{已翻译字典[原文.Trim('%')]}%";
                        jobj["stage"] = 1;
                        需要输出 = true;
                    }
                    // 天麻临时特殊处理2
                    else if (原文.StartsWith("IMGNO_") && 已翻译字典.ContainsKey(原文.Substring(6)))
                    {
                        Console.WriteLine($"【天麻】{已翻译字典[原文.Substring(6)]}");
                        jobj["translation"] = $"IMGNO_{已翻译字典[原文.Substring(6)]}";
                        jobj["stage"] = 1;
                        需要输出 = true;
                    }
                }
                新文件.Add(jobj);
            }
            if (需要输出)
            {
                string jsonContent = JsonConvert.SerializeObject(新文件, Formatting.Indented);
                File.WriteAllText(条目.Key, jsonContent);
            }
        }
        Timer.Stop();
        if (Configs.autoOpenFolder)
        {
            Process.Start(directoryPath);
        }
    }
    static void 自动修正(string 游戏目录, string 字典根目录)
    {
        Timer.Start();
        string[] 游戏目录下所有文件 = Directory.GetFiles(游戏目录, "*.*", SearchOption.AllDirectories);

        // 筛选出指定类型的文件，为了安全只修正ERB和ERH，不修正CSV。如果想依靠ERB/ERH填充CSV，请依靠查重功能
        List<string> 待汉化文件 = new List<string>();
        foreach (string 文件 in 游戏目录下所有文件)
        {
            if (文件.EndsWith(".erb", StringComparison.OrdinalIgnoreCase) ||
                文件.EndsWith(".erh", StringComparison.OrdinalIgnoreCase))
            {
                待汉化文件.Add(文件);
            }
        }

        List<string> 存在的修正字典 = new List<string>();
        string CSV字典根目录 = Path.Combine(字典根目录, "CSV");
        string[] CSV目录下的一级字典 = Directory.GetFiles(CSV字典根目录);

        foreach (string 参考文件 in Configs.autoReplaceRefer)
        {
            // 检查文件是否存在于目录中
            foreach (string 字典 in CSV目录下的一级字典)
            {
                if (Path.GetFileNameWithoutExtension(字典).Equals(参考文件, StringComparison.OrdinalIgnoreCase))
                {
                    存在的修正字典.Add(Path.Combine(CSV字典根目录, Path.GetFileName(字典)));
                    break;
                }
            }
        }

        StringBuilder pt输入 = new StringBuilder();
        foreach (var 字典 in 存在的修正字典)
        {
            // AppendLine自带换行符，但还要去掉json数组头尾的方括号，尾巴还要补逗号
            pt输入.AppendLine(File.ReadAllText(字典).TrimStart('[').TrimEnd(']'));
            pt输入.Append(',');
        }
        // 合并后要去除尾逗号，再加回方括号
        string 合并后的字符串 = "[" + pt输入.ToString().TrimEnd(',') + "]";
        JArray 修正字典 = JArray.Parse(合并后的字符串);

        Console.WriteLine("根据CPU性能与磁盘读写速度，修正过程可能会长达1秒~60秒，请勿中止程序！");
        Parallel.ForEach(待汉化文件, (文件名) =>
        {
            string 待处理文本 = File.ReadAllText(文件名);
            待处理文本 = Tools.RegexReplace(待处理文本, 修正字典);
            File.WriteAllText(文件名, 待处理文本, Configs.fileEncoding);
        });

        Timer.Stop();
        Console.WriteLine("自动修正完毕！");
    }

    static void 暴力修正()
    {
        // 获取指定目录下的所有文件
        string gameDirectory = Tools.ReadLine("请拖入游戏根目录：（做好备份）");
        string[] files = Directory.GetFiles(gameDirectory, "*.*", SearchOption.AllDirectories);

        // 筛选出指定类型的文件，为了安全只修正ERB和ERH，不修正CSV。如果想依靠ERB/ERH填充CSV，请依靠查重功能
        List<string> 文件名List = new List<string>();
        foreach (string file in files)
        {
            if (file.EndsWith(".erb", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith(".erh", StringComparison.OrdinalIgnoreCase))
            {
                文件名List.Add(file);
            }
        }
        string[] pt文件名组 = Directory.GetFiles(Tools.ReadLine("请拖入暴力修正字典目录（会合并多个字典）："), "*json", SearchOption.AllDirectories);

        Timer.Start();

        StringBuilder pt输入 = new StringBuilder();
        foreach (var item in pt文件名组)
        {
            // AppendLine自带换行符，但还要去掉json数组头尾的方括号，尾巴还要补逗号
            pt输入.AppendLine(File.ReadAllText(item).TrimStart('[').TrimEnd(']'));
            pt输入.Append(',');
        }
        // 合并后要去除尾逗号，再加回方括号
        string 合并后的字符串 = "[" + pt输入.ToString().TrimEnd(',') + "]";
        JArray 修正字典 = JArray.Parse(合并后的字符串);
        Timer.Stop();
        Console.WriteLine("字典翻译导入完成！正在全局替换，请稍候……");

        Timer.Start();
        foreach (var 文件名 in 文件名List)
        {
            string 待处理文本 = File.ReadAllText(文件名);
            待处理文本 = Tools.RegexReplace(待处理文本, 修正字典);
            File.WriteAllText(文件名, 待处理文本, Configs.fileEncoding);
        }
        Timer.Stop();
        Console.WriteLine("替换完毕！");

    }

    /// <summary>
    /// 用字典汉化游戏
    /// </summary>
    static void Translator()
    {
        string gameDirectory = Tools.ReadLine("请拖入需要汉化的游戏根目录（请做好备份）：");
        string transFileDirectory = Tools.ReadLine("请拖入放置CSV和ERB目录的译文目录：");

        Timer.Start();

        // 遍历所有译文JSON
        Parallel.ForEach(Directory.GetFiles(transFileDirectory, "*.json", SearchOption.AllDirectories), (jsonFile) =>
        {
            // 获取相对路径
            var relativePath = Tools.GetrelativePath(jsonFile, transFileDirectory).ToLower();
            bool isCSV = relativePath.StartsWith("csv");
            bool isIMG = relativePath.StartsWith("resources");
            bool isXML = relativePath.EndsWith(".xml.json");
            bool fileExist = false;
            // 得到输出路径
            var targetPath = Path.Combine(gameDirectory, relativePath);

            // 考虑到有erb、erh同名的情况
            List<string> targetFiles = new List<string>();

            
            // 专门处理FL的XML文件
            if (isXML)
            {
                string newFile = targetPath.Remove(targetPath.Length - 4);
                if (File.Exists(newFile))
                {
                    targetFiles.Add(newFile);
                    fileExist = true;
                }
            }
            else
            {
                // 遍历所有可能的扩展，后悔之前多手把扩展截掉了，现在想改稍微有点麻烦
                foreach (var ext in Configs.extensions)
                {
                    string newFile = Path.ChangeExtension(targetPath, ext);
                    if (File.Exists(newFile))
                    {
                        targetFiles.Add(newFile);
                        fileExist = true;
                    }
                }
            }
            
            // 如果目标脚本不存在，跳过这一条并报错
            if (!fileExist)
            {
                if (isIMG || (isCSV))
                {
                    if (!targetPath.Contains("修正字典"))
                    {
                        Console.WriteLine($"【错误】：没找到{targetPath}.CSV！");
                    }
                }
                else if (isXML)
                {
                    Console.WriteLine($"【错误】：没找到{targetPath}的XML配置！");
                }
                else
                {
                    Console.WriteLine($"【错误】：没找到{targetPath}的ERB脚本！");
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
                    // 读取pt脚本
                    JArray jsonArray = JArray.Parse(ptJsonContent);

                    foreach (var item in targetFiles)
                    {
                        // 读取游戏脚本
                        string scriptContent = File.ReadAllText(item, Configs.fileEncoding);
                        if (!isIMG)
                        {
                            // 使用字典替换原文
                            scriptContent = Tools.RegexReplace(scriptContent, jsonArray);
                        }
                        else
                        {
                            // 图像资源配置CSV采用特殊方式翻译
                            scriptContent = Tools.TransResConfig(scriptContent, jsonArray);
                        }

                        // 覆盖写入
                        File.WriteAllText(item, scriptContent, Configs.fileEncoding);
                    }
                }
            }
        });

        Timer.Stop();

        Console.WriteLine("翻译已完成！");
        if (Configs.autoReplace)
        {
            Console.WriteLine("检查到变量自动修正已启用，按任意键开始执行修正任务！");
            Console.WriteLine("自动修正会参考部分CSV变量汉化，去修正ERB中未翻译的内插变量。");
            Console.WriteLine("该功能在翻译前中期能有效抑制变量翻译带来的错误。");
            Console.WriteLine("如果不想修正，也可以在这一步直接关闭程序并且调整设置。");
            Console.ReadKey();

            自动修正(gameDirectory, transFileDirectory);
        }
        if (Configs.autoOpenFolder)
        {
            Process.Start(gameDirectory);
        }
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
        Tools.CleanDirectory(Path.Combine(appPath, "CSV"));
        Tools.CleanDirectory(Path.Combine(appPath, "ERB"));
        Tools.CleanDirectory(Path.Combine(appPath, "resources"));

        string mergePath = merge ? Tools.ReadLine("请拖入已经汉化的游戏根目录（作为翻译参考）：") : string.Empty;

        string path = Tools.ReadLine("请拖入需要汉化的游戏根目录：");

        string csvDirectory = Path.Combine(path, "CSV");
        string erbDirectory = Path.Combine(path, "ERB");
        string resDirectory = Path.Combine(path, "resources");
        string xmlDirectory = Path.Combine(path, "XML");

        Console.WriteLine("开始提取字典，根据CPU性能与磁盘读写速度这可能需要2秒~60秒，请稍候……");

        Timer.Start();

        if (Directory.Exists(csvDirectory))
        {
            // 获取所有csv文件
            var csvNames = Directory.GetFiles(csvDirectory, "*.csv", SearchOption.AllDirectories);
            Parallel.ForEach(csvNames, csvName =>
            {
                // 获取相对路径
                var relativePath = Tools.GetrelativePath(csvName, path);
                // 得到输出路径
                var targetFile = Path.Combine(appPath, relativePath);
                // 解析CSV
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
            Console.WriteLine($"【警告】找不到CSV目录: {csvDirectory}！");
            Console.ReadKey();
        }
        if (Directory.Exists(erbDirectory))
        {
            // 获取所有erb和erh文件
            // 这里用一个扭曲的方法来处理ERB和ERH同名的情况，主要是为了维护正在翻译中的项目的键值
            // 也就是同名时，把ERH从列表中删掉，然后在具体ErbParser的时候，尝试寻找同目录的ERH文件，如果有，把ERH拼接在ERB前面
            var erbNames = Directory.EnumerateFiles(erbDirectory, "*.*", SearchOption.AllDirectories)
            .Where(file => erbExtensions.Any(x => file.EndsWith(x, StringComparison.OrdinalIgnoreCase)))

            // 搞复杂了，只要在循环的时候，后缀如果是erh，检查erbNames是否包含同名erb，有的话就跳过，完事！

            //// 首先，创建一个包含所有文件名（不包含扩展名）和其对应的完整文件路径的字典
            //.GroupBy(file => Path.GetFileNameWithoutExtension(file), file => file)
            //// 对于每个分组，选择扩展名为.erb的文件，如果有的话
            //.Select(group => group.FirstOrDefault(file => file.EndsWith(".ERB") || file.EndsWith(".erb")))
            //// 过滤掉为null的项，即那些只包含.erh文件的组
            //.Where(file => file != null)
            ;

            Parallel.ForEach(erbNames, erbName =>
            {
                // 跳过同名的ERH，最后会在同名ERB里拼接，所以不会漏掉
                char nameLastChar = erbName.Last();
                bool isERH = nameLastChar == 'h' || nameLastChar == 'H';
                if (isERH && File.Exists(Path.ChangeExtension(erbName, ".erb")))
                {
                    return;
                }
                // 获取相对路径
                var relativePath = Tools.GetrelativePath(erbName, path);
                // 得到输出路径
                var targetFile = Path.Combine(appPath, relativePath);
                // 解析ERB
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
                        //parser.DebugPrint();
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
            Console.WriteLine($"【警告】找不到ERB目录: {erbDirectory}");
            Console.ReadKey();
        }
        if (Directory.Exists(resDirectory))
        {
            // 获取所有csv文件
            var resNames = Directory.GetFiles(resDirectory, "*.csv", SearchOption.AllDirectories);
            Parallel.ForEach(resNames, resName =>
            {
                // 获取相对路径
                var relativePath = Tools.GetrelativePath(resName, path);
                // 得到输出路径
                var targetFile = Path.Combine(appPath, relativePath);
                // 解析CSV
                RESParser parser = new RESParser();
                parser.ParseFile(resName);

                // 输出Json
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                if (merge)
                {
                    var referencePath = Path.Combine(mergePath, relativePath);
                    if (File.Exists(referencePath))
                    {
                        RESParser referenceParser = new RESParser();
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
        if (Directory.Exists(xmlDirectory))
        {
            // 获取所有xml文件
            List<string> xmlNames = Directory.GetFiles(xmlDirectory, "*.xml", SearchOption.AllDirectories).ToList();
            xmlNames.AddRange(Directory.GetFiles(erbDirectory, "*.xml", SearchOption.AllDirectories));

            Parallel.ForEach(xmlNames, xmlName =>
            {
                // 获取相对路径
                var relativePath = Tools.GetrelativePath(xmlName, path);
                // 得到输出路径
                var targetFile = Path.Combine(appPath, relativePath);
                // 解析XML
                XMLParser parser = new XMLParser();
                parser.ParseFile(xmlName);

                // 输出Json
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                if (merge)
                {
                    var referencePath = Path.Combine(mergePath, relativePath);
                    if (File.Exists(referencePath))
                    {
                        XMLParser referenceParser = new XMLParser();
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

        Timer.Stop();
        Console.WriteLine("已将生成的JSON放置在此程序目录下");
        if (Configs.autoOpenFolder)
        {
            Process.Start(appPath);
        }
    }

    /// <summary>
    /// <b>更新版本</b>
    /// <br>需求是：</br>
    /// <br>1.旧的键值要维持不变（删除或隐藏旧条目不能改变其他条目键值）</br>
    /// <br>2.新的键值不能重复（需要取旧版最大键值+1）</br>
    /// <br>3.新的条目最好顺序不要在最后，而是在正确的上下文之间</br>
    /// </summary>
    static void versionUpdate(string appPath)
    {
        // 清理上次生成的文件，必须放前面不然删得慢了碰到后面的多线程会报错
        Tools.CleanDirectory(Path.Combine(appPath, "CSV"));
        Tools.CleanDirectory(Path.Combine(appPath, "ERB"));
        Tools.CleanDirectory(Path.Combine(appPath, "resources"));

        string oldPath = Tools.ReadLine("请拖入放置CSV和ERB目录的译文目录：");
        string[] oldFileArray = Directory.GetFiles(oldPath, "*.json", SearchOption.AllDirectories);
        Dictionary<string, JArray> oldDict = new Dictionary<string, JArray>();
        foreach (var oldFile in oldFileArray)
        {
            string oldTrans = File.ReadAllText(oldFile);
            // pt的json都是[起头的
            if (!oldTrans.StartsWith("["))
            {
                break;
            }
            JArray jsonArray = JArray.Parse(oldTrans);
            // 以相对路径作为键值，先不考虑同目录下同名文件的情况，出事了再改
            string relativePath = Tools.GetrelativePath(oldFile, oldPath);
            oldDict.Add(relativePath, jsonArray);
        }
        string newPath = Tools.ReadLine($"已导入{oldDict.Count}个文件。\n请拖入新版本游戏目录：");
        string csvDirectory = Path.Combine(newPath, "CSV");
        string erbDirectory = Path.Combine(newPath, "ERB");
        string resDirectory = Path.Combine(newPath, "resources");

        Timer.Start();

        // 这段代码有空得封装一下，现在先跑起来
        if (Directory.Exists(csvDirectory))
        {
            // 获取所有csv文件
            var csvNames = Directory.GetFiles(csvDirectory, "*.csv", SearchOption.AllDirectories);
            Parallel.ForEach(csvNames, csvName =>
            {
                // 获取相对路径
                var relativePath = Tools.GetrelativePath(csvName, newPath);
                // 得到输出路径
                var targetFile = Path.Combine(appPath, relativePath);
                // 解析CSV
                CSVParser parser = new CSVParser();
                parser.ParseFile(csvName);

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                string fileKey = Path.ChangeExtension(relativePath, ".json");
                // 如果译文字典里已经存在相应的文件，取作参考，否则正常输出
                if (oldDict.ContainsKey(fileKey))
                {
                    List<JObject> referenceObjects = oldDict[fileKey].ToObject<List<JObject>>();

                    int maxKey = int.MinValue;
                    foreach (var jObject in referenceObjects)
                    {
                        string key = jObject["key"].ToString();
                        // 获取键值最后的5位序号
                        int num = int.Parse(Tools.lastNum.Match(key).Value);
                        if (num > maxKey)
                        {
                            maxKey = num;
                        }
                    }
                    // 得到起始序号
                    int index = maxKey + 1;
                    var csvList = parser.GetList().Distinct();
                    List<JObject> PTJsonObjList = new List<JObject>();
                    foreach (string text in csvList)
                    {
                        JObject targetObject = referenceObjects.FirstOrDefault(j => j["original"].ToString() == text);
                        // 找到对应条目，就直接用参考覆盖
                        if (targetObject != null)
                        {
                            PTJsonObjList.Add(targetObject);
                        }
                        // 否则，新建一个index序号的新条目
                        else
                        {
                            PTJsonObjList.Add(new JObject
                            {
                                ["key"] = Path.ChangeExtension(relativePath, "") + index.ToString().PadLeft(5, '0'),
                                ["original"] = text,
                                ["translation"] = ""
                            });
                            Console.WriteLine($"[CSV]{text}");
                            // 仅在成功添加新条目时，才自增序号
                            index++;
                        }
                    }

                    if (PTJsonObjList.Count() > 0)
                    {
                        string jsonContent = JsonConvert.SerializeObject(PTJsonObjList, Formatting.Indented);
                        File.WriteAllText(Path.ChangeExtension(targetFile, ".json"), jsonContent);
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
                var relativePath = Tools.GetrelativePath(erbName, newPath);
                // 得到输出路径
                var targetFile = Path.Combine(appPath, relativePath);
                // 解析ERB
                ERBParser parser = new ERBParser();
                parser.ParseFile(erbName);

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                string fileKey = Path.ChangeExtension(relativePath, ".json");
                if (oldDict.ContainsKey(fileKey))
                {
                    List<JObject> referenceObjects = oldDict[fileKey].ToObject<List<JObject>>();

                    int maxKey = int.MinValue;
                    foreach (var jObject in referenceObjects)
                    {
                        string key = jObject["key"].ToString();
                        // 获取键值最后的5位序号
                        int num = int.Parse(Tools.lastNum.Match(key).Value);
                        if (num > maxKey)
                        {
                            maxKey = num;
                        }
                    }
                    // 得到起始序号
                    int index = maxKey + 1;
                    // 合并变量名和长文本
                    var tuple = parser.GetListTuple();
                    List<(string, string)> varNameList = parser.VarNameListFilter(tuple.name.lines);
                    List<(string, string)> textList = parser.TextListFilter(tuple.text.lines).ToList();

                    List<JObject> PTJsonObjList = new List<JObject>();
                    // 处理变量名
                    foreach ((string original, string context) in varNameList)
                    {
                        JObject targetObject = referenceObjects.FirstOrDefault(j => j["original"].ToString() == original);
                        // 找到对应条目，就直接用参考覆盖
                        if (targetObject != null)
                        {
                            // 版本过渡的临时处理
                            if (!targetObject.ContainsKey("context"))
                            {
                                targetObject.Add("context", context);
                            }
                            PTJsonObjList.Add(targetObject);
                        }
                        // 否则，新建一个index序号的新条目
                        else
                        {
                            PTJsonObjList.Add(new JObject
                            {
                                ["key"] = new StringBuilder("变量")
                                .Append(Path.ChangeExtension(relativePath, ""))
                                .Append(index.ToString().PadLeft(5, '0'))
                                .ToString(),
                                ["original"] = original,
                                ["translation"] = "",
                                ["context"] = context
                            });
                            Console.WriteLine($"[变量]{original}");
                            // 仅在成功添加新条目时，才自增序号
                            index++;
                        }
                    }
                    // 处理长文本
                    foreach ((string original, string context) in textList)
                    {
                        JObject targetObject = referenceObjects.FirstOrDefault(j => j["original"].ToString() == original);
                        // 找到对应条目，就直接用参考覆盖
                        if (targetObject != null)
                        {
                            // 版本过渡的临时处理
                            if (!targetObject.ContainsKey("context"))
                            {
                                targetObject.Add("context", context);
                            }
                            PTJsonObjList.Add(targetObject);
                        }
                        // 否则，新建一个index序号的新条目
                        else
                        {
                            PTJsonObjList.Add(new JObject
                            {
                                ["key"] = new StringBuilder("文本")
                                .Append(Path.ChangeExtension(relativePath, ""))
                                .Append(index.ToString().PadLeft(5, '0'))
                                .ToString(),
                                ["original"] = original,
                                ["translation"] = "",
                                ["context"] = context
                            });
                            Console.WriteLine($"[文本]{original}");
                            // 仅在成功添加新条目时，才自增序号
                            index++;
                        }
                    }

                    if (PTJsonObjList.Count() > 0)
                    {
                        string jsonContent = JsonConvert.SerializeObject(PTJsonObjList, Formatting.Indented);
                        File.WriteAllText(Path.ChangeExtension(targetFile, ".json"), jsonContent);
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
        if (Directory.Exists(resDirectory))
        {
            // 获取所有res文件
            var resNames = Directory.GetFiles(resDirectory, "*.csv", SearchOption.AllDirectories);
            Parallel.ForEach(resNames, resName =>
            {
                // 获取相对路径
                var relativePath = Tools.GetrelativePath(resName, newPath);
                // 得到输出路径
                var targetFile = Path.Combine(appPath, relativePath);
                // 解析CSV
                RESParser parser = new RESParser();
                parser.ParseFile(resName);

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                string fileKey = Path.ChangeExtension(relativePath, ".json");
                // 如果译文字典里已经存在相应的文件，取作参考，否则正常输出
                if (oldDict.ContainsKey(fileKey))
                {
                    List<JObject> referenceObjects = oldDict[fileKey].ToObject<List<JObject>>();

                    int maxKey = int.MinValue;
                    foreach (var jObject in referenceObjects)
                    {
                        string key = jObject["key"].ToString();
                        // 获取键值最后的5位序号
                        int num = int.Parse(Tools.lastNum.Match(key).Value);
                        if (num > maxKey)
                        {
                            maxKey = num;
                        }
                    }
                    // 得到起始序号
                    int index = maxKey + 1;
                    var resList = parser.GetList().Distinct();
                    List<JObject> PTJsonObjList = new List<JObject>();
                    foreach (string text in resList)
                    {
                        JObject targetObject = referenceObjects.FirstOrDefault(j => j["original"].ToString() == text);
                        // 找到对应条目，就直接用参考覆盖
                        if (targetObject != null)
                        {
                            PTJsonObjList.Add(targetObject);
                        }
                        // 否则，新建一个index序号的新条目
                        else
                        {
                            PTJsonObjList.Add(new JObject
                            {
                                ["key"] = Path.ChangeExtension(relativePath, "") + index.ToString().PadLeft(5, '0'),
                                ["original"] = text,
                                ["translation"] = ""
                            });
                            Console.WriteLine($"[IMG]{text}");
                            // 仅在成功添加新条目时，才自增序号
                            index++;
                        }
                    }

                    if (PTJsonObjList.Count() > 0)
                    {
                        string jsonContent = JsonConvert.SerializeObject(PTJsonObjList, Formatting.Indented);
                        File.WriteAllText(Path.ChangeExtension(targetFile, ".json"), jsonContent);
                    }
                }
                else
                {
                    parser.WriteJson(targetFile, relativePath);
                }
            });
        }

        Timer.Stop();
        if (Configs.autoOpenFolder)
        {
            Process.Start(appPath);
        }
    }
}

