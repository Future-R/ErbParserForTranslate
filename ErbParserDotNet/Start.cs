using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public static class Start
{
    static readonly string[] fileExtensions = new string[] { ".erb", ".erh", ".csv" };
    public static void Main()
    {
        Console.WriteLine("请拖入ERB目录（将遍历子目录）：");
        ReadFile(Console.ReadLine().Trim('"'));
        
        Console.WriteLine("完成！");
        Console.ReadKey();
    }

    static void ReadFile(string path)
    {
        // 获取目录下所有".erb", ".erh", ".csv"
        var fileNames = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(file => fileExtensions.Any(x => file.EndsWith(x, StringComparison.OrdinalIgnoreCase)));
        foreach (string fileName in fileNames)
        {
            // 解析CSV
            if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                // TODO
            }
            // 解析ERB和ERH
            else
            {
                ERBParser parser = new ERBParser();
                parser.ParseFile(fileName);
                parser.DebugPrint();
            }
        }
    }
}

