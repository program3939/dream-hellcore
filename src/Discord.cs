// Dreamy Hellcore - Discord-Aktivitaet (Rich Presence) ueber die lokale Discord-Schnittstelle (IPC).
// Braucht eine Discord-Anwendungs-ID in der Datei discord_app_id.txt neben der Programmdatei (Anleitung dort).
// Ohne ID oder ohne laufendes Discord passiert nichts. Es werden nur Hellstage und Raumtyp uebermittelt.
using System;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace DreamyHellcore;

static class Discord
{
    static string appId, pending, last; static Stream pipe; static readonly object gate = new();
    static Thread th; static volatile bool run; static long startUnix;
    // Englische Namen fuer die Anzeige auf Discord (Reihenfolge wie Room.ThemeNames / Room.EscapeNames)
    static readonly string[] ThemeEn = { "Hall", "Pillar Forest", "Liminal Corridor", "Stairwell", "Descent", "Beam Bridge", "Stepping Stones", "Symmetry",
        "Void", "Crawlspace", "Storm Room", "Floating Room", "Tilted Room", "Narrows", "Machine Room", "Rollercoaster Room" };
    static readonly string[] EscapeEn = { "-", "Seals", "Sequence", "Don't Look", "Stand Still", "Flee", "-", "-", "-", "-", "-",
        "Lights Out", "Sum", "Counting Lock", "Every Stone Once", "The Odd One", "Scale", "Mirror Image", "Counter", "Coupled Wheels", "Secret Code" };

    public static void Start()
    {
        try
        {
            string p = Path.Combine(AppContext.BaseDirectory, "discord_app_id.txt");
            if (!File.Exists(p)) return;
            foreach (string raw in File.ReadAllLines(p))
            {
                string l = raw.Trim(); if (l.Length < 15 || l.Length > 22) continue;
                bool digits = true; foreach (char c in l) digits &= c >= '0' && c <= '9';
                if (digits) { appId = l; break; }
            }
        }
        catch { return; }
        if (appId == null) return;
        startUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); run = true;
        th = new Thread(Loop) { IsBackground = true }; th.Start();
    }

    /// <summary>Aktivitaet setzen: "Hellstage N" und darunter Raumtyp bzw. Escape-Raum.</summary>
    public static void Set(long stage, Room r)
    {
        if (!run || r == null) return;
        string state = r.Escape >= 11 ? "Escape room: " + EscapeEn[r.Escape] : r.Escape > 0 ? "Trial: " + EscapeEn[r.Escape] : ThemeEn[r.Theme];
        if (r.Heaven) state += " (False Heaven)";
        string json = "{\"cmd\":\"SET_ACTIVITY\",\"args\":{\"pid\":" + Environment.ProcessId + ",\"activity\":{\"details\":\"Hellstage " + stage
            + "\",\"state\":\"" + state + "\",\"timestamps\":{\"start\":" + startUnix + "},\"assets\":{\"large_image\":\"logo\",\"large_text\":\"Dreamy Hellcore\"}}},\"nonce\":\""
            + Guid.NewGuid() + "\"}";
        lock (gate) { pending = json; last = json; }
    }

    public static void Stop() { run = false; try { pipe?.Dispose(); } catch { } pipe = null; }

    static void Loop()
    {
        while (run)
        {
            try
            {
                if (pipe == null) { Connect(); lock (gate) pending ??= last; }
                string p; lock (gate) { p = pending; pending = null; }
                if (p != null && pipe != null) { Send(1, p); ReadFrame(); }
            }
            catch { try { pipe?.Dispose(); } catch { } pipe = null; }
            Thread.Sleep(pipe == null ? 15000 : 2000);
        }
    }

    static void Connect()
    {
        for (int i = 0; i < 10 && pipe == null; i++)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var np = new NamedPipeClientStream(".", "discord-ipc-" + i, PipeDirection.InOut);
                    np.Connect(250); pipe = np;
                }
                else
                {
                    string dir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? "/tmp";
                    var s = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    s.Connect(new UnixDomainSocketEndPoint(Path.Combine(dir, "discord-ipc-" + i)));
                    pipe = new NetworkStream(s, true);
                }
            }
            catch { pipe = null; }
        }
        if (pipe == null) return;
        Send(0, "{\"v\":1,\"client_id\":\"" + appId + "\"}"); ReadFrame();
    }

    static void Send(int op, string json)
    {
        byte[] body = Encoding.UTF8.GetBytes(json), buf = new byte[8 + body.Length];
        BitConverter.GetBytes(op).CopyTo(buf, 0); BitConverter.GetBytes(body.Length).CopyTo(buf, 4); body.CopyTo(buf, 8);
        pipe.Write(buf, 0, buf.Length); pipe.Flush();
    }
    static void ReadFrame()
    {
        byte[] head = new byte[8]; ReadExact(head, 8);
        int len = BitConverter.ToInt32(head, 4); if (len < 0 || len > 1 << 20) throw new IOException();
        ReadExact(new byte[len], len);
    }
    static void ReadExact(byte[] b, int n) { int o = 0; while (o < n) { int k = pipe.Read(b, o, n - o); if (k <= 0) throw new IOException(); o += k; } }
}
