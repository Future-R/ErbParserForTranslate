using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 解析Era传统字典，就是用/t分割的
/// </summary>
public static class EraDictParser
{
    public static void 二级菜单()
    {
        string menuString =
@"请输入序号并回车：
[ 0] - ERA字典转PT字典
[ 1] - PT字典转ERA字典
[ 2] - 女仆酱字典转PT字典（理论上不用转换也可以上传Paratranz）
[ 3] - 还是算了";
        string command = Tools.ReadLine(menuString);
        switch (command)
        {
            case "0":
                解析Era字典();
                break;
            case "1":
                解析PT字典二级菜单();
                break;
            case "2":
                解析女仆酱字典();
                break;
            case "3":
                return;
            default:
                return;
        }
    }

    public static void 解析女仆酱字典()
    {
        string ERA目录 = Tools.ReadLine("请拖入女仆酱字典(txt)目录：");
        string[] ERA文件名组 = Directory.GetFiles(ERA目录, "*.txt", SearchOption.AllDirectories);
        Timer.Start();
        Parallel.ForEach(ERA文件名组, era文件 =>
        {
            ParseERA(era文件, true);
        });
        Timer.Stop();
        Console.WriteLine("转换完毕！");
        if (Configs.autoOpenFolder)
        {
            Process.Start(ERA目录);
        }
    }
    public static void 解析Era字典()
    {
        string ERA目录 = Tools.ReadLine("请拖入ERA字典(txt)目录：");
        string[] ERA文件名组 = Directory.GetFiles(ERA目录, "*.txt", SearchOption.AllDirectories);
        Timer.Start();
        //foreach (var era文件 in ERA文件名组)
        //{
        //    ParseFile(era文件);
        //}
        Parallel.ForEach(ERA文件名组, era文件 =>
        {
            ParseERA(era文件);
        });
        Timer.Stop();
        Console.WriteLine("转换完毕！");
        if (Configs.autoOpenFolder)
        {
            Process.Start(ERA目录);
        }
    }

    public enum 导出模式
    {
        完整导出,
        仅已翻译,
        仅未翻译
    }

    public static void 解析PT字典二级菜单()
    {
        string menuString =
@"请输入序号并回车：
[ 0] - 【完整导出】所有字典条目
[ 1] - 仅导出【已翻译】的字典条目
[ 2] - 仅导出【未翻译】的字典条目
[ 3] - 【返回】上一级";
        string command = Tools.ReadLine(menuString);
        switch (command)
        {
            case "0":
                解析PT字典(导出模式.完整导出);
                break;
            case "1":
                解析PT字典(导出模式.仅已翻译);
                break;
            case "2":
                解析PT字典(导出模式.仅未翻译);
                break;
            case "3":
                return;
            default:
                解析PT字典(导出模式.完整导出);
                return;
        }
    }

    public static void 解析PT字典(导出模式 模式)
    {
        string PT目录 = Tools.ReadLine("请拖入PT字典(json)目录：");
        string[] PT文件名组 = Directory.GetFiles(PT目录, "*.json", SearchOption.AllDirectories);
        Timer.Start();
        Parallel.ForEach(PT文件名组, pt文件 =>
        {
            ParsePT(pt文件, 模式);
        });
        Timer.Stop();
        Console.WriteLine("转换完毕！");
        if (Configs.autoOpenFolder)
        {
            Process.Start(PT目录);
        }
    }

    public static void ParsePT(string filePath, 导出模式 模式)
    {
        string json输入 = File.ReadAllText(filePath);
        // pt的json都是[起头的
        if (!json输入.StartsWith("["))
        {
            return;
        }
        JArray jsonArray = JArray.Parse(json输入);
        StringBuilder 输出字符串 = new StringBuilder();

        bool 导出已翻译 = 模式 == 导出模式.完整导出 || 模式 == 导出模式.仅已翻译;
        bool 导出未翻译 = 模式 == 导出模式.完整导出 || 模式 == 导出模式.仅未翻译;

        foreach (var jobj in jsonArray.ToObject<List<JObject>>())
        {
            string 原文 = jobj["original"].ToString();
            if (jobj.ContainsKey("stage") && (int)jobj["stage"].ToObject(typeof(int)) > 0)
            {
                if (导出已翻译)
                {
                    输出字符串.Append(原文);
                    输出字符串.Append("\t");
                    输出字符串.AppendLine(jobj["translation"].ToString());
                }

            }
            else
            {
                if (导出未翻译)
                {
                    输出字符串.Append(原文);
                    输出字符串.Append("\t");
                    输出字符串.AppendLine(原文);
                }
            }
        }
        string 输出文件名 = Path.ChangeExtension(filePath, "txt");
        if (输出字符串.Length > 0)
        {
            File.WriteAllText(输出文件名, 输出字符串.ToString());
        }
        File.Delete(filePath);
    }

    public static void ParseERA(string filePath, bool 女仆酱模式 = false)
    {
        List<string> lineList = new List<string>();
        try
        {
            string content = File.ReadAllText(filePath, Configs.fileEncoding);
            lineList = content.Replace(Environment.NewLine, "\n").Split(new[] { "\n" }, StringSplitOptions.None).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"读取文件失败：{ex.Message}");
            return;
        }
        // 放飞，中文命名！
        string 文件名 = Path.GetFileName(filePath);
        List<JObject> PZJsonObj = new List<JObject>();
        int 序号 = 0;
        foreach (string line in lineList)
        {
            if (女仆酱模式)
            {
                JObject pzobj = new JObject
                {
                    ["key"] = 文件名 + 序号.ToString().PadLeft(5, '0'),
                    ["original"] = line,
                    ["translation"] = line,
                    ["stage"] = 0
                };
                PZJsonObj.Add(pzobj);
                序号++;
            }
            else
            {
                if (!line.Contains("\t"))
                {
                    continue;
                }
                else
                {
                    string[] 键值数组 = line.Split(new[] { "\t" }, StringSplitOptions.None);
                    if (键值数组.Length == 2)
                    {
                        string 原文 = 键值数组[0];
                        string 译文 = 键值数组[1];
                        int stage = 1;
                        if (原文 == 译文)
                        {
                            译文 = "";
                            stage = 0;
                        }
                        JObject pzobj = new JObject
                        {
                            ["key"] = 文件名 + 序号.ToString().PadLeft(5, '0'),
                            ["original"] = 原文,
                            ["translation"] = 译文,
                            ["stage"] = stage
                        };

                        PZJsonObj.Add(pzobj);
                        序号++;
                    }
                }
            }
        }
        string json输出 = JsonConvert.SerializeObject(PZJsonObj, Formatting.Indented);
        string 输出文件名 = Path.ChangeExtension(filePath, "json");
        File.WriteAllText(输出文件名, json输出);
        // PZ会识别txt，所以干脆删掉
        File.Delete(filePath);
    }

}
