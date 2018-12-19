﻿using System;
using System.Collections.Generic;
using CrossArc.Structs;
using System.IO;
using System.Diagnostics;
using Zstandard.Net;
using System.Runtime.InteropServices;
using System.IO.Compression;
using System.Windows.Forms;

namespace CrossArc
{
    public class ARC
    {
        public static string FilePath = "data.arc";

        public static _sArcHeader Header;
        public static _sBGMHashToName[] BGMHashToName;
        public static _sBGMNameToHash[] BGMNameToHash;
        public static _sBGMIndexToFile[] BGMIndexToFile;
        public static _sDirectoryOffsets[] DirectoryOffsets_1;
        public static _sDirectoryOffsets[] DirectoryOffsets_2;
        public static _sDirectoryList[] DirectoryLists;
        public static _sFileInformation[] FileInformation;
        public static _sFolderHashIndex[] HashFolderCounts;
        public static _SubFileInfo[] SubFileInformation_1;
        public static _SubFileInfo[] SubFileInformation_2;
        public static _sBGMOffset[] BGMOffsets;

        // for speed
        public static Dictionary<uint, _sDirectoryList> FolderHashDict;
        public static Dictionary<int, _sDirectoryOffsets> ChunkHash1 = new Dictionary<int, _sDirectoryOffsets>();
        public static Dictionary<int, _sDirectoryOffsets> ChunkHash2 = new Dictionary<int, _sDirectoryOffsets>();

        public static void Open()
        {
            if (!File.Exists(FilePath))
            {
                using (OpenFileDialog d = new OpenFileDialog())
                {
                    d.Title = "Select your data.arc file";
                    d.FileName = "data.arc";
                    d.Filter = "Smash Ultimate ARC (*.arc)|*.arc";

                    if(d.ShowDialog() == DialogResult.OK)
                    {
                        FilePath = d.FileName;
                    }
                    else
                    {
                        return;
                    }
                }
            }

            MemoryStream DecompTables = new MemoryStream();
            byte[] DecompTableData;
            using (BinaryReader R = new BinaryReader(new FileStream(FilePath, FileMode.Open)))
            {
                Header = ByteToType<_sArcHeader>(R);

                // Reading First Section
                R.BaseStream.Seek(Header.NodeSectionOffset, SeekOrigin.Begin);
                Debug.WriteLine(R.BaseStream.Position.ToString("X"));

                //compressed table
                //FileStream DecompTables = new FileStream("Tables", FileMode.Create);
                using (BinaryWriter writer = new BinaryWriter(DecompTables))
                {
                    while (R.ReadUInt32() == 0x10)
                    {
                        // grab tables
                        R.ReadInt32();
                        int CompSize = R.ReadInt32();
                        long NextTable = R.ReadInt32() + R.BaseStream.Position;
                        writer.Write(Decompress(R.ReadBytes(CompSize)));
                        R.BaseStream.Position = NextTable;
                        if (R.BaseStream.Position == R.BaseStream.Length) break;
                    }

                }
                DecompTableData = DecompTables.ToArray();
                if(DecompTableData.Length == 0)
                {
                    R.BaseStream.Seek(Header.NodeSectionOffset, SeekOrigin.Begin);
                    DecompTableData = (R.ReadBytes((int)(R.BaseStream.Length - R.BaseStream.Position)));
                }



                /*StreamWriter w = new StreamWriter(new FileStream("TEX.txt", FileMode.Create));
                foreach(_sFileInformation fi in FileInformation)
                {
                    //Console.WriteLine(GetName(fi.Hash2) + " " + GetName(fi.Unk2));
                    if (GetName(fi.Unk2).Equals("nutexb"))
                    {
                        // gottem
                        if (GetName(fi.Hash2).Contains("0x"))
                        {
                            FileOffsetGroup group = GetOffsetFromSubFile(fi);
                            byte[] data = GetFileData(R, group.ArcOffset, group.CompSize, group.DecompSize);
                            string s = "";
                            if (data.Length < 0x6C) continue;
                            for(int i = data.Length - 0x6C; i < data.Length; i++)
                            {
                                if (data[i] == 0) break;
                                s += (char)data[i];
                            }
                            w.WriteLine(s);
                            //Console.WriteLine(s + " " + GetName(fi.Unk2));
                        }
                    }
                }
                w.Close();;*/

                /*HashCheck();
                Console.WriteLine("Done");
                Console.WriteLine("Press enter to close");
                Console.ReadLine();
                return;*/
            }

            using (BinaryReader R = new BinaryReader(new MemoryStream(DecompTableData)))
            {
                R.BaseStream.Position = 0;
                _sNodeHeader NodeHeader = ByteToType<_sNodeHeader>(R);
                //PrintStruct<_sNodeHeader>(NodeHeader);

                R.BaseStream.Seek(0x68, SeekOrigin.Begin);

                //Hash Table
                //BGMHashToName = ReadArray<_sBGMHashToName>(R, NodeHeader.Part1Count);
                R.BaseStream.Seek(0x8 * NodeHeader.Part1Count, SeekOrigin.Current);

                // Hash Table 2
                //BGMNameToHash = ReadArray<_sBGMNameToHash>(R, NodeHeader.Part1Count);
                R.BaseStream.Seek(0xC * NodeHeader.Part1Count, SeekOrigin.Current);

                // Hash Table 3
                //BGMIndexToFile = ReadArray<_sBGMIndexToFile>(R, NodeHeader.Part1Count);
                R.BaseStream.Seek(0x4 * NodeHeader.Part2Count, SeekOrigin.Current);

                //Console.WriteLine(R.BaseStream.Position.ToString("X") + " " + NodeHeader.MusicFileCount.ToString("X"));
                BGMOffsets = ReadArray<_sBGMOffset>(R, NodeHeader.MusicFileCount);

                // Another Hash Table
                R.BaseStream.Seek(0xC * 0xE, SeekOrigin.Current);

                //folders

                Debug.WriteLine("FodlerHashes " + R.BaseStream.Position.ToString("X"));
                DirectoryLists = ReadArray<_sDirectoryList>(R, NodeHeader.FolderCount);

                //file offsets

                Debug.WriteLine("fileoffsets " + R.BaseStream.Position.ToString("X"));
                DirectoryOffsets_1 = ReadArray<_sDirectoryOffsets>(R, NodeHeader.FileCount1);
                DirectoryOffsets_2 = ReadArray<_sDirectoryOffsets>(R, NodeHeader.FileCount2);

                Debug.WriteLine("subfileoffset " + R.BaseStream.Position.ToString("X"));
                HashFolderCounts = ReadArray<_sFolderHashIndex>(R, NodeHeader.HashFolderCount);

                Debug.WriteLine("subfileoffset " + R.BaseStream.Position.ToString("X"));
                FileInformation = ReadArray<_sFileInformation>(R, NodeHeader.FileInformationCount);

                Debug.WriteLine("subfileoffset " + R.BaseStream.Position.ToString("X"));
                SubFileInformation_1 = ReadArray<_SubFileInfo>(R, NodeHeader.SubFileCount);
                SubFileInformation_2 = ReadArray<_SubFileInfo>(R, NodeHeader.SubFileCount2);

                _sHashInt[] HashInts = ReadArray<_sHashInt>(R, NodeHeader.FolderCount);

                Debug.WriteLine(R.BaseStream.Position.ToString("X"));

                Debug.WriteLine((Header.FileSectionOffset + DirectoryOffsets_1[510].Offset).ToString("X"));

                Debug.WriteLine(HashInts[HashInts.Length - 1].Index.ToString("X") + " " + HashInts[HashInts.Length - 1].Hash.ToString("X"));
                Debug.WriteLine("FolderHashes 0x" + DirectoryLists.Length.ToString("X"));
                Debug.WriteLine("Music Table 0x" + BGMOffsets.Length.ToString("X"));
                Debug.WriteLine("Chunks 1 0x" + DirectoryOffsets_1.Length.ToString("X"));
                Debug.WriteLine("Chunks 2 0x" + DirectoryOffsets_2.Length.ToString("X"));
                Debug.WriteLine("HashFolderCounts 0x" + HashFolderCounts.Length.ToString("X"));
                Debug.WriteLine("FileInformation 0x" + FileInformation.Length.ToString("X"));
                Debug.WriteLine("SubFile1 0x" + SubFileInformation_1.Length.ToString("X"));
                Debug.WriteLine("SubFile2 0x" + SubFileInformation_2.Length.ToString("X"));

                FolderHashDict = new Dictionary<uint, _sDirectoryList>();
                foreach (_sDirectoryList fh in DirectoryLists)
                {
                    FolderHashDict.Add(fh.HashID, fh);
                }

                foreach (_sDirectoryOffsets chunk in DirectoryOffsets_1)
                {
                    for (int i = 0; i < chunk.SubDataCount; i++)
                        if (!ChunkHash1.ContainsKey((int)chunk.SubDataStartIndex + i))
                            ChunkHash1.Add((int)chunk.SubDataStartIndex + i, chunk);
                }
                foreach (_sDirectoryOffsets chunk in DirectoryOffsets_2)
                {
                    for (int i = 0; i < chunk.SubDataCount; i++)
                        if (!ChunkHash2.ContainsKey((int)chunk.SubDataStartIndex + i))
                            ChunkHash2.Add((int)chunk.SubDataStartIndex + i, chunk);
                }


                /*using (StreamWriter o = new StreamWriter(new FileStream("Output.txt", FileMode.Create)))
                {
                    foreach (var item in FileInformation)
                    {
                        /*var Directory = DirectoryLists[item.DirectoryIndex >> 8];
                        var DirectoryOffset = DirectoryOffsets_1[(Directory.DirOffsetIndex>>8)];
                        _sDirectoryOffsets DirectoryOffset2 = DirectoryOffset;

                        var FileOffset = SubFileInformation_1[item.SubFile_Index];

                        if((DirectoryOffset.ResourceIndex & 0xFFFFFF) != 0xFFFFFF)
                        {
                            DirectoryOffset2 = DirectoryOffsets_2[(DirectoryOffset.ResourceIndex>>8) & 0xFFFFFF];
                        }
                        var Directory = DirectoryLists[item.DirectoryIndex >> 8];
                        var OffsetGroup = GetFileInformation(item);
                        o.WriteLine(GetName(item.Parent) + "/" + GetName(item.Hash2) +
                            " Offset: 0x" + OffsetGroup.ArcOffset.ToString("X") + 
                            " CompSize: 0x" + OffsetGroup.CompSize.ToString("X") + 
                            " DecompSize: 0x" + OffsetGroup.DecompSize.ToString("X") +
                            " Flag: 0x" + OffsetGroup.Flags.ToString("X"));
                        /*o.WriteLine(GetName(item.Hash2) + " " + GetName(Directory.NameHash) +
                            " Directory Offset: 0x" + (Header.FileSectionOffset + DirectoryOffset.Offset).ToString("X") +
                            (DirectoryOffset.Offset == DirectoryOffset2.Offset ? "" : " 0x" +
                            (Header.FileSectionOffset + DirectoryOffset2.Offset).ToString("X")) +
                            " Offset: 0x" + (FileOffset.Offset << 2).ToString("X") +
                            " Compsize: 0x" + FileOffset.CompSize.ToString("X") +
                            " Flags: 0x" + FileOffset.Flags.ToString("X") +
                            " " + item.SubFile_Index + " " + (FileOffset.Flags & 0xFFFFFF) + 
                            " " + DirectoryOffset.SubDataStartIndex + " - " + (DirectoryOffset.SubDataStartIndex + DirectoryOffset.SubDataCount));
                            
                    }
        }*/
            }
        }

        public static List<FileOffsetGroup> GetFiles()
        {
            List<FileOffsetGroup> Files = new List<FileOffsetGroup>();
            foreach (var item in FileInformation)
            {
                var Directory = DirectoryLists[item.DirectoryIndex >> 8];
                var OffsetGroup = GetFileInformation(item);
                OffsetGroup.FileName = GetName(item.Hash2);
                OffsetGroup.Path = GetName(item.Parent);
                Files.Add(OffsetGroup);
            }
            return Files;
        }

        public static FileOffsetGroup GetFileInformation(_sFileInformation FileInfo)
        {
            FileOffsetGroup g = new FileOffsetGroup();
            
            // Get Directoryies
            var Directory = DirectoryLists[FileInfo.DirectoryIndex >> 8];
            var DirectoryOffset = DirectoryOffsets_1[(Directory.DirOffsetIndex >> 8)];
            _sDirectoryOffsets DirectoryOffset2 = DirectoryOffset;

            if ((DirectoryOffset.ResourceIndex & 0xFFFFFF) != 0xFFFFFF)
            {
                DirectoryOffset2 = DirectoryOffsets_2[((DirectoryOffset.ResourceIndex - DirectoryOffsets_1.Length) & 0xFFFFFF)];
            }

            // Get File Data
            var FileOffset = SubFileInformation_1[FileInfo.SubFile_Index];

            // Parse Flags
            int flag = ((int)FileOffset.Flags >> 24);
            bool Compressed = (flag & 0x03) == 0x03;
            bool External = (flag & 0x08) == 0x08;
            int ExternalOffset = (int)FileOffset.Flags & 0xFFFFFF;

            if (ExternalOffset > 0 && !External)
            {
                return GetFileInformation(FileInformation[ExternalOffset]);
            }
            
            if (External)
            {
                //hack
                DirectoryOffset2 = ChunkHash2[ExternalOffset];
                FileOffset = SubFileInformation_2[ExternalOffset];
                /*while (ExternalOffset < DirectoryOffset2.SubDataStartIndex ||
                    ExternalOffset >= DirectoryOffset2.SubDataStartIndex + DirectoryOffset.SubDataCount)
                {
                    if ((DirectoryOffset2.ResourceIndex&0xFFFFFF) == 0xFFFFFF) break;
                    DirectoryOffset2 = DirectoryOffsets_2[((DirectoryOffset2.ResourceIndex - DirectoryOffsets_1.Length) & 0xFFFFFF)];
                }*/

                /*if (DirectoryOffset2.SubDataStartIndex > ExternalOffset || DirectoryOffset2.SubDataStartIndex + DirectoryOffset.SubDataCount < ExternalOffset)
                {
                    Console.WriteLine((DirectoryOffsets_1.Length + DirectoryOffsets_2.Length).ToString("X") + " " + 
                        ExternalOffset + " " + 
                        DirectoryOffset2.SubDataStartIndex + " " + 
                        (DirectoryOffset.ResourceIndex).ToString("X") + " " + 
                        DirectoryOffsets_2.Length.ToString("X"));
                }*/

                g.ArcOffset = (Header.FileSectionOffset + DirectoryOffset2.Offset + (FileOffset.Offset << 2));
                g.Offset = FileOffset.Offset;
                g.CompSize = FileOffset.CompSize;
                g.DecompSize = FileOffset.DecompSize;
                g.Flags = FileOffset.Flags;
            }
            else
            {
                g.ArcOffset = (Header.FileSectionOffset + DirectoryOffset.Offset + (FileOffset.Offset << 2));
                g.Offset = FileOffset.Offset;
                g.CompSize = FileOffset.CompSize;
                g.DecompSize = FileOffset.DecompSize;
                g.Flags = FileOffset.Flags;
            }

            return g;
        }

        public static string[] GetPaths()
        {
            List<string> paths = new List<string>();

            foreach(_sFolderHashIndex fhi in HashFolderCounts)
            {
                paths.Add(GetPathString(FolderHashDict, FolderHashDict[fhi.Hash]));
            }

            return paths.ToArray();
        }

        public static void CommandFunctions(string FolderToExtract)
        {
            using (BinaryReader R = new BinaryReader(new FileStream(FilePath, FileMode.Open)))
            {
                if (!FolderToExtract.Equals(""))
                {
                    _sDirectoryList ToExtract;
                    if (FolderHashDict.TryGetValue(CRC32.Crc32C(FolderToExtract), out ToExtract))
                    {
                        Console.WriteLine("Extracting " + GetPathString(FolderHashDict, ToExtract));
                        ExtractFolder(R, ToExtract);
                    }
                    else
                    {
                        foreach (_sDirectoryList h in DirectoryLists)
                        {
                            if (GetPathString(FolderHashDict, h).Equals(FolderToExtract))
                            {
                                ExtractFolder(R, h);
                                break;
                            }
                        }
                        Debug.WriteLine("Could no extract " + FolderToExtract);
                    }
                }
                else
                {
                    foreach (_sDirectoryList h in DirectoryLists)
                    {
                        Console.WriteLine("Extracting " + GetPathString(FolderHashDict, h));
                        ExtractFolder(R, h);
                    }
                }
            }

        }


        public static void HashCheck()
        {
            foreach (_sDirectoryList fh in DirectoryLists)
            {
                GetPathString(FolderHashDict, fh);
            }
            foreach (_sFileInformation fi in FileInformation)
            {
                GetName(fi.Hash2);
                GetName(fi.Extension);
                GetName(fi.Parent);
            }

            StreamWriter w = new StreamWriter(new FileStream("UsedString.txt", FileMode.Create));
            foreach (uint i in UsedHashes.Keys)
            {
                w.WriteLine(UsedHashes[i]);
            }
            w.Close();
            w = new StreamWriter(new FileStream("UnusedString.txt", FileMode.Create));
            foreach (uint i in MissingHashes.Keys)
            {
                w.WriteLine(i.ToString("X"));
            }
            w.Close();

            Console.WriteLine("Total Hashes: " + (MissingHashes.Count + UsedHashes.Count) + " Found: " + UsedHashes.Count + " Missing: " + MissingHashes.Count + " Percent Found: " + ((UsedHashes.Count) / (float)(UsedHashes.Count + MissingHashes.Count)) * 100);

        }

        public static _sFileInformation[] GetFileInformationFromFolder(_sDirectoryList FolderHash)
        {
            _sFileInformation[] info = new _sFileInformation[(int)FolderHash.FileInformationCount];
            for (int i = 0; i < FolderHash.FileInformationCount; i++)
            {
                info[i] = (FileInformation[FolderHash.FileInformationStartIndex + i]);
            }
            return info;
        }

        public static void ExtractFolder(BinaryReader Stream, _sDirectoryList FolderHash)
        {
            for (int i = 0; i < FolderHash.FileInformationCount; i++)
            {
                _sFileInformation info = FileInformation[FolderHash.FileInformationStartIndex + i];

                string FinalPath = GetPathString(FolderHashDict, FolderHash) + "/" + info.Parent.ToString("X");
                //Directory.CreateDirectory(FinalPath);

                _SubFileInfo sub = SubFileInformation_1[info.SubFile_Index];
                //Debug.WriteLine("\tFolder - " + info.Unk4.ToString("X"));
                //Debug.WriteLine("\t\t" + GetName(info.Hash2) + " " + sub.Flags.ToString("X"));

                GetName(info.Hash2);
                FileOffsetGroup group = GetOffsetFromSubFile(info);
                //Debug.WriteLine("\t\t\tArcOffset: 0x" + group.ArcOffset.ToString("X") + " Offset: " + group.Offset.ToString("X") + " CompSize: 0x" + group.CompSize.ToString("X") + " DecompSize:" + group.DecompSize.ToString("X") + " Flags: 0x" + group.Flags.ToString("X"));

                ExportFile(FinalPath + "/" + GetName(info.Hash2), Stream, group.ArcOffset, group.CompSize, group.DecompSize);
            }
        }

        public static void ExportFile(string filepath, BinaryReader R, long Offset, long CompSize, long DecompSize)
        {
            File.WriteAllBytes(filepath, GetFileData(R, Offset, CompSize, DecompSize));
        }

        private static byte[] GetFileData(BinaryReader R, long Offset, long CompSize, long DecompSize)
        {
            R.BaseStream.Seek(Offset, SeekOrigin.Begin);

            byte[] data = R.ReadBytes((int)CompSize);

            if (CompSize != DecompSize && DecompSize > 0 && CompSize > 0)
            {
                try
                {
                    data = Decompress(data);
                }
                catch (IOException)
                {
                    Console.WriteLine("Error: Could Not Decompress File ");
                }
            }

            if (DecompSize != data.Length)
                Console.WriteLine("Error: Filesize mismatch ");

            return data;
        }


        public static byte[] GetFileData(long Offset, long CompSize, long DecompSize)
        {
            using (BinaryReader R = new BinaryReader(new FileStream(FilePath, FileMode.Open)))
            {
                return GetFileData(R, Offset, CompSize, DecompSize);
            }
        }

        public static byte[] Decompress(byte[] compressed)
        {
            using (var memoryStream = new MemoryStream(compressed))
            using (var compressionStream = new ZstandardStream(memoryStream, CompressionMode.Decompress))
            using (var temp = new MemoryStream())
            {
                compressionStream.CopyTo(temp);
                return temp.ToArray();
            }
        }


        public struct FileOffsetGroup
        {
            public long ArcOffset;
            public long Offset;
            public long CompSize;
            public long DecompSize;
            public uint Flags;
            public string Path;
            public string FileName;
        }

        public static FileOffsetGroup GetOffsetFromSubFile(_sFileInformation FileInfo)
        {
            FileOffsetGroup g = new FileOffsetGroup();
            int SubIndex = (int)FileInfo.SubFile_Index;
            _SubFileInfo subfile = SubFileInformation_1[SubIndex];

            int flag = ((int)subfile.Flags >> 24);
            bool UseTable2 = (flag & 0xFF) == 0x08 || (flag & 0xFF) == 0x0B;
            int ExternalOffset = (int)subfile.Flags & 0xFFFFFF;

            if (flag == 0x03 && ExternalOffset > 0)
            {
                //Debug.WriteLine("\tEmpty?------------");
                //Debug.WriteLine("\t\t" + subfile.Offset.ToString("X") + " " + subfile.CompSize.ToString("X") + " " + subfile.DecompSize.ToString("X"));
            }

            if (ExternalOffset > 0 && !UseTable2)
            {
                //Debug.WriteLine("\t\t External ->" + GetName(FileInformation[ExternalOffset].Hash2));
                return GetOffsetFromSubFile(FileInformation[ExternalOffset]);
            }
            Dictionary<int, _sDirectoryOffsets> Offsets = ChunkHash1;
            if (UseTable2)
            {
                Offsets = ChunkHash2;
                SubIndex = (int)(subfile.Flags & 0xFFFFFF);
                subfile = SubFileInformation_2[SubIndex];
            }

            //foreach (_sFileChunkOffsets chunk in Offsets)
            _sDirectoryOffsets chunk = Offsets[SubIndex];
            {
                if (SubIndex >= chunk.SubDataStartIndex && SubIndex < chunk.SubDataStartIndex + chunk.SubDataCount)
                {
                    g.ArcOffset = (Header.FileSectionOffset + chunk.Offset + (subfile.Offset << 2));
                    g.Offset = subfile.Offset;// (Header.FileSectionOffset + chunk.Offset + (subfile.Offset << 2));
                    g.CompSize = subfile.CompSize;
                    g.DecompSize = subfile.DecompSize;
                    g.Flags = subfile.Flags;
                    //break;
                }
            }

            return g;
        }

        public static string GetPathString(Dictionary<uint, _sDirectoryList> HashBank, _sDirectoryList Folder)
        {
            string Folder1 = "";
            string Folder2 = "";
            string Folder3 = "";

            if (Folder.HashID != Folder.NameHash && Folder.NameHash != 0 && HashBank.ContainsKey(Folder.NameHash))
                Folder3 = GetPathString(HashBank, HashBank[Folder.NameHash]) + "/";
            else if (Folder.NameHash != 0)
                Folder3 = GetName(Folder.NameHash);

            if (HashBank.ContainsKey(Folder.Hash4))
                Folder2 = GetPathString(HashBank, HashBank[Folder.Hash4]) + "/";
            else if (Folder.Hash4 != 0)
                Folder3 = GetName(Folder.Hash4);

            /*if (Folder.Hash4 != 0 && Folder.Hash4 != 0 && HashBank.ContainsKey(Folder.Hash4))
                Folder1 = GetPathString(HashBank, HashBank[Folder.Hash4]) + "/";
            else if (Folder.Hash4 != 0)
                Folder3 = GetName(Folder.Hash4);*/

            return Folder1 + Folder2 + Folder3;
        }


        public static Dictionary<uint, string> MissingHashes = new Dictionary<uint, string>();
        public static Dictionary<uint, string> UsedHashes = new Dictionary<uint, string>();
        public static string GetName(uint Hash)
        {
            string name = "";
            if (HashDict.TryGetValue(Hash, out name))
            {
                //if (!UsedHashes.ContainsKey(Hash))
                //    UsedHashes.Add(Hash, name);
                return name;
            }

            //if (!MissingHashes.ContainsKey(Hash))
            //    MissingHashes.Add(Hash, name);
            return "0x" + Hash.ToString("X");
        }



        public static T[] ReadArray<T>(BinaryReader reader, uint Size)
        {
            // slow af but whatevs
            T[] arr = new T[Size];
            for (int i = 0; i < Size; i++)
            {
                arr[i] = ByteToType<T>(reader);
            }
            return arr;
        }

        public static T ByteToType<T>(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(Marshal.SizeOf(typeof(T)));

            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            T theStructure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();

            return theStructure;
        }
    }
}
