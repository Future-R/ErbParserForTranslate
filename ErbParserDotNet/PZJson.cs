using System;
using System.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace ErbParserDotNet
{
    public class PZJson
    {
        public class JsonEntry
        {
            // 使用 JsonPropertyName 特性确保生成的JSON键是小写
            [System.Text.Json.Serialization.JsonPropertyName("key")]
            public string Key { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("original")]
            public string Original { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("translation")]
            public string Translation { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("stage")]
            public int Stage { get; set; }
        }

        public static void 输出ERD字典(string[] 文件名)
        {

            // 创建一个列表来存储将要被序列化的对象
            var jsonEntries = new List<JsonEntry>();

            // 2. 遍历文件路径数组
            for (int i = 0; i < 文件名.Length; i++)
            {
                string path = 文件名[i];

                // 3. 提取不带后缀的文件名
                // Path.GetFileNameWithoutExtension 是一个非常方便的方法
                string originalFileName = Path.GetFileNameWithoutExtension(path);

                // 4. 创建并填充对象实例
                var entry = new JsonEntry
                {
                    Key = $"文件名{i:D3}",
                    Original = originalFileName,
                    Translation = "",
                    Stage = 0
                };

                // 将创建的对象添加到列表中
                jsonEntries.Add(entry);
            }

            // 5. 将对象列表序列化为格式化的JSON字符串
            var options = new JsonSerializerOptions
            {
                // WriteIndented = true 使JSON输出带有缩进，更易读
                WriteIndented = true,
                // Encoder 设置允许中文字符直接显示而不是被编码为 \uXXXX
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            string jsonOutput = JsonSerializer.Serialize(jsonEntries, options);

            // 在控制台输出结果
            //Console.WriteLine("--- 生成的 JSON 内容 ---");
            //Console.WriteLine(jsonOutput);

            // 6. 将JSON字符串保存到文件
            string outputFilePath = "CSV\\ERD字典.json";
            File.WriteAllText(outputFilePath, jsonOutput);
        }
    }
}
