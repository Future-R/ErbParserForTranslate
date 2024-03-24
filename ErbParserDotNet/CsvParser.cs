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
        var lines = File.ReadAllLines(filePath);
        foreach (var line in lines)
        {
            // 修剪行末注释
            var index = line.IndexOf(';');
            string contentWithoutComment = index != -1 ? line.Substring(0, index) : line;

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
        var PTJsonObjList = valueList.Select((item, index) =>
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
}

