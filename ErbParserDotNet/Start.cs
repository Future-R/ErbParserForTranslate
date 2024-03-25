﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;

public static class Start
{
    
    static readonly string[] erbExtensions = new string[] { ".erb", ".erh" };
    public static void Main()
    {
        string appPath = System.AppDomain.CurrentDomain.BaseDirectory;
        Tools.Init();

        while (true)
        {
            // 主菜单
            Console.WriteLine("请输入序号并回车（默认为0）：");
            Console.WriteLine("0.用字典汉化游戏（TODO）\n1.提取文本到字典\n2.补充新版本条目到字典（TODO）\n3.从已汉化本体中提取字典（WIP）");
            string command = Console.ReadLine();
            switch (command)
            {
                case "0":
                    break;
                case "1":
                    ReadFile(appPath);
                    break;
                case "2":
                    break;
                case "3":
                    break;
                case "4":
                    break;
                default:
                    break;
            }

            Console.ReadKey();
            Console.Clear();
        }
    }

    /// <summary>
    /// 用字典汉化游戏
    /// </summary>
    static void Translator()
    {
        // TODO
    }
    /// <summary>
    /// 提取文本到字典
    /// </summary>
    /// <param name="appPath"></param>
    /// <exception cref="DirectoryNotFoundException"></exception>
    static void ReadFile(string appPath)
    {
        // 清理上次生成的文件，必须放前面不然删得慢了碰到后面的多线程会报错
        if (Directory.Exists(Path.Combine(appPath, "CSV"))) Directory.Delete(Path.Combine(appPath, "CSV"), true);
        if (Directory.Exists(Path.Combine(appPath, "ERB"))) Directory.Delete(Path.Combine(appPath, "ERB"), true);

        Console.WriteLine("请拖入游戏根目录：");
        string path = Console.ReadLine().Trim('"');

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

