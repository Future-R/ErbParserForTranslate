using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class CSVParser
{
    List<string> valueList = new List<string>();
    public void ParseFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath, Start.fileEncoding);
        foreach (var line in lines)
        {
            // 修剪行末注释，刚刚知道原来括号也是注释
            var indexA = line.IndexOf(';');
            var contentWithoutComment = indexA != -1 ? line.Substring(0, indexA) : line;
            var indexB = contentWithoutComment.IndexOf('(');
            contentWithoutComment = indexB != -1 ? contentWithoutComment.Substring(0, indexB) : contentWithoutComment;

            // 分割每一行的内容
            var parts = contentWithoutComment.Split(',');

            // 提取除了第一列以外的内容
            valueList.AddRange(parts
                .Skip(1)
                // 筛除空成员和纯数字成员
                .Where(text => !string.IsNullOrEmpty(text) && !int.TryParse(text, out _)));
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
                // 键值是相对路径(去除后缀)+四位数字ID
                ["key"] = Path.ChangeExtension(relativePath, "") + index.ToString().PadLeft(4, '0'),
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
                            // 键值是相对路径(去除后缀)+四位数字ID
                            ["key"] = Path.ChangeExtension(relativePath, "") + index.ToString().PadLeft(4, '0'),
                            ["original"] = originObjs[index],
                            ["translation"] = referenceObjs[index]
                        });
                    }
                    else
                    {
                        PTJsonObjList.Add(new JObject
                        {
                            ["key"] = Path.ChangeExtension(relativePath, "") + index.ToString().PadLeft(4, '0'),
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

