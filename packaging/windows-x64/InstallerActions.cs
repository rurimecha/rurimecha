using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
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

    [CustomAction]
    public static ActionResult ReadNetwork(Session session)
    {
        try
        {
            string ip = "0.0.0.0", port = "8080", path = Path.Combine(DataDirectory(), "network.json");
            if (File.Exists(path))
            {
                var config = new JavaScriptSerializer { MaxJsonLength = 65536 }.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
                port = Convert.ToString(config["port"], CultureInfo.InvariantCulture);
                bool ipv4 = Convert.ToBoolean(config["ipv4"]);
                ip = ipv4 ? "0.0.0.0" : "::";
                object addresses;
                if (config.TryGetValue(ipv4 ? "ipv4_addresses" : "ipv6_addresses", out addresses) && addresses != null)
                {
                    foreach (object address in (IEnumerable)addresses)
                    {
                        ip = Convert.ToString(address);
                        if (!IPAddress.IsLoopback(IPAddress.Parse(ip))) break;
                    }
                }
                session["RURI_ORIGINAL_IP"] = ip;
                session["RURI_ORIGINAL_PORT"] = port;
            }
            if (session["RURI_LISTEN_IP"] == "") session["RURI_LISTEN_IP"] = ip;
            if (session["RURI_LISTEN_PORT"] == "") session["RURI_LISTEN_PORT"] = port;
            return ActionResult.Success;
        }
        catch (Exception error) { return Fail(session, "无法读取已有网络设置。请先退出 Ruri，检查应用数据目录后重试。", error); }
    }

    // Bind real sockets, including the loopback address needed by the desktop window.
    public static void CheckAddress(string address, int port)
    {
        IPAddress ip;
        if (!IPAddress.TryParse(address, out ip) || port < 1 || port > 65535)
            throw new ArgumentException("请输入本机 IP 和 1–65535 范围内的端口。");
        var sockets = new List<Socket>();
        try
        {
            var addresses = new List<IPAddress> { ip };
            if (!ip.Equals(IPAddress.Any) && !ip.Equals(IPAddress.Loopback)) addresses.Add(IPAddress.Loopback);
            foreach (IPAddress bind in addresses)
            {
                var socket = new Socket(bind.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                sockets.Add(socket);
                socket.ExclusiveAddressUse = true;
                if (bind.AddressFamily == AddressFamily.InterNetworkV6)
                    socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, true);
                socket.Bind(new IPEndPoint(bind, port));
            }
        }
        finally { foreach (Socket socket in sockets) socket.Close(); }
    }

    [CustomAction]
    public static ActionResult CheckNetwork(Session session)
    {
        session["RURI_NETWORK_VALID"] = "0";
        int port;
        IPAddress ip;
        if (!IPAddress.TryParse(session["RURI_LISTEN_IP"], out ip) ||
            !int.TryParse(session["RURI_LISTEN_PORT"], out port) || port < 1 || port > 65535)
        {
            session["RURI_NETWORK_ERROR"] = "请输入本机 IP 和 1–65535 范围内的端口。";
            return ActionResult.Success;
        }
        try
        {
            CheckAddress(ip.ToString(), port);
            session["RURI_LISTEN_IP"] = ip.ToString();
            session["RURI_LISTEN_PORT"] = port.ToString(CultureInfo.InvariantCulture);
            session["RURI_NETWORK_VALID"] = "1";
        }
        catch (SocketException)
        {
            session["RURI_NETWORK_ERROR"] = "无法使用这个监听地址或端口。请确认 IP 属于本机，并退出 Ruri 或占用该端口的其他程序，再重试；也可以改用其他端口。";
        }
        return ActionResult.Success;
    }

    [CustomAction]
    public static ActionResult PrepareNetwork(Session session)
    {
        CheckNetwork(session);
        if (session["RURI_NETWORK_VALID"] != "1") return Fail(session, session["RURI_NETWORK_ERROR"], null);
        string path = Path.Combine(DataDirectory(), "network.json");
        var data = new CustomActionData();
        data.Add("Directory", DataDirectory());
        data.Add("Core", Path.Combine(session["INSTALLFOLDER"], "rurimecha-core.exe"));
        IPAddress ip = IPAddress.Parse(session["RURI_LISTEN_IP"]);
        data.Add("Address", (ip.AddressFamily == AddressFamily.InterNetworkV6 ? "[" + ip + "]" : ip.ToString()) + ":" + session["RURI_LISTEN_PORT"]);
        data.Add("Changed", (session["RURI_ORIGINAL_IP"] != session["RURI_LISTEN_IP"] || session["RURI_ORIGINAL_PORT"] != session["RURI_LISTEN_PORT"]) ? "1" : "0");
        data.Add("Original", File.Exists(path) ? Convert.ToBase64String(File.ReadAllBytes(path)) : "");
        session["ApplyNetwork"] = data.ToString();
        session["RollbackNetwork"] = data.ToString();
        return ActionResult.Success;
    }

    [CustomAction]
    public static ActionResult ApplyNetwork(Session session)
    {
        var data = session.CustomActionData;
        if (data["Changed"] != "1") return ActionResult.Success;
        try
        {
            var start = new ProcessStartInfo(data["Core"], "-data-dir \"" + data["Directory"] + "\" -configure-listen \"" + data["Address"] + "\"");
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardError = true;
            using (var process = Process.Start(start))
            {
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new IOException(error);
            }
            return ActionResult.Success;
        }
        catch (Exception error) { return Fail(session, "无法保存监听设置。请退出 Ruri，确认应用数据目录可写、端口可用，再重新安装。", error); }
    }

    [CustomAction]
    public static ActionResult RollbackNetwork(Session session)
    {
        var data = session.CustomActionData;
        if (data["Changed"] != "1") return ActionResult.Success;
        string path = Path.Combine(data["Directory"], "network.json");
        if (data["Original"] == "") File.Delete(path);
        else File.WriteAllBytes(path, Convert.FromBase64String(data["Original"]));
        return ActionResult.Success;
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

    public static void ClearDataDirectory(string directory)
    {
        var root = new DirectoryInfo(directory);
        FileAttributes rootAttributes;
        try { rootAttributes = File.GetAttributes(root.FullName); }
        catch (FileNotFoundException) { return; }
        catch (DirectoryNotFoundException) { return; }
        RejectLink(rootAttributes);
        if ((rootAttributes & FileAttributes.Directory) == 0)
            throw new UnsafeDataException("应用数据路径不是文件夹。请检查数据位置后重试，或取消勾选数据清理。");
        for (DirectoryInfo parent = root.Parent; parent != null; parent = parent.Parent)
            RejectLink(parent.Attributes);

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
