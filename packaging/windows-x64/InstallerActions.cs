using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using WixToolset.Dtf.WindowsInstaller;
using FileAttributes = System.IO.FileAttributes;

public static class InstallerActions
{
    public static string InstallationDirectory(string selected, string previous)
    {
        string path = Path.GetFullPath(selected);
        string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(trimmed, previous.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(trimmed), "Rurimecha", StringComparison.OrdinalIgnoreCase))
            return path;
        return Path.Combine(path, "Rurimecha");
    }

    [CustomAction]
    public static ActionResult NormalizeInstallFolder(Session session)
    {
        try
        {
            string selected = session["INSTALLFOLDER"];
            string target = InstallationDirectory(selected, session["RURI_PREVIOUS_INSTALLFOLDER"]);
            if (!string.Equals(selected.TrimEnd('\\'), target.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                session.SetTargetPath("INSTALLFOLDER", target);
            return ActionResult.Success;
        }
        catch (Exception error) { return Fail(session, "无法使用所选安装位置。请选择可写的文件夹后重试。", error); }
    }

    private static string DataDirectory()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rurimecha", "data");
    }

    private const string NetworkResetError = "无法重置旧的设备连接设置。请从系统托盘退出 Ruri，确认应用数据目录可写且未使用链接，再重新安装。";

    [CustomAction]
    public static ActionResult PrepareNetworkReset(Session session)
    {
        try
        {
            string directory = DataDirectory();
            if (session["RollbackDisabled"] != "" && HasDataDirectory(new DirectoryInfo(directory)) &&
                IsRegularFile(Path.Combine(directory, "network.json")))
                return Fail(session, "请启用 Windows 安装回滚，以便在安装失败时保留原设备连接设置。", null);

            var data = new CustomActionData();
            data.Add("Directory", directory);
            data.Add("Backup", Path.Combine(directory, ".msi-network-" + Guid.NewGuid().ToString("N")));
            string payload = data.ToString();
            session["ResetNetwork"] = payload;
            session["RollbackNetworkReset"] = payload;
            session["CompleteNetworkReset"] = payload;
            return ActionResult.Success;
        }
        catch (Exception error) { return Fail(session, NetworkResetError, error); }
    }

    [CustomAction]
    public static ActionResult ResetNetwork(Session session)
    {
        try
        {
            var data = session.CustomActionData;
            string directory = data["Directory"];
            if (!HasDataDirectory(new DirectoryInfo(directory))) return ActionResult.Success;
            string path = Path.Combine(directory, "network.json");
            if (IsRegularFile(path))
            {
                string lockPath = Path.Combine(directory, "core.lock");
                using (var gate = IsRegularFile(lockPath) ? File.Open(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null)
                    File.Move(path, data["Backup"]);
            }
            return ActionResult.Success;
        }
        catch (Exception error) { return Fail(session, NetworkResetError, error); }
    }

    [CustomAction]
    public static ActionResult RollbackNetworkReset(Session session)
    {
        try
        {
            var data = session.CustomActionData;
            string directory = data["Directory"];
            if (HasDataDirectory(new DirectoryInfo(directory)) && IsRegularFile(data["Backup"]))
                File.Move(data["Backup"], Path.Combine(directory, "network.json"));
            return ActionResult.Success;
        }
        catch (Exception error) { return Fail(session, "无法恢复安装前的设备连接设置。请从系统托盘退出 Ruri，再重新安装。", error); }
    }

    [CustomAction]
    public static ActionResult CompleteNetworkReset(Session session)
    {
        try
        {
            var data = session.CustomActionData;
            if (HasDataDirectory(new DirectoryInfo(data["Directory"])) && IsRegularFile(data["Backup"]))
                File.Delete(data["Backup"]);
            return ActionResult.Success;
        }
        catch (Exception error) { return Fail(session, NetworkResetError, error); }
    }

    // Only these Core-owned locations are eligible for removal. Additional files
    // in the data directory may belong to the user, even inside a cache folder.
    private static readonly Regex CoreDataFolders = new Regex(
        @"\A(?:ai-gateway|audio-preparation|images|license|logs|tls|transcodes|" +
        @"voice-catalog(?:/(?:covers|profiles))?|media-tools(?:/(?:ffmpeg-[\w.-]+|\.preparing-\d+))?|" +
        @"core\.db\.before-v\d+-\d+)\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CoreDataFiles = new Regex(@"\A(?:" +
        @"core\.lock|(?:core|scrape-results|media-results)\.db(?:-wal|-shm|-journal)?|" +
        @"network\.json|default-admin-password\.txt|api-token|setup-code|\.(?:network|default-admin-password)-\d+|" +
        @"core\.db\.before-v\d+-\d+/core\.db(?:-wal|-shm|-journal)?|" +
        @"ai-gateway/(?:settings\.json|\.gateway-settings-\d+)|" +
        @"audio-preparation/(?:\.lock|format|clip-[a-f0-9]{24}\.wav)|" +
        @"images/(?:[a-f0-9]{64}-[a-f0-9]{64}|\.(?:image|ruri)-\d+)|" +
        @"license/(?:identity\.json|license\.lock|\.identity-\d+)|" +
        @"logs/(?:core\.jsonl(?:\.\d+)?|\.log-retention-\d+)|" +
        @"tls/[a-f0-9]{32}\.(?:crt|key)|" +
        @"transcodes/(?:[a-f0-9]{64}\.audio|ruri-\d+\.part)|" +
        @"voice-catalog/(?:catalog\.json|profiles/[a-f0-9]{32}\.json|covers/[a-f0-9]{32}\.img|(?:(?:profiles|covers)/)?\.voice-\d+)|" +
        @"media-tools/(?:ffmpeg-[\w.-]+|\.preparing-\d+)/(?:ffmpeg\.exe|ffprobe\.exe|LICENSE\.txt|download)" +
        @")\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private sealed class UnsafeDataException : IOException
    {
        public UnsafeDataException(string message) : base(message) {}
    }

    private static FileAttributes? ExistingAttributes(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static bool IsRegularFile(string path)
    {
        var attributes = ExistingAttributes(path);
        if (!attributes.HasValue) return false;
        if ((attributes.Value & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            throw new IOException("Expected a regular application file: " + path);
        return true;
    }

    private static bool HasDataDirectory(DirectoryInfo root)
    {
        var attributes = ExistingAttributes(root.FullName);
        if (!attributes.HasValue) return false;
        RejectLink(attributes.Value);
        if ((attributes.Value & FileAttributes.Directory) == 0)
            throw new UnsafeDataException("应用数据路径不是文件夹。请检查数据位置后重试，或取消勾选数据清理。");
        for (DirectoryInfo parent = root.Parent; parent != null; parent = parent.Parent)
            RejectLink(parent.Attributes);
        return true;
    }

    public static void ClearDataDirectory(string directory)
    {
        var root = new DirectoryInfo(directory);
        if (!HasDataDirectory(root)) return;
        var entries = new List<FileSystemInfo>();
        InspectDataEntries(root, "", entries);
        string lockPath = Path.Combine(root.FullName, "core.lock");
        using (File.Open(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            foreach (FileSystemInfo entry in entries)
            {
                if (string.Equals(entry.FullName, lockPath, StringComparison.OrdinalIgnoreCase)) continue;
                RejectLink(File.GetAttributes(entry.FullName));
                entry.Delete();
            }
        }
        File.Delete(lockPath);
        root.Delete();
    }

    private static void RejectLink(FileAttributes attributes)
    {
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new UnsafeDataException("数据目录包含链接，未清除数据。请检查该目录后重试，或取消勾选数据清理。");
    }

    private static void InspectDataEntries(DirectoryInfo directory, string prefix, List<FileSystemInfo> entries)
    {
        foreach (FileSystemInfo entry in directory.GetFileSystemInfos())
        {
            RejectLink(entry.Attributes);
            string relativePath = prefix + entry.Name;
            var child = entry as DirectoryInfo;
            if (!(child != null ? CoreDataFolders : CoreDataFiles).IsMatch(relativePath))
                throw new UnsafeDataException("数据目录含有额外文件或文件夹。请先将额外资料移出数据目录再重试，或取消勾选数据清理。");
            if (child != null) InspectDataEntries(child, relativePath + "/", entries);
            entries.Add(entry);
        }
    }

    [CustomAction]
    public static ActionResult PrepareRemoveData(Session session)
    {
        if (session["RollbackDisabled"] != "")
            return Fail(session, "当前的 Windows 安装设置不支持同时清理数据。请取消勾选数据清理后卸载。", null);
        var data = new CustomActionData();
        data.Add("Directory", DataDirectory());
        session["RemoveUserData"] = data.ToString();
        return ActionResult.Success;
    }

    [CustomAction]
    public static ActionResult RemoveUserData(Session session)
    {
        try { ClearDataDirectory(session.CustomActionData["Directory"]); return ActionResult.Success; }
        catch (UnsafeDataException error) { return Fail(session, error.Message, null); }
        catch (Exception error) { return Fail(session, "无法完成应用数据清理。请退出 Ruri、确认数据目录可写后重试，或取消勾选数据清理。", error); }
    }

    private static ActionResult Fail(Session session, string message, Exception error)
    {
        if (error != null) session.Log(error.ToString());
        using (var record = new Record(0))
        {
            record.FormatString = message;
            session.Message(InstallMessage.Error, record);
        }
        return ActionResult.Failure;
    }
}
