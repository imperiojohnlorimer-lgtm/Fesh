using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Fesh;

// Playing over a local network (1.30). Nothing here knows about the game or the screen, and nothing blocks: the game
// polls it once a frame. A connection is plain TCP (moves must arrive, in order); finding a game is a UDP broadcast.
// Everything that comes in is untrusted: frames are bounded, bad ones close the connection, and no message can make the
// receiver throw.

// One small message. `t` says what it is; the other fields are used by whichever messages need them.
sealed class NetMsg
{
    public string t = "", id = "", name = "", why = "", data;
    public int v, seat, pit, rev, dig, first, s0, s1, seq, to, from, ch;
    public int[] n;                    // lists of small numbers (a roster, a look)
}

interface ILink
{
    bool Alive { get; }
    double SilentFor { get; }          // seconds since anything arrived
    string Peer { get; }
    void Send(NetMsg m);
    NetMsg Receive();                  // the next message, or null
    void Close();
}

static class Wire
{
    static readonly JsonSerializerOptions Opts = new() { IncludeFields = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault };
    public const int MaxFrame = 2048;

    // A message read from JSON text carried inside another (a routed one), or null if it isn't one.
    public static NetMsg ParseText(string json)
    {
        if (string.IsNullOrEmpty(json) || json.Length > MaxFrame) return null;
        try { var m = JsonSerializer.Deserialize<NetMsg>(json, Opts); return m == null || string.IsNullOrEmpty(m.t) || m.t.Length > 16 ? null : m; }
        catch (Exception) { return null; }
    }
    public static string ToText(NetMsg m) => JsonSerializer.Serialize(m, Opts);

    // A frame is four bytes of length (little endian) and that much JSON.
    public static byte[] Frame(NetMsg m)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(m, Opts);
        if (json.Length > MaxFrame) return null;       // never put a frame on the wire that the other side would refuse
        var f = new byte[4 + json.Length];
        f[0] = (byte)json.Length; f[1] = (byte)(json.Length >> 8); f[2] = (byte)(json.Length >> 16); f[3] = (byte)(json.Length >> 24);
        Buffer.BlockCopy(json, 0, f, 4, json.Length);
        return f;
    }

    // Takes every whole frame out of the front of buf. False means the stream is bad (a length that can't be right, or
    // JSON that isn't a message): the caller drops the connection.
    public static bool Parse(byte[] buf, ref int have, List<NetMsg> into)
    {
        int at = 0;
        while (have - at >= 4)
        {
            int len = buf[at] | buf[at + 1] << 8 | buf[at + 2] << 16 | buf[at + 3] << 24;
            if (len <= 0 || len > MaxFrame) return false;
            if (have - at - 4 < len) break;
            try
            {
                var m = JsonSerializer.Deserialize<NetMsg>(new ReadOnlySpan<byte>(buf, at + 4, len), Opts);
                if (m == null || string.IsNullOrEmpty(m.t) || m.t.Length > 16) return false;
                into.Add(m);
            }
            catch (Exception) { return false; }
            at += 4 + len;
        }
        if (at > 0) { Buffer.BlockCopy(buf, at, buf, 0, have - at); have -= at; }
        return true;
    }
}

sealed class TcpLink : ILink
{
    readonly TcpClient c;
    readonly Socket sock;
    readonly byte[] buf = new byte[Wire.MaxFrame * 2 + 8];
    int have;
    readonly Queue<NetMsg> inbox = new();
    readonly Queue<byte[]> outbox = new();
    int outOffset, outBytes;
    readonly Stopwatch quiet = Stopwatch.StartNew();
    bool dead;
    public string Peer { get; }

    // However fast a peer sends, one look at the socket takes at most this many bytes, and a peer that gets more than this
    // many messages ahead of the game is cut off: a flood can't freeze a frame or fill memory. Sending never blocks
    // either: what the socket won't take yet waits in a small queue, and a peer that never reads is cut off.
    const int MaxBytesPerPump = 16384, MaxInbox = 512, MaxOutBytes = 32768;

    public TcpLink(TcpClient client)
    {
        c = client;
        c.NoDelay = true;
        sock = c.Client;
        sock.Blocking = false;
        Peer = sock.RemoteEndPoint is IPEndPoint ep ? ep.Address.ToString() : "?";
    }

    public bool Alive { get { Pump(); return !dead; } }
    public double SilentFor => quiet.Elapsed.TotalSeconds;

    void Pump()
    {
        if (dead) return;
        try
        {
            Flush();
            int budget = MaxBytesPerPump;
            while (!dead && budget > 0 && sock.Available > 0 && have < buf.Length)
            {
                int n = sock.Receive(buf, have, Math.Min(Math.Min(sock.Available, buf.Length - have), budget), SocketFlags.None, out var err);
                if (err == SocketError.WouldBlock) break;
                if (err != SocketError.Success || n <= 0) { dead = true; break; }
                have += n; budget -= n;
                quiet.Restart();
                var got = new List<NetMsg>();
                if (!Wire.Parse(buf, ref have, got)) { dead = true; break; }
                foreach (var m in got) inbox.Enqueue(m);
                if (inbox.Count > MaxInbox) { dead = true; break; }
            }
            // The other end closed the connection.
            if (!dead && sock.Poll(0, SelectMode.SelectRead) && sock.Available == 0) dead = true;
        }
        catch (Exception) { dead = true; }
    }

    // Sends what the socket will take right now; the rest stays queued for the next look.
    void Flush()
    {
        try
        {
            while (!dead && outbox.Count > 0)
            {
                var f = outbox.Peek();
                int n = sock.Send(f, outOffset, f.Length - outOffset, SocketFlags.None, out var err);
                if (err == SocketError.WouldBlock) return;
                if (err != SocketError.Success) { dead = true; return; }
                outOffset += n; outBytes -= n;
                if (outOffset >= f.Length) { outbox.Dequeue(); outOffset = 0; }
            }
        }
        catch (Exception) { dead = true; }
    }

    public void Send(NetMsg m)
    {
        if (dead) return;
        var f = Wire.Frame(m);
        if (f == null) return;
        outbox.Enqueue(f);
        outBytes += f.Length;
        if (outBytes > MaxOutBytes) { dead = true; return; }
        Flush();
    }

    public NetMsg Receive()
    {
        if (inbox.Count == 0) Pump();
        return inbox.Count > 0 ? inbox.Dequeue() : null;
    }

    public void Close()
    {
        if (!dead) Flush();            // a goodbye still queued gets its chance
        dead = true;
        try { c.Close(); } catch (Exception) { /* already gone */ }
    }
}

// Two ends of a pretend connection in memory, for tests that shouldn't need a network.
sealed class MemoryLink : ILink
{
    readonly Queue<NetMsg> inbox = new();
    MemoryLink other;
    bool dead;
    public double Quiet;               // what SilentFor says; a test sets it
    public string Peer => "memory";
    public bool Alive => !dead;
    public double SilentFor => Quiet;
    public readonly List<NetMsg> Sent = new();

    public static (MemoryLink a, MemoryLink b) Pair()
    {
        var a = new MemoryLink(); var b = new MemoryLink();
        a.other = b; b.other = a;
        return (a, b);
    }

    public void Send(NetMsg m)
    {
        if (dead) return;
        Sent.Add(m);
        // Through the same bytes a real connection would use, so what's received is exactly what a socket would give.
        var f = Wire.Frame(m);
        if (f == null) return;
        var got = new List<NetMsg>();
        int have = f.Length;
        var copy = (byte[])f.Clone();
        if (Wire.Parse(copy, ref have, got)) foreach (var g in got) other.inbox.Enqueue(g);
    }

    public NetMsg Receive() => inbox.Count > 0 ? inbox.Dequeue() : null;

    public void Close() { dead = true; if (other != null) other.dead = true; }
    public void Inject(NetMsg m) => inbox.Enqueue(m);
}

// Waiting for someone to join: a listener, and a reply to anyone asking on the network who's hosting.
sealed class LanHost : IDisposable
{
    public const int DiscoveryPort = 47830, FirstPort = 47831;
    readonly TcpListener listener;
    readonly UdpClient udp;
    public readonly int Port;
    public readonly bool Discoverable;
    public readonly string Id = Guid.NewGuid().ToString("N")[..12];     // so a host heard on several adapters is one entry
    public string Name, Game;
    public bool Open = true;

    public LanHost(string name, string game, int discoveryPort = DiscoveryPort, int firstPort = FirstPort)
    {
        Name = name; Game = game;
        TcpListener l = null;
        int port = firstPort;
        for (; port < firstPort + 12; port++)
        {
            try { l = new TcpListener(IPAddress.Any, port); l.Start(2); break; }
            catch (SocketException) { l = null; }
        }
        listener = l ?? throw new SocketException((int)SocketError.AddressAlreadyInUse);
        Port = port;
        try
        {
            udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));
            Discoverable = true;
        }
        catch (Exception) { udp?.Dispose(); udp = null; }
    }

    // Once a frame: answers anyone who asked, and hands over a new connection if there is one.
    public ILink Poll()
    {
        try
        {
            for (int k = 0; k < 16 && udp != null && udp.Available > 0; k++)
            {
                IPEndPoint from = new(IPAddress.Any, 0);
                var data = udp.Receive(ref from);
                if (Encoding.ASCII.GetString(data) != LanBrowser.Ask) continue;
                var reply = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, object> { ["fesh"] = "lan", ["v"] = SungkaSession.Protocol, ["id"] = Id, ["name"] = Name, ["port"] = Port, ["game"] = Game, ["open"] = Open }));
                udp.Send(reply, reply.Length, from);
            }
        }
        catch (Exception) { /* discovery is a convenience */ }
        try
        {
            if (listener.Pending())
            {
                // Always accepted, even when full: the session tells a late guest the game is full (Open only changes the advertisement).
                return new TcpLink(listener.AcceptTcpClient());
            }
        }
        catch (Exception) { }
        return null;
    }

    public void Dispose()
    {
        try { listener.Stop(); } catch (Exception) { }
        try { udp?.Dispose(); } catch (Exception) { }
    }
}

// Asking the network who's hosting, once or twice a second, and remembering who answered.
sealed class LanBrowser : IDisposable
{
    public const string Ask = "FESH-LAN?";
    public sealed class Found { public string Id = "", Name = "", Ip = "", Game = ""; public int Port; public double Seen; public bool Open = true; }
    public readonly List<Found> Hosts = new();
    readonly UdpClient udp;
    readonly int discoveryPort;
    readonly Stopwatch clock = Stopwatch.StartNew();
    double nextPing;
    readonly bool loopbackOnly;

    public LanBrowser(int discoveryPort = LanHost.DiscoveryPort, bool loopbackOnly = false)
    {
        this.discoveryPort = discoveryPort;
        this.loopbackOnly = loopbackOnly;
        try
        {
            udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0)) { EnableBroadcast = true };
        }
        catch (Exception) { udp = null; }
    }

    public bool Working => udp != null;

    static IEnumerable<IPAddress> BroadcastAddresses()
    {
        var res = new List<IPAddress> { IPAddress.Broadcast };
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var ua in nic.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null) continue;
                    var ip = ua.Address.GetAddressBytes(); var mask = ua.IPv4Mask.GetAddressBytes();
                    var bc = new byte[4];
                    for (int i = 0; i < 4; i++) bc[i] = (byte)(ip[i] | ~mask[i]);
                    res.Add(new IPAddress(bc));
                }
            }
        }
        catch (Exception) { }
        return res.Distinct();
    }

    public void Poll()
    {
        if (udp == null) return;
        double now = clock.Elapsed.TotalSeconds;
        try
        {
            if (now >= nextPing)
            {
                nextPing = now + 1.2;
                var ask = Encoding.ASCII.GetBytes(Ask);
                // The same computer too, so two copies of the game on one machine find each other.
                udp.Send(ask, ask.Length, new IPEndPoint(IPAddress.Loopback, discoveryPort));
                if (!loopbackOnly)
                    foreach (var a in BroadcastAddresses()) { try { udp.Send(ask, ask.Length, new IPEndPoint(a, discoveryPort)); } catch (Exception) { } }
            }
            for (int k = 0; k < 32 && udp.Available > 0; k++)
            {
                IPEndPoint from = new(IPAddress.Any, 0);
                var data = udp.Receive(ref from);
                Ingest(data, from.Address, now);
            }
        }
        catch (Exception) { /* a bad answer is ignored */ }
        Hosts.RemoveAll(h => now - h.Seen > 5);
    }

    // One answer to the question. One host heard through several adapters (a VPN, a virtual machine, the loopback) is one
    // entry, and a real address is kept over a loopback one.
    public void Ingest(byte[] data, IPAddress source, double now)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            var r = doc.RootElement;
            if (r.GetProperty("fesh").GetString() != "lan" || r.GetProperty("v").GetInt32() != SungkaSession.Protocol) return;
            int port = r.GetProperty("port").GetInt32();
            if (port is < 1 or > 65535) return;
            string ip = source.ToString();
            string id = r.TryGetProperty("id", out var idp) ? SungkaSession.Clean(idp.GetString()) : ip + ":" + port;
            var f = Hosts.FirstOrDefault(h => h.Id == id);
            if (f == null) Hosts.Add(f = new Found { Id = id, Ip = ip, Port = port });
            else if (f.Ip == "127.0.0.1" && ip != "127.0.0.1") { f.Ip = ip; f.Port = port; }
            f.Name = SungkaSession.Clean(r.GetProperty("name").GetString());
            f.Game = SungkaSession.Clean(r.GetProperty("game").GetString());
            f.Open = r.GetProperty("open").GetBoolean();
            f.Seen = now;
        }
        catch (Exception) { /* not an answer */ }
    }

    public void Dispose() { try { udp?.Dispose(); } catch (Exception) { } }
}

// Joining a game: connecting without waiting for it.
sealed class LanConnect
{
    readonly TcpClient client = new();
    readonly Task task;
    readonly Stopwatch clock = Stopwatch.StartNew();
    public string Error;
    public bool Done;

    public LanConnect(string ip, int port)
    {
        task = client.ConnectAsync(ip, port);
    }

    // The connection once it's made; null while still trying (Error says why if it gave up).
    public ILink Poll()
    {
        if (Done) return null;
        if (task.IsFaulted) { Error = "Couldn't reach that address."; Done = true; client.Close(); return null; }
        if (task.IsCompletedSuccessfully) { Done = true; return new TcpLink(client); }
        if (clock.Elapsed.TotalSeconds > 4) { Error = "No answer from that address."; Done = true; client.Close(); }
        return null;
    }

    public void Cancel() { Done = true; try { client.Close(); } catch (Exception) { } }
}

static class Lan
{
    // This computer's address on the local network, for telling the other player. The adapter that has a way out (a gateway)
    // and isn't obviously virtual (VirtualBox, VMware, Hyper-V) comes first.
    public static string LocalAddress()
    {
        try
        {
            string best = null;
            var nics = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .OrderBy(n => IsVirtual(n) || IsMobileData(n) ? 1 : 0).ThenBy(n => n.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any)) ? 0 : 1);
            foreach (var nic in nics)
            {
                foreach (var ua in nic.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    string s = ua.Address.ToString();
                    if (s.StartsWith("169.254.")) continue;
                    if (s.StartsWith("192.168.") || s.StartsWith("10.") || s.StartsWith("172.")) return s;
                    best ??= s;
                }
            }
            return best ?? "127.0.0.1";
        }
        catch (Exception) { return "127.0.0.1"; }
    }

    // A phone's mobile-data link has a way out but is not the network the other player is on (a phone that is the hotspot has no gateway on
    // its own hotspot side), so it comes after the Wi-Fi and hotspot adapters. Windows never names an adapter like these.
    static bool IsMobileData(NetworkInterface n)
    {
        string s = n.Name.ToLowerInvariant();
        return s.StartsWith("rmnet") || s.StartsWith("ccmni") || s.StartsWith("pdp") || s.StartsWith("wwan") || s.StartsWith("ppp") || s.StartsWith("clat") || s.StartsWith("v4-") || s.StartsWith("tun") || s.StartsWith("dummy");
    }

    static bool IsVirtual(NetworkInterface n)
    {
        string s = (n.Name + " " + n.Description).ToLowerInvariant();
        return s.Contains("virtualbox") || s.Contains("vmware") || s.Contains("hyper-v") || s.Contains("vethernet") || s.Contains("host-only") || s.Contains("pseudo") || s.Contains("vpn") || s.Contains("tap-") || s.Contains("tunnel");
    }

    // "192.168.1.20" or "192.168.1.20:47831".
    public static bool TryParse(string text, out string ip, out int port)
    {
        ip = null; port = LanHost.FirstPort;
        text = (text ?? "").Trim();
        int colon = text.IndexOf(':');
        string host = colon >= 0 ? text[..colon] : text;
        if (colon >= 0 && !int.TryParse(text[(colon + 1)..], out port)) return false;
        if (port is < 1 or > 65535) return false;
        if (!IPAddress.TryParse(host, out var addr) || addr.AddressFamily != AddressFamily.InterNetwork || host.Split('.').Length != 4) return false;
        ip = addr.ToString();
        return true;
    }
}
