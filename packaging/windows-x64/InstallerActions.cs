using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Web.Script.Serialization;
using WixToolset.Dtf.WindowsInstaller;

public static class InstallerActions
{
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

    // Only the application's data folder is removed. Media paths are never passed here.
    public static void ClearDataDirectory(string directory)
    {
        if (!Directory.Exists(directory)) return;
        RejectLink(Directory.GetParent(directory).FullName);
        RejectLinks(directory);
        string lockPath = Path.Combine(directory, "core.lock");
        using (File.Open(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            foreach (string path in Directory.GetFileSystemEntries(directory))
            {
                if (string.Equals(path, lockPath, StringComparison.OrdinalIgnoreCase)) continue;
                if (Directory.Exists(path)) Directory.Delete(path, true);
                else File.Delete(path);
            }
        }
        File.Delete(lockPath);
        Directory.Delete(directory);
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & System.IO.FileAttributes.ReparsePoint) != 0)
            throw new IOException("数据目录包含链接，未清除数据。请检查该目录后手动处理。");
    }

    private static void RejectLinks(string directory)
    {
        RejectLink(directory);
        foreach (string path in Directory.GetFileSystemEntries(directory))
        {
            RejectLink(path);
            if (Directory.Exists(path)) RejectLinks(path);
        }
    }

    [CustomAction]
    public static ActionResult PrepareRemoveData(Session session)
    {
        var data = new CustomActionData();
        data.Add("Directory", DataDirectory());
        session["RemoveUserData"] = data.ToString();
        return ActionResult.Success;
    }

    [CustomAction]
    public static ActionResult RemoveUserData(Session session)
    {
        try { ClearDataDirectory(session.CustomActionData["Directory"]); return ActionResult.Success; }
        catch (Exception error) { return Fail(session, "无法清除应用数据。请退出 Ruri，检查目录权限及链接后重试；媒体来源目录不受影响。", error); }
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
