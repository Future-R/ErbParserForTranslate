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
        // 清理上次生成的文件
        string appPath = System.AppDomain.CurrentDomain.BaseDirectory;
        if (Directory.Exists(Path.Combine(appPath, "CSV"))) Directory.Delete(Path.Combine(appPath, "CSV"), true);
        if (Directory.Exists(Path.Combine(appPath, "ERB"))) Directory.Delete(Path.Combine(appPath, "ERB"), true);

        Console.WriteLine("请拖入游戏根目录：");
        ReadFile(Console.ReadLine().Trim('"'), appPath);

        Console.ReadKey();
    }

    static void ReadFile(string path, string appPath)
    {
        // 统计耗时
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        string csvDirectory = Path.Combine(path, "CSV");
        string erbDirectory = Path.Combine(path, "ERB");

        if (Directory.Exists(csvDirectory))
        {
            // 获取所有csv文件
            var csvNames = Directory.GetFiles(csvDirectory, "*.csv", SearchOption.AllDirectories);
            Parallel.ForEach(csvNames, csvName =>
            {
                // 获取相对路径
                var relativePath = csvName.Substring(path.Length + 1);
                // 得到输出路径
                var targetFile = Path.Combine(appPath, relativePath);
                CSVParser parser = new CSVParser();
                parser.ParseFile(csvName);

                // 输出Json
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                parser.WriteJson(targetFile, relativePath);
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
                var relativePath = erbName.Substring(path.Length + 1);
                // 得到输出路径
                var targetFile = Path.Combine(appPath, relativePath);
                ERBParser parser = new ERBParser();
                parser.ParseFile(erbName);
                //parser.DebugPrint();

                // 输出Json
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                parser.WriteJson(targetFile, relativePath);
            });
        }
        else
        {
            throw new DirectoryNotFoundException($"找不到ERB目录: {erbDirectory}");
        }

        stopwatch.Stop();
        TimeSpan ts = stopwatch.Elapsed;

        // 格式化并输出耗时
        string elapsedTime = String.Format("{0:00}秒{1:00}",
            ts.Seconds,
            ts.Milliseconds / 10);
        Console.WriteLine("耗时：" + elapsedTime);
        Console.WriteLine("已将生成的JSON放置在此程序目录下");
    }
}

