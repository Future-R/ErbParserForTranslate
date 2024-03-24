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
        string appPath = System.AppDomain.CurrentDomain.BaseDirectory;

        Console.WriteLine("请拖入游戏根目录：");
        ReadFile(Console.ReadLine().Trim('"'), appPath);

        Console.ReadKey();
    }

    static void ReadFile(string path, string appPath)
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
                // 在本程序目录下创建与ERA目录相同结构的目录
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
        string elapsedTime = String.Format("{0:00}:{1:00}.{2:00}",
            ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);
        Console.WriteLine("耗时：" + elapsedTime);
    }
}

