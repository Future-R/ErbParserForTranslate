using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 计时器
/// </summary>
public static class Timer
{
    static Stopwatch stopwatch;

    public static void Start()
    {
        stopwatch = new Stopwatch();
        stopwatch.Start();
    }

    public static void Stop()
    {
        stopwatch.Stop();
        TimeSpan ts = stopwatch.Elapsed;
        string elapsedTime = String.Format("{0:00}秒{1:00}",
            ts.Seconds,
            ts.Milliseconds / 10);
        Console.WriteLine("耗时：" + elapsedTime);
    }
}

