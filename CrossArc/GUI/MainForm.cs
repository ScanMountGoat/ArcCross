﻿using ArcCross;
using CrossArc.GUI.Nodes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Windows.Forms;
using System.Configuration;

namespace CrossArc.GUI
{
    public partial class MainForm : Form
    {
        public string FilePath;
        public int Version;
        public static int SelectedRegion
        {
            get
            {
                return int.Parse(ConfigurationManager.AppSettings["Region"]);
            }
            set
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings["Region"].Value = value.ToString();
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
        }

        public static ARC ArcFile;
    
        public static ContextMenu NodeContextMenu;

        private GuiNode Root;

        private BackgroundWorker searchWorker;

        public Dictionary<string, FileInformation> pathToFileInfomation = new Dictionary<string, FileInformation>();

        public MainForm()
        {
            InitializeComponent();

            ArcFile = new ARC();

            treeView1.BeforeExpand += folderTree_BeforeExpand;

            treeView1.NodeMouseClick += (sender, args) => treeView1.SelectedNode = args.Node;

            treeView1.HideSelection = false;

            treeView1.ImageList = new ImageList();
            treeView1.ImageList.Images.Add("folder", Properties.Resources.folder);
            treeView1.ImageList.Images.Add("file", Properties.Resources.file);
            
            comboBox1.SelectedIndex = SelectedRegion;

            comboBox1.SelectedIndexChanged += (sender, args) =>
            {
                SelectedRegion = comboBox1.SelectedIndex;
                treeView1_AfterSelect(null, null);
            };

            NodeContextMenu = new ContextMenu();

            {
                MenuItem item = new MenuItem("Extract file(s)");
                item.Click += ExtractFile;
                NodeContextMenu.MenuItems.Add(item);
            }
            {
                MenuItem item = new MenuItem("Extract file(s) (Compressed)");
                item.Click += ExtractFileComp;
                NodeContextMenu.MenuItems.Add(item);
            }
            {
                MenuItem item = new MenuItem("Extract file(s) (With Offset)");
                item.Click += ExtractFileOffset;
                NodeContextMenu.MenuItems.Add(item);
            }
            {
                MenuItem item = new MenuItem("Extract file(s) (Compressed, With Offset)");
                item.Click += ExtractFileCompOffset;
                NodeContextMenu.MenuItems.Add(item);
            }
        }

        private void ExtractFile(object sender, EventArgs args)
        {
            if(treeView1.SelectedNode != null && treeView1.SelectedNode is GuiNode n && n.Base is FileNode file)
            {
                ProgressBar bar = new ProgressBar();
                bar.Show();
                bar.Extract(new FileNode[] { file });
            }
            if (treeView1.SelectedNode != null && treeView1.SelectedNode is GuiNode n2 && n2.Base is FolderNode folder)
            {
                ProgressBar bar = new ProgressBar();
                bar.Show();
                bar.Extract(folder.GetAllFiles());
            }
        }

        private void ExtractFileComp(object sender, EventArgs args)
        {
            if (treeView1.SelectedNode != null && treeView1.SelectedNode is GuiNode n && n.Base is FileNode file)
            {
                ProgressBar bar = new ProgressBar();
                bar.DecompressFiles = false;
                bar.Show();
                bar.Extract(new FileNode[] { file });
            }
            if (treeView1.SelectedNode != null && treeView1.SelectedNode is GuiNode n2 && n2.Base is FolderNode folder)
            {
                ProgressBar bar = new ProgressBar();
                bar.DecompressFiles = false;
                bar.Show();
                bar.Extract(folder.GetAllFiles());
            }
        }

        private void ExtractFileOffset(object sender, EventArgs args)
        {
            if (treeView1.SelectedNode != null && treeView1.SelectedNode is GuiNode n && n.Base is FileNode file)
            {
                ProgressBar bar = new ProgressBar();
                bar.UseOffsetName = true;
                bar.Show();
                bar.Extract(new FileNode[] { file });
            }
            if (treeView1.SelectedNode != null && treeView1.SelectedNode is GuiNode n2 && n2.Base is FolderNode folder)
            {
                ProgressBar bar = new ProgressBar();
                bar.UseOffsetName = true;
                bar.Show();
                bar.Extract(folder.GetAllFiles());
            }
        }

        private void ExtractFileCompOffset(object sender, EventArgs args)
        {
            if (treeView1.SelectedNode != null && treeView1.SelectedNode is GuiNode n && n.Base is FileNode file)
            {
                ProgressBar bar = new ProgressBar();
                bar.UseOffsetName = true;
                bar.DecompressFiles = false;
                bar.Show();
                bar.Extract(new FileNode[] { file });
            }
            if (treeView1.SelectedNode != null && treeView1.SelectedNode is GuiNode n2 && n2.Base is FolderNode folder)
            {
                ProgressBar bar = new ProgressBar();
                bar.UseOffsetName = true;
                bar.DecompressFiles = false;
                bar.Show();
                bar.Extract(folder.GetAllFiles());
            }
        }

        private void folderTree_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node is GuiNode n)
                n.BeforeExpand();
        }

        private void openARCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.FileName = "data.arc";
                d.Filter += "Smash Ultimate ARC|*.arc";
                if(d.ShowDialog() == DialogResult.OK)
                {
                    Cursor.Current = Cursors.WaitCursor;

                    Stopwatch s = new Stopwatch();
                    s.Start();
                    ArcFile.InitFileSystem(d.FileName);
                    System.Diagnostics.Debug.WriteLine("parse arc: " + s.Elapsed.Milliseconds);
                    s.Restart();
                    InitFileSystem();
                    System.Diagnostics.Debug.WriteLine("init nodes: " + s.Elapsed.Milliseconds);
                    s.Restart();

                    Cursor.Current = Cursors.Arrow;
                    label1.Text = "Arc Version: " + ArcFile.Version.ToString("X");

                    updateHashesToolStripMenuItem.Enabled = false;

                    Version = ArcFile.Version;
                    FilePath = d.FileName;

                    HashDict.Unload();
                }
            }
        }

        private void InitFileSystem()
        {
            treeView1.Nodes.Clear();
            FolderNode root = new FolderNode("root");
            foreach (var file in ArcFile.GetFileList())
            {
                string[] path = file.Split('/');
                ProcessFile(root, path, 0);

            }
            foreach (var file in ArcFile.GetStreamFileList())
            {
                string[] path = file.Split('/');
                ProcessFile(root, path, 0);
            }
            Root = new GuiNode(root);
            treeView1.Nodes.Add(Root);
        }

        private void ProcessFile(FolderNode parent, string[] path, int index)
        {
            if(path.Length - 1 == index)
            {
                var FileNode = new FileNode(path[index]);
                parent.AddChild(FileNode);
                return;
            }

            FolderNode node = null;
            string folderName = path[index];
            foreach(var f in parent.Children)
            {
                if (f.Text.Equals(folderName))
                {
                    node = (FolderNode)f;
                    break;
                }
            }

            if(node == null)
            {
                node = new FolderNode(folderName);
                parent.AddChild(node);
            }
            
            ProcessFile(node, path, index + 1);
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            propertyGrid1.SelectedObject = null;
            if (treeView1.SelectedNode != null && treeView1.SelectedNode is GuiNode n && n.Base is FileNode file)
            {
                propertyGrid1.SelectedObject = file.FileInformation;
            }
        }

        private bool DownloadingHashes = false;

        private void updateHashesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (DownloadingHashes)
                return;
            var dl = MessageBox.Show("", "Download Hashes?", MessageBoxButtons.YesNo);

            if(dl == DialogResult.Yes)
            using (var client = new WebClient())
            {
                Cursor.Current = Cursors.WaitCursor;
                MessageBox.Show("Downloading Hashes please wait...");
                DownloadingHashes = true;
                DownloadHashes();
                DownloadingHashes = false;
                Cursor.Current = Cursors.Arrow;
            }
        }

        private void DownloadHashes()
        {
            using (var client = new WebClient())
            {
                client.DownloadFile("https://github.com/ultimate-research/archive-hashes/raw/master/Hashes", "Hashes.txt");
            }
        }


        private object lockTree = new object();

        private void searchBox_TextChanged(object sender, EventArgs e)
        {
            if (!ArcFile.Initialized || Root == null)
                return;
            lock(lockTree)
            {
                if (searchWorker != null)
                {
                    searchWorker.CancelAsync();
                    searchWorker.Dispose();
                    searchWorker = null;
                }

                treeView1.Nodes.Clear();

                if (searchBox.Text == "")
                {
                    treeView1.Nodes.Add(Root);
                    searchLabel.Visible = false;
                }
                else
                {
                    searchWorker = new BackgroundWorker();
                    searchWorker.DoWork += new DoWorkEventHandler(Search);
                    searchWorker.ProgressChanged += new ProgressChangedEventHandler(AddNode);
                    searchWorker.WorkerSupportsCancellation = true;
                    searchWorker.WorkerReportsProgress = true;
                    searchWorker.RunWorkerAsync();
                    searchLabel.Visible = true;
                }
            }
        }

        private void AddNode(object sender, ProgressChangedEventArgs args)
        {
            lock (lockTree)
            {
                if(searchWorker != null)
                {
                    if (args.ProgressPercentage == 100)
                    {
                        System.Diagnostics.Debug.WriteLine("Done Searching");
                        searchLabel.Visible = false;
                    }
                    else
                        treeView1.Nodes.Add(new GuiNode((BaseNode)args.UserState));
                }
            }
        }
        
        private void Search(object sender, DoWorkEventArgs e)
        {
            Queue<BaseNode> toSearch = new Queue<BaseNode>();
            toSearch.Enqueue(Root.Base);

            long value;

            bool interrupted = false;

            var key = searchBox.Text;
            if (key == "0")
                return;

            while (toSearch.Count > 0)
            {
                lock (lockTree)
                {
                    if (searchBox != null && key != searchBox.Text || searchWorker == null || searchWorker.CancellationPending)
                    {
                        interrupted = true;
                        break;
                    }

                    var s = toSearch.Dequeue();

                    if (s.Text.Contains(key))
                    {
                        searchWorker.ReportProgress(0, s);
                    }
                    if (s is FileNode file &&
                        key.Length >= 3 &&
                        key.StartsWith("0x") && 
                        long.TryParse(
                        key.Substring(2, key.Length - 2),
                              NumberStyles.HexNumber, 
                              CultureInfo.InvariantCulture
                              , out value)
                              && file.FileInformation.Offset == value)
                    {
                        searchWorker.ReportProgress(0, s);
                    }

                    foreach (var b in s.SubNodes)
                    {
                        toSearch.Enqueue(b);
                    }
                }
            }

            if(!interrupted)
                searchWorker.ReportProgress(100, null);
        }
        
        private void SearchCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // add other code here
            /*if (e.Cancelled && restartWorker)
            {
                restartWorker = false;
                searchWorker.RunWorkerAsync();
            }*/
        }

        private void ExportAll(string key)
        {
            /*Queue<BaseNode> toSearch = new Queue<BaseNode>();
            toSearch.Enqueue(Root);
            List<FileNode> toExport = new List<FileNode>();

            while (toSearch.Count > 0)
            {

                var s = toSearch.Dequeue();

                if (s.Text.Contains(key))
                {
                    if (s is FileNode fn)
                        toExport.Add(fn);
                }

                foreach (var b in s.BaseNodes)
                {
                    toSearch.Enqueue(b);
                }
            }

            ProgressBar bar = new ProgressBar();
            bar.Show();
            bar.Extract(toExport.ToArray());*/
        }
        
    }
}
