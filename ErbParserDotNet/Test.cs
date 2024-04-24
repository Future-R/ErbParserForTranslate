using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class Test
{
    public static void Debug()
    {
        //var lineString = Tools.ReadLine("请输入：");
        //if (lineString.StartsWith("@"))
        //{
        //    int start = lineString.IndexOf("(");
        //    int end = start != -1 ? lineString.IndexOf(")", start) : -1;
        //    // 函数在定义时要么只出现一对括号，要么没有括号而是用逗号划分
        //    // 左括号索引小于右括号索引，且左括号索引不为-1，说明匹配到正确的括号了
        //    if (start != -1 && start < end)
        //    {
        //        // 匹配函数名
        //        string funcName = lineString.Substring(1, start - 1);
        //        Console.WriteLine($"funcName:{funcName}");

        //        string args = lineString.Substring(start + 1, end - start - 1);
        //        var tokens = args.Split(',')
        //            .Where(arg => !string.IsNullOrWhiteSpace(arg))
        //            .Select(arg => arg.Trim());
        //        // 参数Token里可能不止参数名，还可能有 参数名 = "初始值"，此时判断右值是否为字符串，是字符串的话扔到text里
        //        foreach (var token in tokens)
        //        {
        //            string[] parts = token.Split('=');
        //            Console.WriteLine($"funcName:{parts[0].Trim()}");
        //            if (parts.Length > 1 && !int.TryParse(parts[1].Trim(), out _))
        //            {
        //                Console.WriteLine($"text:{parts[1].Trim()}");
        //            }
        //        }
        //    }
        //    else
        //    {
        //        var enumer = lineString.Split(',')
        //            .Where(arg => !string.IsNullOrWhiteSpace(arg))
        //            .Select(arg => arg.Trim());
        //        // enumer.FirstOrDefault()是函数名，不需要翻译，所以SKIP(1)
        //        // 其它成员是参数名
        //        foreach (var token in enumer.Skip(1))
        //        {
        //            string[] parts = token.Split('=');
        //            Console.WriteLine($"funcName:{parts[0].Trim()}");
        //            if (parts.Length > 1 && !int.TryParse(parts[1].Trim(), out _))
        //            {
        //                Console.WriteLine($"text:{parts[1].Trim()}");
        //            }
        //        }
        //    }
        //}

        //var test = Tools.ReadLine("请输入：");
        //if (test.StartsWith("VARSET "))
        //{
        //    var rightValue = test.Substring(6).Trim();
        //    var rightEnumer = rightValue.Split(',')
        //            .Where(arg => !string.IsNullOrWhiteSpace(arg))
        //            .Select(arg => arg.Trim());
        //    foreach (var item in rightEnumer)
        //    {
        //        Console.WriteLine(item);
        //    }
        //}

        //var test = Tools.ReadLine("请输入：");
        //ERBParser eRBParser = new ERBParser();
        //eRBParser.simpleFuncExpression(test);

        //var test = Tools.ReadLine("请输入：");
        //string kw_eng = @"[_a-zA-Z0-9]*";
        //Regex engArrayFilter = new Regex($@"^{kw_eng}$");
        //var isArray = engArrayFilter.IsMatch(test);
        //var isNum = int.TryParse(test, out int n) && n >= 0;
        //Console.WriteLine($"isArray:{isArray};isNum:{isNum}");

        var test = ExpressionParser.Slash(Tools.ReadLine("请输入："));
        foreach (var item in test.vari)
        {
            Console.WriteLine($"【变量】{item}");
        }
        foreach (var item in test.text)
        {
            Console.WriteLine($"【文本】{item}");
        }

        //AhoCorasick ahoCorasick = new AhoCorasick();
        //ahoCorasick.AddPattern("ABCDEFGH", "abcdefgh");
        //ahoCorasick.AddPattern("CDEF", "cdef");
        //ahoCorasick.AddPattern("EFG", "efg");
        //ahoCorasick.Build();
        //var test = @"ABCDEFGG";
        //test = ahoCorasick.Process(test);
        //Console.WriteLine(test);
    }

    public static void 检查CharaID变动()
    {
        string oldDirectory = Tools.ReadLine("请拖入旧CSV目录：");
        string[] oldFiles = Directory.GetFiles(oldDirectory, "*.csv", SearchOption.AllDirectories);
        string newDirectory = Tools.ReadLine("请拖入新CSV目录：");
        string[] newFiles = Directory.GetFiles(newDirectory, "*.csv", SearchOption.AllDirectories);

        Dictionary<string, int> 旧版字典 = new Dictionary<string, int>();
        Dictionary<string, int> 新版字典 = new Dictionary<string, int>();

        foreach (var item in oldFiles)
        {
            using (StreamReader sr = new StreamReader(item))
            {
                // 读取第一行ID
                string line = sr.ReadLine();
                string ID = Tools.GetCSVValue(line, false).FirstOrDefault();
                bool isNum = int.TryParse(ID.Trim(), out int id);
                // 取不到数字ID就跳过这个文件
                if (!isNum) continue;

                // 读取第二行名称
                line = sr.ReadLine();
                string Name = Tools.GetCSVValue(line, false).FirstOrDefault();
                // 取不到名字就跳过这个文件
                if (string.IsNullOrEmpty(Name)) continue;

                旧版字典.Add(Name, id);
            }
        }

        foreach (var item in newFiles)
        {
            using (StreamReader sr = new StreamReader(item))
            {
                // 读取第一行ID
                string line = sr.ReadLine();
                string ID = Tools.GetCSVValue(line, false).FirstOrDefault();
                bool isNum = int.TryParse(ID.Trim(), out int id);
                // 取不到数字ID就跳过这个文件
                if (!isNum) continue;

                // 读取第二行名称
                line = sr.ReadLine();
                string Name = Tools.GetCSVValue(line, false).FirstOrDefault();
                // 取不到名字就跳过这个文件
                if (string.IsNullOrEmpty(Name)) continue;

                新版字典.Add(Name, id);
            }
        }

        foreach (var item in 新版字典)
        {
            int id = item.Value;
            string name = item.Key;
            if (旧版字典.ContainsKey(name))
            {
                if (旧版字典[name] != id)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"【移动】{name,16}ID{旧版字典[name],5} => {id,5}");
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"【新增】ID{id,6}{name}");
            }
        }
    }
}

