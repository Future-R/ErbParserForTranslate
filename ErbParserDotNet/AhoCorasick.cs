using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Aho-Corasick自动机，用于字符串搜索和模式替换
/// </summary>
public class AhoCorasick
{
    // 根节点是自动机的起点，所有模式串的匹配都是从这个节点开始的
    private readonly Node _root = new Node();

    // 添加模式串及其对应的翻译，构建模式串到翻译的映射
    public void AddPattern(string pattern, string translation)
    {
        Node node = _root;
        foreach (char c in pattern)
        {
            // 确保每个字符都有一个对应的子节点
            if (!node.Children.ContainsKey(c))
            {
                node.Children[c] = new Node();
            }
            node = node.Children[c];
        }
        // 设置模式串的翻译和原始长度
        node.Translation = translation;
        node.OriginalLength = pattern.Length;
    }

    // 构建Aho-Corasick自动机，设置每个节点的失败指针
    public void Build()
    {
        // 创建一个队列用于存储待处理的节点
        Queue<Node> queue = new Queue<Node>();
        // 将所有子节点加入队列，并设置它们的失败指针为根节点
        foreach (Node node in _root.Children.Values)
        {
            queue.Enqueue(node);
            node.Failure = _root;
        }
        while (queue.Count > 0)
        {
            Node r = queue.Dequeue();
            foreach (char c in r.Children.Keys)
            {
                Node u = r.Children[c];
                queue.Enqueue(u);
                Node v = r.Failure;
                // 寻找v的失败指针中是否有c的子节点
                while (v != _root && !v.Children.ContainsKey(c))
                {
                    v = v.Failure;
                }
                // 设置u的失败指针为找到的子节点或者根节点
                u.Failure = v.Children.ContainsKey(c) ? v.Children[c] : _root;
            }
        }
    }

    // 处理文本，将匹配到的模式串替换为对应的翻译
    public string Process(string text)
    {
        // 记录需要替换的原文索引
        List<(int start, int length, string translation)> replacements = new List<(int start, int length, string translation)>();
        Node node = _root;
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            Node success = _root;
            // 如果当前节点没有对应的子节点，通过失败指针继续匹配
            while (node != _root && !node.Children.ContainsKey(c))
            {
                node = node.Failure;
            }
            // 如果当前节点有子节点c，则移动到该子节点
            node = node.Children.ContainsKey(c) ? node.Children[c] : _root;

            // 如果匹配成功，用一个tempNode来记录最长匹配，然后再继续搜索子节点有没有i+1，直到当前节点没有对应的子节点，或者i走完了，把tempNode扔给replacements
            // 因为是向下匹配，所以只要匹配成功就一定会更长，但失败不一定只回退一步，所以需要一个int记录深度
            if (node.Translation != null)
            {
                int depth = -1;
                do
                {
                    if (node.Translation != null)
                    {
                        success = node;
                        depth = -1;
                    }
                    i++;
                    depth++;
                    if (i >= text.Length) break;
                    c = text[i];
                    node = node.Children.ContainsKey(c) ? node.Children[c] : _root;
                } while (node != _root);

                // 计算替换的起始位置和长度
                int start = i - success.OriginalLength - depth;
                replacements.Add((start, success.OriginalLength, success.Translation));
                // 跳过将被替换的部分，并重置当前节点为根节点
                i = start + success.OriginalLength;
                node = _root;
            }
            else
            {
                i++;
            }
        }
        // 到这里才真正执行替换
        StringBuilder result = new StringBuilder(text);
        for (int j = replacements.Count - 1; j >= 0; j--)
        {
            (int start, int length, string translation) = replacements[j];
            result.Remove(start, length);
            result.Insert(start, translation);
        }
        return result.ToString();
    }

    // 自动机节点
    private class Node
    {
        public Dictionary<char, Node> Children { get; } = new Dictionary<char, Node>();
        public Node Failure { get; set; }
        public string Translation { get; set; }
        public int OriginalLength { get; set; }
    }
}