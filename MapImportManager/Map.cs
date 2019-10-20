using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

static class Map {
    const byte
        CustomFlag = 0x0D,
        NulByte = 0x00;

    static readonly ArchType[] archTypes = {
        new ArchType { DetectFile = "war3map.j", ImpFile = "war3map.imp" },
        new ArchType { DetectFile = "war3campaign.w3f", ImpFile = "war3campaign.imp" },
    };

    public static List<ImportFile> GetImportList(string mapPath) {
        List<ImportFile> result = null;
        IntPtr hMpq;
        if(StormLib.SFileOpenArchive(mapPath, 0u, 0u, out hMpq)) {
            var impFile = DetectImpFile(hMpq);
            if(impFile != null)
                result = ReadImportFile(hMpq, impFile);
        }
        if(hMpq != IntPtr.Zero)
            StormLib.SFileCloseArchive(hMpq);

        return result;
    }

    static List<ImportFile> ReadImportFile(IntPtr hMpq, string impFile) {
        List<ImportFile> result = null;

        IntPtr hFile;
        if(StormLib.SFileOpenFileEx(hMpq, impFile, 0u, out hFile)) {
            var fSize = StormLib.SFileGetFileSize(hFile, 0u);
            if(fSize < 0xFFFFFFFFu) {
                var buffer = Marshal.AllocHGlobal((int)fSize);
                if(StormLib.SFileReadFile(hFile, buffer, fSize, 0u, IntPtr.Zero)) {
                    int offset = 0;
                    var version = Marshal.ReadInt32(buffer, offset);
                    if(version < 2) {
                        offset += sizeof(int);
                        var fileCount = Marshal.ReadInt32(buffer, offset);
                        offset += sizeof(int);
                        result = new List<ImportFile>(fileCount);
                        for(int i = 0; i < fileCount; i++) {
                            var bt = (version > 0) && ((Marshal.ReadByte(buffer, offset++) & 1) > NulByte);
                            var strLen = SeekStrLen(buffer, offset, fSize);
                            var data = new byte[strLen];
                            Marshal.Copy(buffer + offset, data, 0, data.Length);
                            offset += strLen + 1;
                            result.Add(new ImportFile(Encoding.UTF8.GetString(data), bt));
                        }
                    }
                }
                Marshal.FreeHGlobal(buffer);
            } else
                result = new List<ImportFile>();
        }
        if(hFile != IntPtr.Zero)
            StormLib.SFileCloseFile(hFile);

        return result;
    }

    public static SaveResult SaveImportList(string mapPath, List<ImportFile> fileList) {
        var result = new SaveResult();
        var saved = true;
        IntPtr hMpq;
        if(saved &= StormLib.SFileOpenArchive(mapPath, 0u, 0u, out hMpq)) {
            var failedFiles = new List<string>();
            for(int i = 0; (i < fileList.Count) && saved; i++) {
                var file = fileList[i];
                if(file.Deleted) {
                    StormLib.SFileRemoveFile(hMpq, file.OrigPath, 0u);
                } else if(file.Changed) {
                    if(string.IsNullOrEmpty(file.DiskPath)) {
                        StormLib.SFileRenameFile(hMpq, file.OrigPath, file.Path);
                    } else {
                        StormLib.SFileRemoveFile(hMpq, file.OrigPath, 0u);
                        if(!StormLib.SFileAddFileEx(hMpq, file.DiskPath, file.Path, 0x80000200u, 0x2u, 0x2u)) {
                            failedFiles.Add(file.DiskPath);
                            file.Deleted = true;
                        }
                    }
                }
            }
            result.FailedFiles = failedFiles;
            
            var impFile = DetectImpFile(hMpq);
            if(saved &= (impFile != null))
                saved &= WriteImportFile(hMpq, impFile, fileList);

            StormLib.SFileCompactArchive(hMpq, null, false);
        }
        if(saved &= (hMpq != IntPtr.Zero))
            saved &= StormLib.SFileCloseArchive(hMpq);

        result.Saved = saved;
        return result;
    }

    static bool WriteImportFile(IntPtr hMpq, string impFile, List<ImportFile> fileList) {
        var data = GenImpFile(fileList);
        if(data == null)
            return StormLib.SFileRemoveFile(hMpq, impFile, 0u);

        var buffer = Marshal.AllocHGlobal(data.Length);
        Marshal.Copy(data, 0, buffer, data.Length);

        var result = true;
        IntPtr hFile;
        if(result &= StormLib.SFileCreateFile(hMpq, impFile, 0ul, (uint)data.Length, 0u, 0x80000200u, out hFile))
            result &= StormLib.SFileWriteFile(hFile, buffer, (uint)data.Length, 0x2u);

        if(result &= (hFile != IntPtr.Zero))
            result &= StormLib.SFileFinishFile(hFile);

        Marshal.FreeHGlobal(buffer);

        return result;
    }

    static byte[] GenImpFile(List<ImportFile> fileListSrc) {
        var fileList = new List<ImportFile>(fileListSrc.Count);
        for(int i = 0; i < fileListSrc.Count; i++) {
            var file = fileListSrc[i];
            if(!file.Deleted)
                fileList.Add(file);
        }
        
        if(fileList.Count == 0)
            return null;

        byte[] data = null;
        using(var stream = new MemoryStream()) {
            using(var writer = new BinaryWriter(stream, Encoding.UTF8, true)) {
                writer.Write(1);
                writer.Write(fileList.Count);
                for(int i = 0; i < fileList.Count; i++) {
                    var file = fileList[i];
                    writer.Write(file.Custom ? CustomFlag : NulByte);
                    var str = Encoding.UTF8.GetBytes(file.InnerPath);
                    stream.Write(str, 0, str.Length);
                    writer.Write(NulByte);
                }
                writer.Close();
            }
            data = stream.ToArray();
            stream.Close();
        }
        return data;
    }

    static string DetectImpFile(IntPtr hMpq) {
        string impFile = null;
        for(int i = 0; (i < archTypes.Length) && (impFile == null); i++) {
            var archType = archTypes[i];
            IntPtr hFile;
            if(StormLib.SFileOpenFileEx(hMpq, archType.DetectFile, 0u, out hFile) && (StormLib.SFileGetFileSize(hFile, 0u) < 0xFFFFFFFFu))
                impFile = archType.ImpFile;

            if(hFile != IntPtr.Zero)
                StormLib.SFileCloseFile(hFile);
        }
        return impFile;
    }

    static int SeekStrLen(IntPtr mem, int startPos, uint maxPos) {
        for(int i = startPos; i < maxPos; i++)
            if(Marshal.ReadByte(mem, i) == 0x00)
                return i - startPos;

        return 0;
    }

    public static void Export(string mapPath, List<ImportFile> fileList, string destPath) {
        IntPtr hMpq;
        if(StormLib.SFileOpenArchive(mapPath, 0u, 0u, out hMpq)) {
            for(int i = 0; i < fileList.Count; i++) {
                var file = fileList[i];
                if(!string.IsNullOrEmpty(file.OrigPath)) {
                    var fp = Path.Combine(destPath, file.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(fp));
                    StormLib.SFileExtractFile(hMpq, file.OrigPath, fp, 0u);
                }
            }
        }
        if(hMpq != IntPtr.Zero)
            StormLib.SFileCloseArchive(hMpq);
    }

    public static void Load() {
        Marshal.PrelinkAll(typeof(StormLib));
    }

    class ArchType {
        public string DetectFile, ImpFile;
    }
}

public class ImportFile {
    const string war3mapImported = @"war3mapImported\";

    public string Path {
        get => (customFlag ? filePath : $"{war3mapImported}{filePath}");
        set {
            if(value.StartsWith(war3mapImported)) {
                filePath = value.Substring(war3mapImported.Length);
                customFlag = false;
            } else {
                filePath = value;
                customFlag = true;
            }
        }
    }

    public bool Custom { get => customFlag; }
    public bool Changed { get; set; }
    public bool Deleted { get; set; }
    public string DiskPath { get; set; }
    public string InnerPath { get => filePath; }
    public string OrigPath { get; }

    string filePath;
    bool customFlag;

    public ImportFile(string path, bool custom) {
        filePath = path;
        customFlag = custom;
        OrigPath = Path;

        if(filePath.StartsWith(war3mapImported))
            filePath = filePath.Substring(war3mapImported.Length);
    }

    public ImportFile(string path, string diskPath) {
        filePath = path;
        DiskPath = diskPath;
        customFlag = true;
        Changed = true;
    }
}

class SaveResult {
    public bool Saved;
    public List<string> FailedFiles;
}

static class StormLib {
    [DllImport("stormlib.dll", ExactSpelling = true, SetLastError = true, ThrowOnUnmappableChar = false)]
    public static extern bool SFileOpenArchive([MarshalAs(UnmanagedType.LPTStr)] string szMpqName, uint dwPriority, uint dwFlags, out IntPtr phMpq);

    [DllImport("stormlib.dll", ExactSpelling = true, SetLastError = true, ThrowOnUnmappableChar = false)]
    public static extern bool SFileCloseArchive(IntPtr hMpq);

    [DllImport("stormlib.dll", ExactSpelling = true, SetLastError = true, ThrowOnUnmappableChar = false)]
    public static extern bool SFileOpenFileEx(IntPtr hMpq, [MarshalAs(UnmanagedType.LPStr)] string szFileName, uint dwSearchScope, out IntPtr phFile);

    [DllImport("stormlib.dll", ExactSpelling = true, SetLastError = true, ThrowOnUnmappableChar = false)]
    public static extern uint SFileGetFileSize(IntPtr hFile, uint pdwFileSizeHigh);

    [DllImport("stormlib.dll", ExactSpelling = true, SetLastError = true, ThrowOnUnmappableChar = false)]
    public static extern bool SFileReadFile(IntPtr hFile, IntPtr lpBuffer, uint dwToRead, uint pdwRead, IntPtr lpOverlapped);

    [DllImport("stormlib.dll", ExactSpelling = true, SetLastError = true, ThrowOnUnmappableChar = false)]
    public static extern bool SFileCloseFile(IntPtr hFile);

    [DllImport("stormlib.dll", ExactSpelling = true, SetLastError = true, ThrowOnUnmappableChar = false)]
    public static extern bool SFileRenameFile(IntPtr hMpq, [MarshalAs(UnmanagedType.LPStr)] string szOldFileName, [MarshalAs(UnmanagedType.LPStr)] string szNewFileName);

    [DllImport("stormlib.dll", ExactSpelling = true, SetLastError = true, ThrowOnUnmappableChar = false)]
    public static extern bool SFileCreateFile(IntPtr hMpq, [MarshalAs(UnmanagedType.LPStr)] string szArchiveName, ulong fileTime, uint dwFileSize, uint lcLocale, uint dwFlags, out IntPtr phFile);

    [DllImport("stormlib.dll", ExactSpelling = true, SetLastError = true, ThrowOnUnmappableChar = false)]
    public static extern bool SFileWriteFile(IntPtr hFile, IntPtr pvData, uint dwSize, uint dwCompression);

    [DllImport("stormlib.dll", ExactSpelling = true, SetLastError = true, ThrowOnUnmappableChar = false)]
    public static extern bool SFileFinishFile(IntPtr hFile);

    [DllImport("stormlib.dll", ExactSpelling = true, SetLastError = true, ThrowOnUnmappableChar = false)]
    public static extern bool SFileAddFileEx(IntPtr hMpq, [MarshalAs(UnmanagedType.LPTStr)] string szFileName, [MarshalAs(UnmanagedType.LPStr)] string szArchivedName, uint dwFlags, uint dwCompression, uint dwCompressionNext);

    [DllImport("stormlib.dll", ExactSpelling = true, SetLastError = true, ThrowOnUnmappableChar = false)]
    public static extern bool SFileRemoveFile(IntPtr hMpq, [MarshalAs(UnmanagedType.LPStr)] string szFileName, uint dwSearchScope);

    [DllImport("stormlib.dll", ExactSpelling = true, SetLastError = true, ThrowOnUnmappableChar = false)]
    public static extern bool SFileCompactArchive(IntPtr hMpq, [MarshalAs(UnmanagedType.LPStr)] string szListFile, bool bReserved);

    [DllImport("stormlib.dll", ExactSpelling = true, SetLastError = true, ThrowOnUnmappableChar = false)]
    public static extern bool SFileExtractFile(IntPtr hMpq, [MarshalAs(UnmanagedType.LPStr)] string szToExtract, [MarshalAs(UnmanagedType.LPTStr)] string szExtracted, uint dwSearchScope);
}
