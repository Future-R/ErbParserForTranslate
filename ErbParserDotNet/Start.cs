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
using ErbParserDotNet;
using System.Threading;

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
[10] - 获取字典差异（方便上传Paratranz）
[11] - 设置
[12] - 访问项目主页";
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
                    VersionUpdate(appPath);
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
                    ExtractModifiedFiles();
                    break;
                case "11":
                    Settings();
                    break;
                case "12":
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
                                                                                                file.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
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
                string 替换后文件名 = 文件名.Replace("_translated.", ".");
                if (机翻字典.ContainsKey(替换后文件名))
                {
                    Console.WriteLine($"{替换后文件名}已存在，无法再次录入");
                }
                else
                {
                    机翻字典.Add(替换后文件名, jobj);
                }
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
                    string 原文 = jobj["original"].ToString().Trim();
                    string 译文 = jobj["translation"].ToString().Trim();
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
                    //// 天麻临时特殊处理1
                    //Match IMGSRC = Regex.Match(原文, $"^\"<img src='(.*?)'>\"$");
                    if (已翻译字典.ContainsKey(原文))
                    {
                        Console.WriteLine($"【翻译】{已翻译字典[原文]}");
                        jobj["translation"] = 已翻译字典[原文];
                        jobj["stage"] = 1;
                        需要输出 = true;
                    }
                    //// 天麻临时特殊处理1
                    //else if (IMGSRC.Success && 已翻译字典.ContainsKey(IMGSRC.Groups[1].Value))
                    //{
                    //    Console.WriteLine($"【天麻】{已翻译字典[IMGSRC.Groups[1].Value]}");
                    //    jobj["translation"] = $"\"<img src='{已翻译字典[IMGSRC.Groups[1].Value]}'>\"";
                    //    jobj["stage"] = 1;
                    //    需要输出 = true;
                    //}
                    // 引号括起的也要拿去和不括起的比较
                    else if (原文.Trim().StartsWith("\"") || 原文.Trim().EndsWith("\""))
                    {
                        string 处理后原文 = 原文.Trim().Trim('"');

                        if (已翻译字典.ContainsKey(处理后原文))
                        {
                            char? 前引号 = null;
                            char? 后引号 = null;
                            if (原文.Trim().StartsWith("\""))
                            {
                                前引号 = '"';
                            }
                            if (原文.Trim().EndsWith("\""))
                            {
                                后引号 = '"';
                            }

                            Console.WriteLine($"【括号】{已翻译字典[处理后原文]}");
                            jobj["translation"] = $"{前引号}{已翻译字典[处理后原文]}{后引号}";
                            jobj["stage"] = 1;
                            需要输出 = true;
                        }
                    }
                    // 处理残留括号
                    else if (原文.Trim().StartsWith("(\"") || 原文.Trim().EndsWith("\")"))
                    {
                        string 处理后原文 = 原文.Trim().TrimStart('(').TrimEnd(')').Trim('"');
                        if (已翻译字典.ContainsKey(处理后原文))
                        {
                            char? 前括号 = null;
                            char? 后括号 = null;
                            char? 前引号 = null;
                            char? 后引号 = null;
                            if (原文.Trim().StartsWith("(\""))
                            {
                                前括号 = '(';
                                前引号 = '"';
                            }
                            else if(原文.Trim().StartsWith("("))
                            {
                                前括号 = '(';
                            }
                            if (原文.Trim().EndsWith("\")"))
                            {
                                后括号 = ')';
                                后引号 = '"';
                            }
                            else if (原文.Trim().StartsWith(")"))
                            {
                                后括号 = ')';
                            }

                            Console.WriteLine($"【括号】{已翻译字典[处理后原文]}");
                            jobj["translation"] = $"{前括号}{前引号}{已翻译字典[处理后原文]}{后引号}{后括号}";
                            jobj["stage"] = 1;
                            需要输出 = true;
                        }
                    }
                    // 百分号括起的也要拿去和不括起的比较
                    else if (原文.StartsWith("%") && 原文.EndsWith("%") && 已翻译字典.ContainsKey(原文.Trim('%')))
                    {
                        Console.WriteLine($"【百分】{已翻译字典[原文.Trim('%')]}");
                        jobj["translation"] = $"%{已翻译字典[原文.Trim('%')]}%";
                        jobj["stage"] = 1;
                        需要输出 = true;
                    }
                    //// 天麻临时特殊处理2
                    //else if (原文.StartsWith("IMGNO_") && 已翻译字典.ContainsKey(原文.Substring(6)))
                    //{
                    //    Console.WriteLine($"【天麻】{已翻译字典[原文.Substring(6)]}");
                    //    jobj["translation"] = $"IMGNO_{已翻译字典[原文.Substring(6)]}";
                    //    jobj["stage"] = 1;
                    //    需要输出 = true;
                    //}
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
                文件.EndsWith(".erh", StringComparison.OrdinalIgnoreCase) ||
                文件.EndsWith(".erd", StringComparison.OrdinalIgnoreCase))
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
                    if (!targetPath.Contains("修正字典") && !targetPath.Contains("erd字典"))
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
        // 重命名ERD文件
        Tools.RenameErdFiles(transFileDirectory, gameDirectory);
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

            // 获取所有erd文件
            var erdNames = Directory.GetFiles(erbDirectory, "*.ERD", SearchOption.AllDirectories);
            if (erdNames.Length > 0) PZJson.输出ERD字典(erdNames);
            Parallel.ForEach(erdNames, erdName =>
            {
                // 获取相对路径
                var relativePath = Tools.GetrelativePath(erdName, path);
                // 得到输出路径
                var targetFile = Path.Combine(appPath, relativePath);
                // 解析ERD
                CSVParser parser = new CSVParser();
                parser.ParseFile(erdName);

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

    #region VersionUpdate Refactored

    /// <summary>
    /// <b>更新版本</b>
    /// <br>需求是：</br>
    /// <br>1.旧的键值要维持不变（删除或隐藏旧条目不能改变其他条目键值）</br>
    /// <br>2.新的键值不能重复（需要取旧版最大键值+1）</br>
    /// <br>3.新的条目最好顺序不要在最后，而是在正确的上下文之间</br>
    /// </summary>
    /// <param name="appPath">应用程序根目录，用于存放输出的字典文件。</param>
    private static void VersionUpdate(string appPath)
    {
        // 1. 准备工作：清理输出目录
        Tools.CleanDirectory(Path.Combine(appPath, "CSV"));
        Tools.CleanDirectory(Path.Combine(appPath, "ERB"));
        Tools.CleanDirectory(Path.Combine(appPath, "resources"));

        // 2. 加载旧版字典
        string oldDictPath = Tools.ReadLine("请拖入包含旧版译文的字典目录：");
        var oldJsonDict = LoadOldJsonDictionary(oldDictPath);
        if (oldJsonDict == null || oldJsonDict.Count == 0)
        {
            Console.WriteLine("未能加载任何旧版字典文件，操作中止。");
            return;
        }

        // 3. 获取新版游戏目录
        string newGamePath = Tools.ReadLine($"已导入 {oldJsonDict.Count} 个字典文件。\n请拖入新版本游戏目录：");

        Timer.Start();

        // 4. 定义并执行各类型文件的处理逻辑
        // 为不同类型的文件定义其专属的解析逻辑
        var fileProcessingTasks = new Action[]
        {
            () => ProcessDirectoryForUpdate(
                newGamePath, "CSV", "*.csv", appPath, oldJsonDict, (filePath) =>
                {
                    var parser = new CSVParser();
                    parser.ParseFile(filePath);
                    return parser.GetList().Distinct().Select(item => new TranslationEntry { Original = item, Type = "文本" }).ToList();
                }),
            () => ProcessDirectoryForUpdate(
                newGamePath, "ERB", "*.*", appPath, oldJsonDict, (filePath) =>
                {
                    // 跳过同名的 ERH 文件，因为它们会在解析 ERB 时被合并
                    if (IsDuplicateErh(filePath)) return new List<TranslationEntry>();

                    var parser = new ERBParser();
                    parser.ParseFile(filePath);
                    var (nameList, textList) = parser.GetListTuple();

                    var entries = new List<TranslationEntry>();
                    entries.AddRange(parser.VarNameListFilter(nameList.lines)
                        .Select(item => new TranslationEntry { Original = item.Item1, Context = item.Item2, Type = "变量" }));
                    entries.AddRange(parser.TextListFilter(textList.lines)
                        .Select(item => new TranslationEntry { Original = item.Item1, Context = item.Item2, Type = "文本" }));
                    return entries;
                }, erbExtensions), // 传入ERB/ERH后缀进行过滤
            () => ProcessDirectoryForUpdate(
                newGamePath, "resources", "*.csv", appPath, oldJsonDict, (filePath) =>
                {
                    var parser = new RESParser();
                    parser.ParseFile(filePath);
                    return parser.GetList().Distinct().Select(item => new TranslationEntry { Original = item, Type = "文本" }).ToList();
                }),
            () => ProcessErdFilesForUpdate(newGamePath, appPath, oldJsonDict)
        };

        // 并行处理所有任务
        Parallel.Invoke(fileProcessingTasks);

        Timer.Stop();
        if (Configs.autoOpenFolder)
        {
            Process.Start(appPath);
        }
    }

    /// <summary>
    /// （ERD专属处理逻辑）扫描ERB目录下所有.erd文件，将它们的文件名作为条目更新到单一的ERD字典中。
    /// </summary>
    /// <param name="newGamePath">新版游戏根目录。</param>
    /// <param name="appPath">输出目录。</param>
    /// <param name="oldDict">已加载的旧版字典。</param>
    private static void ProcessErdFilesForUpdate(
        string newGamePath,
        string appPath,
        Dictionary<string, JArray> oldDict)
    {
        var erdDirectoryPath = Path.Combine(newGamePath, "ERB");
        if (!Directory.Exists(erdDirectoryPath))
        {
            // 如果ERB目录不存在，就没什么可做的了。
            return;
        }

        // 1. 查找新版游戏中所有的 .erd 文件
        var erdFiles = Directory.EnumerateFiles(erdDirectoryPath, "*.erd", SearchOption.AllDirectories).ToList();
        if (!erdFiles.Any())
        {
            // 如果没有找到 .erd 文件，则直接返回。
            return;
        }

        Console.WriteLine($"发现 {erdFiles.Count} 个 .erd 文件，正在处理文件名...");

        // 2. 将每个 .erd 文件的文件名（不含扩展名）转换为标准化的 TranslationEntry
        var newEntries = erdFiles.Select(filePath => new TranslationEntry
        {
            // 核心逻辑：Original 值是文件名本身
            Original = Path.GetFileNameWithoutExtension(filePath),
            Type = "ERD文件名"
        }).ToList();

        // 3. 定义输出字典的相对路径和绝对路径
        // 遵循参考代码的约定，将ERD字典放在CSV目录下
        const string erdDictRelativePath = @"CSV\ERD字典.json";
        var targetFile = Path.Combine(appPath, erdDictRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetFile));

        // 4. 检查是否存在旧的 ERD 字典
        if (oldDict.TryGetValue(erdDictRelativePath, out JArray oldJsonArray))
        {
            // 4a. 如果存在，则合并新旧条目
            var finalJsonList = MergeEntries(newEntries, oldJsonArray, erdDictRelativePath);
            if (finalJsonList.Any())
            {
                string jsonContent = JsonConvert.SerializeObject(finalJsonList, Formatting.Indented);
                File.WriteAllText(targetFile, jsonContent);
            }
        }
        else
        {
            // 4b. 如果不存在，则根据新条目直接创建
            var newJsonObjects = CreateNewJsonObjects(newEntries, erdDictRelativePath);
            if (newJsonObjects.Any())
            {
                string jsonContent = JsonConvert.SerializeObject(newJsonObjects, Formatting.Indented);
                File.WriteAllText(targetFile, jsonContent);
            }
        }
    }


    /// <summary>
    /// 加载指定路径下的所有旧版PT字典文件到内存中。
    /// </summary>
    private static Dictionary<string, JArray> LoadOldJsonDictionary(string path)
    {
        if (!Directory.Exists(path))
        {
            Console.WriteLine($"【错误】目录不存在：{path}");
            return null;
        }
        var oldFileArray = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);
        var oldDict = new Dictionary<string, JArray>();
        foreach (var oldFile in oldFileArray)
        {
            try
            {
                string oldTrans = File.ReadAllText(oldFile);
                // PT字典总是以[开头
                if (oldTrans.Trim().StartsWith("["))
                {
                    JArray jsonArray = JArray.Parse(oldTrans);
                    string relativePath = Tools.GetrelativePath(oldFile, path);
                    oldDict[relativePath] = jsonArray;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"【警告】读取或解析旧字典失败: {Path.GetFileName(oldFile)} - {ex.Message}");
            }
        }
        return oldDict;
    }

    /// <summary>
    /// （核心处理逻辑）处理指定子目录下的所有文件，与旧字典合并后生成新字典。
    /// </summary>
    /// <param name="newGamePath">新版游戏根目录。</param>
    /// <param name="subDirectory">要处理的子目录名 (例如 "CSV", "ERB")。</param>
    /// <param name="searchPattern">文件搜索模式 (例如 "*.csv")。</param>
    /// <param name="appPath">输出目录。</param>
    /// <param name="oldDict">已加载的旧版字典。</param>
    /// <param name="parseAction">一个委托，接收文件路径并返回解析出的翻译条目列表。</param>
    /// <param name="allowedExtensions">（可选）用于过滤文件的后缀名数组。</param>
    private static void ProcessDirectoryForUpdate(
        string newGamePath,
        string subDirectory,
        string searchPattern,
        string appPath,
        Dictionary<string, JArray> oldDict,
        Func<string, List<TranslationEntry>> parseAction,
        string[] allowedExtensions = null)
    {
        var directoryPath = Path.Combine(newGamePath, subDirectory);
        if (!Directory.Exists(directoryPath))
        {
            Console.WriteLine($"【警告】找不到目录: {directoryPath}");
            return;
        }

        var files = Directory.EnumerateFiles(directoryPath, searchPattern, SearchOption.AllDirectories);
        if (allowedExtensions != null && allowedExtensions.Length > 0)
        {
            files = files.Where(file => allowedExtensions.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
        }

        Parallel.ForEach(files, filePath =>
        {
            var relativePath = Tools.GetrelativePath(filePath, newGamePath);
            var targetFile = Path.Combine(appPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile));

            // 1. 解析新文件，获取所有条目
            var newEntries = parseAction(filePath);
            if (!newEntries.Any()) return;

            // 2. 检查是否存在旧字典
            string fileKey = Path.ChangeExtension(relativePath, ".json");
            if (oldDict.TryGetValue(fileKey, out JArray oldJsonArray))
            {
                // 3a. 合并新旧条目
                var finalJsonList = MergeEntries(newEntries, oldJsonArray, relativePath);
                if (finalJsonList.Any())
                {
                    string jsonContent = JsonConvert.SerializeObject(finalJsonList, Formatting.Indented);
                    File.WriteAllText(Path.ChangeExtension(targetFile, ".json"), jsonContent);
                }
            }
            else
            {
                // 3b. 如果没有旧字典，则直接生成新字典
                var newJsonObjects = CreateNewJsonObjects(newEntries, relativePath);
                if (newJsonObjects.Any())
                {
                    string jsonContent = JsonConvert.SerializeObject(newJsonObjects, Formatting.Indented);
                    File.WriteAllText(Path.ChangeExtension(targetFile, ".json"), jsonContent);
                }
            }
        });
    }

    /// <summary>
    /// 将新解析出的条目与旧字典的JObject列表进行合并。
    /// </summary>
    /// <summary>
    /// 将新解析出的条目与旧字典的JObject列表进行合并（已修复重复键问题）。
    /// </summary>
    private static List<JObject> MergeEntries(List<TranslationEntry> newEntries, JArray oldJsonArray, string relativePath)
    {
        var finalJsonList = new List<JObject>();
        var referenceObjects = oldJsonArray.ToObject<List<JObject>>();

        // 使用 TryAdd 来处理潜在的重复键，只保留第一个遇到的条目
        var oldEntriesDict = new Dictionary<string, JObject>();
        foreach (var j in referenceObjects)
        {
            var original = j["original"]?.ToString();
            if (original != null && !oldEntriesDict.ContainsKey(original))
            {
                oldEntriesDict.Add(original, j);
            }
        }

        int maxKeyNum = -1;
        if (referenceObjects.Any())
        {
            maxKeyNum = referenceObjects.Max(j => int.Parse(Tools.lastNum.Match(j["key"]?.ToString() ?? "0").Value));
        }
        int nextKeyIndex = maxKeyNum + 1;

        string basePathWithoutExt = Path.ChangeExtension(relativePath, "");

        foreach (var entry in newEntries)
        {
            if (oldEntriesDict.TryGetValue(entry.Original, out JObject existingObject))
            {
                // 如果旧条目存在，直接使用
                // 可以选择性更新 context
                if (entry.Context != null && (!existingObject.ContainsKey("context") || existingObject["context"]?.ToString() != entry.Context))
                {
                    existingObject["context"] = entry.Context;
                }
                finalJsonList.Add(existingObject);
            }
            else
            {
                // 如果是新条目，则创建并添加
                Console.WriteLine($"[{entry.Type}] {entry.Original}");
                finalJsonList.Add(entry.ToJObject(basePathWithoutExt, ref nextKeyIndex));
                // 将新添加的条目也加入字典，以处理源文件内的重复原文
                if (!oldEntriesDict.ContainsKey(entry.Original))
                {
                    oldEntriesDict.Add(entry.Original, finalJsonList.Last() as JObject);
                }
            }
        }
        return finalJsonList;
    }


    /// <summary>
    /// 从新的条目列表直接创建JObject列表（用于没有旧字典参考的情况）。
    /// </summary>
    private static List<JObject> CreateNewJsonObjects(List<TranslationEntry> newEntries, string relativePath)
    {
        int keyIndex = 0;
        string basePathWithoutExt = Path.ChangeExtension(relativePath, "");
        var newJsonObjects = new List<JObject>();

        foreach (var entry in newEntries)
        {
            newJsonObjects.Add(entry.ToJObject(basePathWithoutExt, ref keyIndex));
        }
        return newJsonObjects;
    }

    /// <summary>
    /// 判断一个文件是否是与ERB文件同名的ERH文件。
    /// </summary>
    private static bool IsDuplicateErh(string filePath)
    {
        return filePath.EndsWith(".erh", StringComparison.OrdinalIgnoreCase) &&
               File.Exists(Path.ChangeExtension(filePath, ".erb"));
    }

    /// <summary>
    /// 用于统一表示不同类型解析器输出的翻译条目的内部辅助类。
    /// </summary>
    private class TranslationEntry
    {
        public string Original { get; set; }
        public string Context { get; set; }
        public string Type { get; set; } // "变量", "文本", "CSV" 等

        /// <summary>
        /// 将条目转换为用于输出的 JObject。
        /// </summary>
        public JObject ToJObject(string keyBasePath, ref int index)
        {
            var keyBuilder = new StringBuilder();
            if (Type == "变量" || Type == "文本")
            {
                keyBuilder.Append(Type);
            }
            keyBuilder.Append(keyBasePath).Append(index.ToString("D5"));

            var jobj = new JObject
            {
                ["key"] = keyBuilder.ToString(),
                ["original"] = Original,
                ["translation"] = "",
                ["stage"] = 0
            };

            if (!string.IsNullOrEmpty(Context))
            {
                jobj["context"] = Context;
            }

            index++; // 序号自增
            return jobj;
        }
    }
    #endregion

    /// <summary>
    /// 对比新旧两个目录，提取所有新增或内容被修改的文件到新目录。
    /// </summary>
    static void ExtractModifiedFiles()
    {
        // 1. 获取用户输入的目录路径
        string oldPath = Tools.ReadLine("请拖入原字典目录:");
        string newPath = Tools.ReadLine("请拖入新字典目录:");
        string outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extracted_Files_C");

        if (!Directory.Exists(oldPath) || !Directory.Exists(newPath))
        {
            Console.WriteLine("【错误】输入的一个或多个目录不存在，操作已取消。");
            return;
        }

        // 2. 准备输出目录
        Console.WriteLine($"文件将被提取到: {outputPath}");
        if (Directory.Exists(outputPath))
        {
            Console.WriteLine("警告：输出目录已存在，将先被清空。");
            Tools.CleanDirectory(outputPath);
        }
        Directory.CreateDirectory(outputPath);

        Console.WriteLine("正在开始对比文件，请稍候...");
        Timer.Start();

        // 3. 获取新目录中的所有文件
        var newFiles = Directory.GetFiles(newPath, "*.*", SearchOption.AllDirectories);
        int copiedFilesCount = 0;
        int processedFilesCount = 0;

        // 4. 并行遍历和对比文件
        Parallel.ForEach(newFiles, (newFile) =>
        {
            var relativePath = Tools.GetrelativePath(newFile, newPath);
            var oldFile = Path.Combine(oldPath, relativePath);
            var destFile = Path.Combine(outputPath, relativePath);

            bool shouldCopy = false;
            string reason = "";

            if (!File.Exists(oldFile))
            {
                shouldCopy = true;
                reason = "[新增]";
            }
            else
            {
                // 通过比较文件内容来判断是否被修改
                if (!AreFilesEqual(newFile, oldFile))
                {
                    shouldCopy = true;
                    reason = "[修改]";
                }
            }

            if (shouldCopy)
            {
                // 确保目标子目录存在
                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                // 复制文件
                File.Copy(newFile, destFile, true);
                Console.WriteLine($"{reason} {relativePath}");
                // 使用 Interlocked.Increment 来确保多线程计数的安全
                Interlocked.Increment(ref copiedFilesCount);
            }
            Interlocked.Increment(ref processedFilesCount);
        });

        Timer.Stop();
        Console.WriteLine($"\n对比完成！共处理 {processedFilesCount} 个文件，提取了 {copiedFilesCount} 个新增或修改的文件到目录 C。");

        // 5. 如果配置允许，自动打开输出文件夹
        if (Configs.autoOpenFolder)
        {
            Process.Start(outputPath);
        }
    }

    /// <summary>
    /// 高效地比较两个文件的内容是否完全相同。
    /// </summary>
    /// <param name="path1">第一个文件的路径。</param>
    /// <param name="path2">第二个文件的路径。</param>
    /// <returns>如果文件内容相同则返回 true，否则返回 false。</returns>
    private static bool AreFilesEqual(string path1, string path2)
    {
        try
        {
            // 不预先检查文件大小，因为换行符不同会导致大小不同
            byte[] bytes1 = File.ReadAllBytes(path1);
            byte[] bytes2 = File.ReadAllBytes(path2);

            // 优化：如果文件在字节层面完全一样，则直接返回 true，这是最快的情况。
            if (bytes1.SequenceEqual(bytes2))
            {
                return true;
            }

            // 规范化处理：通过移除所有回车符（Carriage Return, CR, 0x0D, '\r'）的字节
            // 来实现对不同换行符的兼容。这会将 Windows 的 CRLF (`\r\n`) 和
            // Unix 的 LF (`\n`) 都视为相同的换行。
            // 使用 LINQ 的 Where 操作可以高效地创建一个不包含 CR 字节的新序列。
            var normalizedBytes1 = bytes1.Where(b => b != 0x0D);
            var normalizedBytes2 = bytes2.Where(b => b != 0x0D);

            // 比较规范化后的字节序列。
            return normalizedBytes1.SequenceEqual(normalizedBytes2);
        }
        catch (IOException ex)
        {
            // 处理文件可能被占用等IO异常
            Console.WriteLine($"【IO错误】无法对比文件: {ex.Message}");
            return false; // 如果无法比较，则默认它们不同，以便提取出来供用户检查。
        }
    }

}

