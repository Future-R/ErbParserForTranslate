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

    public static void Main()
    {
        string appPath = System.AppDomain.CurrentDomain.BaseDirectory;
        // 读取config.json配置
        Configs.Init();
        // 主要是预编译正则
        Tools.Init();

        while (true)
        {
            // 主菜单
            string menuString =
@"请输入序号并回车（默认为0）：
[0] - 用字典汉化游戏
[1] - 提取文本到字典
[2] - 补充新版本条目到字典
[3] - 从已汉化本体中提取字典
[4] - 检查字典变量名（制作中）
[5] - 暴力修正（这是一个临时的解决方案，旨在解决项目早期变量名翻译不统一的问题）
[6] - 设置
[7] - 访问项目主页";
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
                    CheckVariable();
                    break;
                case "5":
                    暴力修正();
                    break;
                case "6":
                    Settings();
                    break;
                case "7":
                    Process.Start("https://github.com/Future-R/ErbParserForTranslate");
                    break;
                case "999":
                    Debug();
                    break;
                default:
                    Translator();
                    break;
            }

            Console.ReadKey();
            Console.Clear();
        }
    }

    static void Debug()
    {
        //var test = Tools.ReadLine("请输入：");
        //if (test.StartsWith("@") || test.StartsWith("CALL") || test.StartsWith("TRYCALL"))
        //{
        //    int spIndex = test.IndexOf(" ");
        //    string rightValue = test.Substring(spIndex).Trim();
        //    var (vari, text) = ExpressionParser.Slash(rightValue);
        //    foreach (var item in vari)
        //    {
        //        Console.WriteLine($"【变量】{item}");
        //    }
        //    foreach (var item in text)
        //    {
        //        Console.WriteLine($"【文本】{item}");
        //    }
        //}

        //var test = Tools.ReadLine("请输入：");
        //string kw_eng = @"[_a-zA-Z0-9]*";
        //Regex engArrayFilter = new Regex($@"^{kw_eng}$");
        //var isArray = engArrayFilter.IsMatch(test);
        //var isNum = int.TryParse(test, out int n) && n >= 0;
        //Console.WriteLine($"isArray:{isArray};isNum:{isNum}");

        var test = ExpressionParser.Slash(Tools.ReadLine("请输入："));
        foreach (var item in test.vari)
        {
            Console.WriteLine($"【变量】{item}");
        }
        foreach (var item in test.text)
        {
            Console.WriteLine($"【文本】{item}");
        }

        //AhoCorasick ahoCorasick = new AhoCorasick();
        //ahoCorasick.AddPattern("ABDCEFG", "abcd");
        //ahoCorasick.AddPattern("ABC", "defg");
        //ahoCorasick.AddPattern("EF", "ef");
        //ahoCorasick.Build();
        //var test = "23,ABCDEFGHIJKABC";
        //test = ahoCorasick.Process(test);
        //Console.WriteLine(test);
    }

    static void Settings()
    {
        Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"));
        Console.WriteLine("请编辑打开的Config文件并保存，保存后才能生效。");
        Console.ReadKey();
        Configs.Init();
    }

    /// <summary>
    /// 虽然说要检查变量名，但具体要怎么给用户交互呢？
    /// 不断弹出选项让用户输入序号还是？
    /// 百分号和花括号要检查吗？
    /// </summary>
    static void CheckVariable()
    {

    }

    static void 暴力修正()
    {
        // 获取指定目录下的所有文件
        Console.WriteLine("请拖入res目录：");
        string[] files = Directory.GetFiles(Console.ReadLine(), "*.*", SearchOption.AllDirectories);

        // 筛选出指定类型的文件
        List<string> 文件名List = new List<string>();
        foreach (string file in files)
        {
            if (file.EndsWith(".erb", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith(".erh", StringComparison.OrdinalIgnoreCase))
            {
                文件名List.Add(file);
            }
        }
        string pt输入 = File.ReadAllText(Tools.ReadLine("请拖入暴力修正字典："));
        List<JObject> 修正字典 = JArray.Parse(pt输入).ToObject<List<JObject>>();

        Timer.Start();
        foreach (var 文件名 in 文件名List)
        {
            string 待处理文本 = File.ReadAllText(文件名);
            待处理文本 = Tools.ACReplace(待处理文本, JArray.Parse(pt输入));
            File.WriteAllText(文件名, 待处理文本);
        }
        Console.WriteLine("替换完毕！");
        Timer.Stop();
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
            var relativePath = Tools.GetrelativePath(jsonFile, transFileDirectory);
            bool isCSV = relativePath.StartsWith("CSV");
            bool fileExist = false;
            // 得到输出路径
            var targetFile = Path.Combine(gameDirectory, relativePath);

            // 遍历所有可能的扩展，后悔之前多手把扩展截掉了，现在想改稍微有点麻烦
            foreach (var ext in Configs.extensions)
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
                    string scriptContent = File.ReadAllText(targetFile, Configs.fileEncoding);
                    // 使用字典替换原文
                    scriptContent = Tools.ACReplace(scriptContent, jsonArray);
                    // 覆盖写入
                    File.WriteAllText(targetFile, scriptContent, Configs.fileEncoding);
                }
            }
        });

        Timer.Stop();
        
        Console.WriteLine("翻译已完成！");
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

        string mergePath = merge ? Tools.ReadLine("请拖入已经汉化的游戏根目录（作为翻译参考）：") : string.Empty;

        string path = Tools.ReadLine("请拖入需要汉化的游戏根目录：");

        string csvDirectory = Path.Combine(path, "CSV");
        string erbDirectory = Path.Combine(path, "ERB");

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
                    List<string> varNameList = parser.VarNameListFilter(tuple.name);
                    List<string> textList = parser.TextListFilter(tuple.text);

                    List<JObject> PTJsonObjList = new List<JObject>();
                    // 处理变量名
                    foreach (string varName in varNameList)
                    {
                        JObject targetObject = referenceObjects.FirstOrDefault(j => j["original"].ToString() == varName);
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
                                ["key"] = new StringBuilder("变量")
                                .Append(Path.ChangeExtension(relativePath, ""))
                                .Append(index.ToString().PadLeft(5, '0'))
                                .ToString(),
                                ["original"] = varName,
                                ["translation"] = ""
                            });
                            // 仅在成功添加新条目时，才自增序号
                            index++;
                        }
                    }
                    // 处理长文本
                    foreach (string text in textList)
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
                                ["key"] = new StringBuilder("文本")
                                .Append(Path.ChangeExtension(relativePath, ""))
                                .Append(index.ToString().PadLeft(5, '0'))
                                .ToString(),
                                ["original"] = text,
                                ["translation"] = ""
                            });
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

        Timer.Stop();
        if (Configs.autoOpenFolder)
        {
            Process.Start(appPath);
        }
    }
}

