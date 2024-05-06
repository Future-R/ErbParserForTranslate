using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// 解析Era传统字典，就是用/t分割的
/// </summary>
public static class EraDictParser
{
    public static void 解析Era字典()
    {
        Tools.CleanDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CSV"));
        Tools.CleanDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ERB"));
        string ERA目录 = Tools.ReadLine("请拖入ERA字典目录：");
        string[] ERA文件名组 = Directory.GetFiles(ERA目录, "*.txt", SearchOption.AllDirectories);
        Timer.Start();
        //foreach (var era文件 in ERA文件名组)
        //{
        //    ParseFile(era文件);
        //}
        Parallel.ForEach(ERA文件名组, era文件 =>
        {
            ParseFile(era文件);
        });
        Timer.Stop();
        Console.WriteLine("已将生成的JSON放置在原目录下");
        if (Configs.autoOpenFolder)
        {
            Process.Start(ERA目录);
        }
    }

    public static void ParseFile(string filePath)
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
        string json输出 = JsonConvert.SerializeObject(PZJsonObj, Formatting.Indented);
        string 输出文件名 = Path.ChangeExtension(filePath, "json");
        File.WriteAllText(输出文件名, json输出);
    }

}
