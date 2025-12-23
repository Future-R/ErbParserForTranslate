using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;


public class XMLParser
{
    List<string> valueList = new List<string>();
    public void ParseFile(string filePath)
    {
        try
        {
            XDocument doc = XDocument.Load(filePath, LoadOptions.None);

            // 技能
            ProcessSkillDefinitions(doc, valueList);

            // 敌人
            ProcessEnemyData(doc, valueList);

            // 口上
            ProcessKoJoData(doc, valueList);

            // 地图
            ProcessGmapData(doc, valueList);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"【错误】：{Path.GetFileName(filePath)}  {ex.Message}");
        }
    }

    static void ProcessSkillDefinitions(XDocument doc, List<string> terms)
    {
        foreach (XElement defname in doc.Descendants("defname"))
        {
            ExtractElementText(defname.Element("skillname"), terms);
            ExtractElementText(defname.Element("description"), terms);
            ExtractElementText(defname.Element("unlockhint"), terms);

            foreach (XElement reqparam in defname.Elements("reqparam"))
            {
                ExtractAttributeValue(reqparam, "name", terms);
            }
        }
    }

    static void ProcessEnemyData(XDocument doc, List<string> terms)
    {
        foreach (XElement enemyData in doc.Descendants("enemy_data"))
        {
            ExtractAttributeValue(enemyData, "ENEMY_NAME", terms);
            ExtractAttributeValue(enemyData, "TARGETING", terms);
            ExtractAttributeValue(enemyData, "ENEMY_TYPE", terms);

            foreach (XAttribute attr in enemyData.Attributes())
            {
                if (IsRewardItemAttribute(attr.Name.LocalName))
                {
                    terms.Add(attr.Value.Trim());
                }
            }
        }
    }

    static void ProcessKoJoData(XDocument doc, List<string> terms)
    {
        foreach (XElement command in doc.Descendants("command"))
        {
            // 提取基本属性
            ExtractAttributeValue(command, "name", terms);
            ExtractAttributeValue(command, "HO_Act", terms);

            // 处理对话内容
            XElement contents = command.Element("contents");
            if (contents != null && !string.IsNullOrWhiteSpace(contents.Value))
            {
                //ProcessContentLines(contents.Value, terms);
                terms.Add(contents.Value);
            }
        }
    }

    static void ProcessGmapData(XDocument doc, List<string> terms)
    {
        foreach (XElement gmapData in doc.Descendants("GMAPDATA"))
        {
            // 提取地图节点名称
            ExtractElementText(gmapData.Element("NODE_NAME"), terms);
        }
    }

    [Obsolete]
    static void ProcessContentLines(string content, List<string> terms)
    {
        // 分割多行文本并清理格式
        string[] lines = content.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            string processed = line.Trim();

            // 移除颜色标记（例：@C:{...}@...@/C@）
            processed = Regex.Replace(processed, @"@C:\{.*?\}@(.*?)@/C@", "$1");

            // 保留变量但过滤函数调用（例：[$FUNC:...]）
            processed = Regex.Replace(processed, @"\[\$FUNC:.*?\]", "");

            if (!string.IsNullOrWhiteSpace(processed))
            {
                terms.Add(processed);
            }
        }
    }

    static void ExtractElementText(XElement element, List<string> collection)
    {
        if (element?.Value != null && !string.IsNullOrWhiteSpace(element.Value))
        {
            collection.Add(element.Value.Trim());
        }
    }

    static void ExtractAttributeValue(XElement element, string attributeName, List<string> collection)
    {
        XAttribute attr = element?.Attribute(attributeName);
        if (attr?.Value != null && !string.IsNullOrWhiteSpace(attr.Value))
        {
            collection.Add(attr.Value.Trim());
        }
    }

    static bool IsRewardItemAttribute(string attributeName)
    {
        return Regex.IsMatch(attributeName, @"^REWARD_ITEM_\d+$");
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
                ["key"] = relativePath + index.ToString().PadLeft(5, '0'),
                ["original"] = item,
                ["translation"] = "",
                ["stage"] = 0
            };
        });

        if (PTJsonObjList.Count() > 0)
        {
            string jsonContent = JsonConvert.SerializeObject(PTJsonObjList, Formatting.Indented);
            File.WriteAllText(targetFile + ".json", jsonContent);
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
                            ["key"] = new StringBuilder("变量")
                            .Append(relativePath)
                            .Append(index.ToString().PadLeft(5, '0'))
                            .ToString(),
                            ["original"] = originObjs[index],
                            ["translation"] = referenceObjs[index],
                            ["stage"] = 1
                        });
                    }
                    else
                    {
                        PTJsonObjList.Add(new JObject
                        {
                            ["key"] = new StringBuilder("变量")
                            .Append(Path.ChangeExtension(relativePath, ""))
                            .Append(index.ToString().PadLeft(5, '0'))
                            .ToString(),
                            ["original"] = originObjs[index],
                            ["translation"] = "",
                            ["stage"] = 0
                        });
                    }
                }
                if (PTJsonObjList.Count > 0)
                {
                    string jsonContent = JsonConvert.SerializeObject(PTJsonObjList, Formatting.Indented);
                    File.WriteAllText(targetFile + ".json", jsonContent);
                }
                return;
            }
        }
        Console.WriteLine($"【警告】：跳过了填充{relativePath}。");
        // 改用常规模式
        WriteJson(targetFile, relativePath);
    }

    public List<string> GetList()
    {
        return valueList;
    }
}

