using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public static class Start
{
    public static void Main()
    {
        Console.WriteLine("请拖入：");
        ERBParser parser = new ERBParser();
        parser.ParseFile(Console.ReadLine().Trim('"'));
        parser.DebugPrint();
        Console.WriteLine("完成！");
        Console.ReadKey();
    }
}

