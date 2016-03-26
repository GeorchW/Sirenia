using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Sirenia
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        class SpecialTreeNode : TreeNode
        {
            public DirectoryInfo Directory;
            public long Size;
            public long FileCount;
            public SpecialTreeNode(DirectoryInfo dir) : base(dir.Name)
            {
                Size = 0;
                FileCount = 0;
                Directory = dir;
            }
        }

        List<FileInfo> FileList = new List<FileInfo>();
        DirectoryInfo RootDir;
        long totalSize;
        private void btnGetFiles_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                RootDir = new DirectoryInfo(folderBrowserDialog.SelectedPath);
                FileList.Clear();
                tvFiles.Nodes.Clear();
                QueryDirectory(RootDir, new string[0]);
                totalSize = FileList.Sum(file => file.Length);
                float gb = ((float)totalSize) / (1 << 30);
                Dictionary<string, SpecialTreeNode> dict = new Dictionary<string, SpecialTreeNode>();
                foreach (var file in FileList)
                {
                    SpecialTreeNode node;
                    if (!dict.TryGetValue(file.Directory.FullName, out node))
                    {
                        node = new SpecialTreeNode(file.Directory);
                        dict.Add(file.Directory.FullName, node);
                    }
                    node.Size += file.Length;
                    node.FileCount++;
                }
                List<Tuple<string, SpecialTreeNode>> newNodes = new List<Tuple<string, SpecialTreeNode>>(dict.Select(item => new Tuple<string, SpecialTreeNode>(item.Key, item.Value)));
                while (newNodes.Count > 0)
                {
                    var copy = newNodes.ToArray();
                    newNodes.Clear();
                    foreach (var item in copy)
                    {
                        DirectoryInfo dir = new DirectoryInfo(item.Item1);
                        if (dir.Parent != null)
                        {
                            SpecialTreeNode parentNode;
                            if (!dict.TryGetValue(dir.Parent.FullName, out parentNode))
                            {
                                parentNode = new SpecialTreeNode(dir.Parent);
                                newNodes.Add(new Tuple<string, SpecialTreeNode>(dir.Parent.FullName, parentNode));
                                dict.Add(dir.Parent.FullName, parentNode);
                            }
                            parentNode.Nodes.Add(item.Item2);
                        }
                        else
                        {
                            tvFiles.Nodes.Add(item.Item2);
                        }
                    }
                }
                CorrectTree((SpecialTreeNode)tvFiles.Nodes[0]);
            }
        }

        void CorrectTree(SpecialTreeNode node)
        {
            var nodes = node.Nodes.OfType<SpecialTreeNode>();
            foreach (var sub in nodes)
            {
                CorrectTree(sub);
            }
            var children = nodes.OrderBy(sub => sub.Size).ToArray();
            node.Size += children.Sum(sub => sub.Size);
            node.FileCount += children.Sum(sub => sub.FileCount);
            node.Nodes.Clear();
            node.Nodes.AddRange(children);
            node.Text = node.Directory.Name + " (" + (node.Size / (float)(1 << 20)).ToString("0.0") + " MB, " + node.FileCount + " files)";
        }

        string EscapeIgnore(string input)
        {
            return input.Replace(".", "\\.").Replace("*", ".*");
        }

        void QueryDirectory(DirectoryInfo dir, string[] ignores)
        {
            //1. Get ignore file
            string ignorePath = Path.Combine(dir.FullName, "ignore.txt");
            if (File.Exists(ignorePath))
            {
                //2. Append ingore file content to ignore list
                ignores = ignores.Concat(File.ReadAllLines(ignorePath).Select(EscapeIgnore)).ToArray();
            }
            //3. Query directory
            foreach (var subasstr in Directory.GetFileSystemEntries(dir.FullName))
            {
                FileSystemInfo sub;
                try
                {
                    if (File.Exists(subasstr))
                    {
                        sub = new FileInfo(subasstr);
                    }
                    else
                    {
                        sub = new DirectoryInfo(subasstr);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                bool add = true;
                foreach (var ignore in ignores)
                {
                    if (ignore.StartsWith("@contains:") && sub is DirectoryInfo)
                    {
                        var subdir = (DirectoryInfo)sub;
                        string regex = ignore.Substring("@contains:".Length);
                        if (subdir.EnumerateFileSystemInfos().Where(file => Regex.Match(file.Name, regex).Success).Count() > 0)
                        {
                            add = false;
                            break;
                        }
                    }
                    else
                    {
                        if (Regex.Match(sub.Name, ignore).Success)
                        {
                            add = false;
                            break;
                        }
                    }
                }
                if (add)
                {
                    if (sub is FileInfo)
                    {
                        FileList.Add((FileInfo)sub);
                    }
                    else
                    {
                        var subdir = (DirectoryInfo)sub;
                        if ((subdir.Attributes & FileAttributes.System) != FileAttributes.System)
                        {
                            QueryDirectory(subdir, ignores);
                        }
                    }
                }
            }
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                Task task = new Task(() =>
                {
                    long doneBytes = 0;
                    long doneFiles = 0;
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    Stopwatch totalElapsed = new Stopwatch();
                    totalElapsed.Start();
                    DirectoryInfo targetRootDir = new DirectoryInfo(folderBrowserDialog.SelectedPath);
                    foreach (var file in FileList.OrderBy(file => file.Length))
                    {
                        if (sw.ElapsedMilliseconds > 500)
                        {
                            sw.Restart();
                            this.Invoke(new Action(() =>
                            {
                                progressBar.Value = (int)(doneBytes * progressBar.Maximum / totalSize);
                                TimeSpan remaining = new TimeSpan((long)(totalElapsed.Elapsed.Ticks * ((double)(totalSize - doneBytes) / (double)doneBytes)));
                                lStatus.Text = $"{file.FullName}\n({doneFiles} / {FileList.Count}, {remaining} remaining)";
                            }));
                        }
                        string targetPath = Path.Combine(targetRootDir.FullName, file.FullName.Substring(RootDir.FullName.Length));
                        new FileInfo(targetPath).Directory.Create();
                        file.CopyTo(targetPath, true);
                        doneBytes += file.Length;
                        doneFiles++;
                    }
                    this.Invoke(new Action(() =>
                    {
                        progressBar.Value = progressBar.Maximum;
                        lStatus.Text = "Done!";
                    }));
                });
                task.Start();
            }
        }
    }
}
