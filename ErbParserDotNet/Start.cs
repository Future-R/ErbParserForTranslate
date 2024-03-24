using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

public static class Start
{
    static readonly string[] erbExtensions = new string[] { ".erb", ".erh" };
    public static void Main()
    {
        Console.WriteLine("请拖入游戏根目录：");
        
        ReadFile(Console.ReadLine().Trim('"'));

        Console.ReadKey();
    }

    static void ReadFile(string path)
    {
        // 统计耗时
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        // 处理CSV
        string csvDirectory = Path.Combine(path, "CSV");
        string erbDirectory = Path.Combine(path, "ERB");

        if (Directory.Exists(csvDirectory))
        {
            // TODO
        }
        else
        {
            throw new DirectoryNotFoundException($"找不到CSV目录: {csvDirectory}");
        }
        if (Directory.Exists(erbDirectory))
        {
            var erbNames = Directory.EnumerateFiles(erbDirectory, "*.*", SearchOption.AllDirectories)
            .Where(file => erbExtensions.Any(x => file.EndsWith(x, StringComparison.OrdinalIgnoreCase)));
            Parallel.ForEach(erbNames, fileName =>
            {
                ERBParser parser = new ERBParser();
                parser.ParseFile(fileName);
                //parser.DebugPrint();
            });
        }
        else
        {
            throw new DirectoryNotFoundException($"找不到ERB目录: {erbDirectory}");
        }

        stopwatch.Stop();
        TimeSpan ts = stopwatch.Elapsed;

        // 格式化并输出耗时
        string elapsedTime = String.Format("{0:00}:{1:00}.{2:00}",
            ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);
        Console.WriteLine("耗时：" + elapsedTime);
    }
}

