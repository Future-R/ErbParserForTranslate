﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class RESParser
{
    List<string> valueList = new List<string>();
    public void ParseFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath, Configs.fileEncoding);
        foreach (var line in lines)
        {
            string value = Tools.GetResValue(line);
            if (!string.IsNullOrEmpty(value)) valueList.Add(value);
        }
    }

    /// <summary>
    /// 在本程序的目录下输出Json
    /// </summary>
    /// <param name="targetFile">完整路径</param>
    /// <param name="relativePath">相对路径</param>
    public void WriteJson(string targetFile, string relativePath)
    {
        var PTJsonObjList = valueList.Distinct().Select((item, index) =>
        {
            return new JObject
            {
                // 键值是相对路径(去除后缀)+ 5位数字ID
                ["key"] = Path.ChangeExtension(relativePath, "") + index.ToString().PadLeft(5, '0'),
                ["original"] = item,
                ["translation"] = ""
            };
        });

        if (PTJsonObjList.Count() > 0)
        {
            string jsonContent = JsonConvert.SerializeObject(PTJsonObjList, Formatting.Indented);
            File.WriteAllText(Path.ChangeExtension(targetFile, ".json"), jsonContent);
        }
    }

    public void WriteJson(string targetFile, string relativePath, List<string> referenceList)
    {
        // 条数相等不一定匹配，但不相等肯定不匹配
        if (valueList.Count == referenceList.Count)
        {
            var originObjs = valueList.Distinct().ToList();
            var referenceObjs = referenceList.Distinct().ToList();
            // 二次检查
            if (originObjs.Count == referenceObjs.Count)
            {
                List<JObject> PTJsonObjList = new List<JObject>();
                for (int index = 0; index < originObjs.Count; index++)
                {
                    // 如果对比发现，参考和原版不一致，那么把参考的值填进translation
                    if (originObjs[index] != referenceObjs[index])
                    {
                        PTJsonObjList.Add(new JObject
                        {
                            // 键值是相对路径(去除后缀)+ 5位数字ID
                            ["key"] = new StringBuilder("图像")
                            .Append(Path.ChangeExtension(relativePath, ""))
                            .Append(index.ToString().PadLeft(5, '0'))
                            .ToString(),
                            ["original"] = originObjs[index],
                            ["translation"] = referenceObjs[index]
                        });
                    }
                    else
                    {
                        PTJsonObjList.Add(new JObject
                        {
                            ["key"] = new StringBuilder("图像")
                            .Append(Path.ChangeExtension(relativePath, ""))
                            .Append(index.ToString().PadLeft(5, '0'))
                            .ToString(),
                            ["original"] = originObjs[index],
                            ["translation"] = ""
                        });
                    }
                }
                if (PTJsonObjList.Count > 0)
                {
                    string jsonContent = JsonConvert.SerializeObject(PTJsonObjList, Formatting.Indented);
                    File.WriteAllText(Path.ChangeExtension(targetFile, ".json"), jsonContent);
                }
                return;
            }
        }
        Console.WriteLine($"【警告】：跳过了填充{relativePath}。");
        // 改用常规模式
        WriteJson(targetFile, relativePath);
    }
    /// <summary>
    /// 返回valueList
    /// </summary>
    /// <returns></returns>
    public List<string> GetList()
    {
        return valueList;
    }
}

