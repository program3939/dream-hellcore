// Dreamy Hellcore - Spielschleife, Physik, Tests, Audio und Renderer (HDR + Post-Processing).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace DreamyHellcore;

static unsafe partial class Game
{
    const float R = 0.3f, StandH = 1.8f, CrouchH = 0.9f;
    static Room prev, cur, next;
    static List<Box> world = new();
    static long stage = 1;
    static Vector3 pos, vel, respawn;
    static float yaw, pitch, height = StandH, eyeH = StandH - 0.12f, coyote, jumpBuf, fade = 0;
    static bool grounded; static Box groundBox;
    static string savePath; static bool noSave;

    // ---- Shader-Huelle mit Uniform-Cache ----
    static string shaderErrors = "";
    sealed class Sh
    {
        public Shader S; readonly Dictionary<string, int> loc = new();
        public Sh(string vs, string fs, string name)
        {
            S = LoadShaderFromMemory(vs, fs);
            if (S.Id == Rlgl.GetShaderIdDefault()) shaderErrors += name + " ";
        }
        public int Loc(string n) { if (!loc.TryGetValue(n, out int l)) loc[n] = l = GetShaderLocation(S, n); return l; }
        public void F(string n, float v) => SetShaderValue(S, Loc(n), v, ShaderUniformDataType.Float);
        public void V3(string n, Vector3 v) => SetShaderValue(S, Loc(n), v, ShaderUniformDataType.Vec3);
        public void Tex(string n, Texture2D t) => SetShaderValueTexture(S, Loc(n), t);
    }

    static void BuildWorldOld()
    {
        world.Clear(); world.AddRange(cur.Col);
        foreach (var b in next.Col) { var c = b; c.Min.Z += cur.L; c.Max.Z += cur.L; world.Add(c); }
        if (prev != null) foreach (var b in prev.Col) { var c = b; c.Min.Z -= prev.L; c.Max.Z -= prev.L; world.Add(c); }
        SetWindowTitle("Dreamy Hellcore - Hellstage " + stage.ToString(CultureInfo.InvariantCulture));
    }

    static void LoadStage(long n)
    {
        stage = Math.Max(1, n);
        prev?.Dispose(); cur?.Dispose(); next?.Dispose();
        prev = stage > 1 ? new Room(stage - 1) : null; cur = new Room(stage); next = new Room(stage + 1);
        BuildWorld();
        respawn = new Vector3(0, 0.05f, 1f); Respawn(); yaw = 0; pitch = 0; Save(); lastGroundY = 0; if (!Room.NoGpu) Audio.AmbientSet(stage, cur.Theme, cur.Mood); sT = cur.Terror; sHeaven = cur.Heaven ? 1 : 0; EscapeReset();
    }

    static void Respawn() { botRespawns++; Audio.Respawn(); ride = null; Audio.RailSpeed = 0; groundKey = -1; lastGroundY = respawn.Y; pos = respawn; vel = Vector3.Zero; fade = 0; height = StandH; }
    static void Save() { if (noSave) return; try { File.WriteAllText(savePath, stage.ToString(CultureInfo.InvariantCulture)); } catch { } }

    static bool Hit(in Box b, float h) =>
        pos.X - R < b.Max.X && pos.X + R > b.Min.X && pos.Y < b.Max.Y && pos.Y + h > b.Min.Y && pos.Z - R < b.Max.Z && pos.Z + R > b.Min.Z;

    static void Step(float dt, Vector3 wish, float accel)
    {
        float k = MathF.Min(1, accel * dt);
        vel.X += (wish.X - vel.X) * k; vel.Z += (wish.Z - vel.Z) * k;
        vel.Y = MathF.Max(vel.Y - gravNow * dt, -40f);
        const float eps = 0.001f;
        float d = vel.X * dt; pos.X += d;
        foreach (var b in world) if (Hit(b, height)) { pos.X = d > 0 ? b.Min.X - R - eps : b.Max.X + R + eps; vel.X = 0; }
        d = vel.Z * dt; pos.Z += d;
        foreach (var b in world) if (Hit(b, height)) { pos.Z = d > 0 ? b.Min.Z - R - eps : b.Max.Z + R + eps; vel.Z = 0; }
        d = vel.Y * dt; pos.Y += d; grounded = false;
        foreach (var b in world) if (Hit(b, height))
            {
                if (d <= 0) { pos.Y = b.Max.Y; grounded = true; groundBox = b; } else pos.Y = b.Min.Y - height - eps;
                vel.Y = 0;
            }
    }

    static float stepDist, moveTime, ghostTimer, ghostPan, stepSide = 1; static int ghostSteps; static readonly Random grng = new Random(5);

    // ---- Selbsttest ohne Fenster: jeder generierte Sprung wird mit der echten Physik nachsimuliert ----
    static bool TryJump(Box a, Box b, float speed)
    {
        float tx = Math.Clamp((a.Min.X + a.Max.X) / 2, b.Min.X + 0.35f, b.Max.X - 0.35f);
        float tz = b.Min.Z + MathF.Min(0.8f, (b.Max.Z - b.Min.Z) / 2);
        pos = new Vector3(Math.Clamp(tx, a.Min.X + 0.05f, a.Max.X - 0.05f), a.Max.Y, a.Max.Z - 0.05f);
        vel = new Vector3(0, a.Kind == 4 ? Room.BounceV : Room.JumpV, 0); height = StandH; grounded = false;
        for (int s = 0; s < 240 * 7; s++)
        {
            Vector3 d = new(tx - pos.X, 0, tz - pos.Z); float dl = d.Length();
            Vector3 wish = dl > 0.15f ? d / dl * speed : Vector3.Zero;
            if (s == 0) { vel.X = wish.X; vel.Z = wish.Z; }
            Step(1f / 240, wish, 7f);
            if (grounded) return groundBox.Min == b.Min && groundBox.Max == b.Max;
            if (pos.Y < a.Max.Y - 90) return false;
        }
        return false;
    }

    // ---- Bot (nur Test, --bot SEKUNDEN DATEI): spielt den Pfad in der echten Spielschleife ab ----
    static int botIdx, botRespawns; static bool botOn, botWalk; // botWalk: absichtlich zu langsam -> testet Sturz/Respawn
    static void BotOld(out Vector3 wish, out bool crouch, out bool jump)
    {
        var pts = new List<Box>(cur.PathBoxes);
        for (int k = 0; k < 2; k++) { var nb = next.PathBoxes[k]; nb.Min.Z += cur.L; nb.Max.Z += cur.L; pts.Add(nb); }
        if (grounded)
            for (int i = 0; i < pts.Count - 1; i++)
                if (pts[i].Min == groundBox.Min && pts[i].Max == groundBox.Max) { botIdx = i; break; }
        botIdx = Math.Clamp(botIdx, 0, pts.Count - 2);
        Box a = pts[botIdx], b = pts[botIdx + 1];
        float tx = Math.Clamp((a.Min.X + a.Max.X) / 2, b.Min.X + 0.35f, b.Max.X - 0.35f);
        float tz = b.Min.Z + MathF.Min(0.8f, (b.Max.Z - b.Min.Z) / 2);
        bool onA = grounded && pos.Z < a.Max.Z + 0.2f;
        if (onA) { tx = Math.Clamp(tx, a.Min.X + 0.05f, a.Max.X - 0.05f); tz = a.Max.Z - 0.05f; }
        crouch = false;
        foreach (var c in world)
            if (c.Min.Y > pos.Y + 0.5f && c.Min.Y < pos.Y + StandH + 0.1f && pos.X + R > c.Min.X && pos.X - R < c.Max.X
                && pos.Z + 1.3f > c.Min.Z && pos.Z - 1.3f < c.Max.Z) { crouch = true; break; }
        Vector3 d = new(tx - pos.X, 0, tz - pos.Z); float dl = d.Length();
        wish = dl > 0.15f ? d / dl * (crouch ? 3f : botWalk ? 4.5f : Room.SprintSpeed) : Vector3.Zero;
        jump = onA && !crouch && pos.Z >= a.Max.Z - 0.3f;
    }

    // ---- Klangprobe ohne Fenster (--audiotest ORDNER): rendert je 18 s pro Stimmungsstufe als WAV und misst Pegel ----
    static void AudioTest(string dir)
    {
        Directory.CreateDirectory(dir);
        var sb = new System.Text.StringBuilder("KLANGPROBE Dreamy Hellcore (je 18 s, 44,1 kHz Stereo)\n");
        (string name, float t)[] segs = { ("1_traum", 0f), ("2_unbehagen", 0.35f), ("3_grauen", 0.62f), ("4_terror", 0.93f) };
        foreach (var s in segs)
        {
            Audio.T = s.t; Audio.Prox = 0; Audio.Vol = 1; Audio.Tension = s.t; Audio.Music(11); Audio.AmbientSet(3 + (long)(s.t * 40), 0, s.t); Audio.Size = 0.2f + 0.5f * s.t;
            int blocks = 18 * Audio.SR / Audio.BL; var data = new short[blocks * Audio.BL * 2];
            for (int b = 0; b < blocks; b++)
            {
                double sec = (double)b * Audio.BL / Audio.SR;
                if (b % 17 == 0 && sec < 4) Audio.Step(b % 34 == 0 ? -0.18f : 0.18f, 0.8f);
                if (b == (int)(4.5 * Audio.SR / Audio.BL)) Audio.Chime();
                if (s.t > 0.5f && b == (int)(7 * Audio.SR / Audio.BL)) Audio.Flash(); if (s.t > 0.5f && b == (int)(10 * Audio.SR / Audio.BL)) Audio.Scream(1f); if (b == (int)(15 * Audio.SR / Audio.BL)) Audio.Choir(2.5f);
                if (s.t > 0.9f) { Audio.Prox = (float)Math.Clamp((sec - 11) / 3, 0, 1) * (sec < 14.2 ? 1 : 0); if (b == (int)(14.2 * Audio.SR / Audio.BL)) Audio.Touch(); }
                Audio.Fill(); Array.Copy(Audio.Buf, 0, data, b * Audio.BL * 2, Audio.BL * 2);
            }
            double sum = 0; int peak = 0, hot = 0; bool bad = false;
            foreach (short v in data) { int a = Math.Abs((int)v); sum += (double)v * v; if (a > peak) peak = a; if (a > 27000) hot++; }
            double rms = Math.Sqrt(sum / data.Length) / 32768.0; if (double.IsNaN(rms)) bad = true;
            sb.AppendLine(FormattableString.Invariant($"{s.name}: RMS {20 * Math.Log10(Math.Max(rms, 1e-9)):F1} dBFS, Spitze {20 * Math.Log10(Math.Max(peak, 1) / 32768.0):F1} dBFS, {100.0 * hot / data.Length:F2} % nahe Vollaussteuerung{(bad ? "  FEHLER: NaN" : "")}"));
            using var bw = new BinaryWriter(File.Create(Path.Combine(dir, "klangprobe_" + s.name + ".wav")));
            int bytes = data.Length * 2;
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + bytes); bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); bw.Write(16);
            bw.Write((short)1); bw.Write((short)2); bw.Write(Audio.SR); bw.Write(Audio.SR * 4); bw.Write((short)4); bw.Write((short)16);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data")); bw.Write(bytes);
            foreach (short v in data) bw.Write(v);
        }
        File.WriteAllText(Path.Combine(dir, "klangprobe.txt"), sb.ToString());
    }

    static void SelfTest(string outPath)
    {
        Room.NoGpu = true; noSave = true;
        var sb = new System.Text.StringBuilder(); int fails = 0, jumps = 0, walkOk = 0, railN = 0, pzFails = 0;
        var list = new List<long>();
        for (long i = 1; i <= 60; i++) list.Add(i);
        list.AddRange(new long[] { 64, 100, 101, 250, 500, 999, 1000, 2500, 5000, 9999, 10000, 10001, 50000, 1000000, 123456789012L });
        float lastW = 0;
        foreach (long n in list)
        {
            var c = new Room(n); var nx = new Room(n + 1);
            world.Clear(); world.AddRange(c.Col); world.AddRange(c.Dyn); Vector3 no = new(0, c.ExitY, c.L);
            foreach (var b in nx.Col) { var q = b; q.Min += no; q.Max += no; world.Add(q); }
            var pts = new List<Box>(c.PathBoxes); var nb = nx.PathBoxes[0]; nb.Min += no; nb.Max += no; pts.Add(nb);
            int f = 0;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                if (pts[i].Rail) { railN++; continue; }   // Schiene traegt automatisch
                jumps++;
                if (TryJump(pts[i], pts[i + 1], Room.WalkSpeed)) walkOk++;
                if (!TryJump(pts[i], pts[i + 1], Room.SprintSpeed))
                {
                    f++;
                    sb.AppendLine(FormattableString.Invariant($"  FEHLER Hellstage {n} Sprung {i}: von z={pts[i].Max.Z:F1} y={pts[i].Max.Y:F1} nach z={pts[i + 1].Min.Z:F1} y={pts[i + 1].Max.Y:F1}"));
                }
            }
            sb.AppendLine(FormattableString.Invariant($"Hellstage {n}: {pts.Count - 1} Spruenge, {f} Fehler | Raum {c.W:F1} x {c.H:F1} x {c.L:F1} m, {c.Col.Count} Kollider, Typ {Room.ThemeNames[c.Theme]}{(c.DejaVu ? " (Deja-vu)" : "")}, {c.Dyn.Count} dynamisch, {c.Rails.Count} Schienen, Ausgang {c.ExitY:F0} m{(c.Escape > 0 ? $", ESCAPE {Room.EscapeNames[c.Escape]} / {Room.StyleNames[c.Style]}, {c.Seals.Count} Siegel, {c.Plates.Count} Platten" : "")}{(c.W > lastW ? "" : "  WARNUNG: nicht groesser als vorher")}"));
            if (c.Escape >= 11) { string res = SolveByHints(c); sb.AppendLine("  Raetsel " + Room.EscapeNames[c.Escape] + ": " + res); if (res.StartsWith("NICHT")) pzFails++; }
            lastW = c.W; fails += f;
        }
        // Determinismus: gleicher Seed -> identischer Pfad
        var r1 = new Room(777); var r2 = new Room(777); bool same = r1.PathBoxes.Count == r2.PathBoxes.Count;
        for (int i = 0; same && i < r1.PathBoxes.Count; i++) same = r1.PathBoxes[i].Min == r2.PathBoxes[i].Min && r1.PathBoxes[i].Max == r2.PathBoxes[i].Max;
        sb.Insert(0, FormattableString.Invariant($"SELBSTTEST Dreamy Hellcore: {jumps} Spruenge simuliert, {fails} mit Sprint NICHT schaffbar, {walkOk} auch ohne Sprint schaffbar. Determinismus: {(same ? "OK" : "FEHLER")}. Raetsel nicht per Hinweis loesbar: {pzFails}\n"));
        File.WriteAllText(outPath, sb.ToString());
    }

    // ---- Render-Ziele ----
    static RenderTexture2D MakeRT(int w, int h, bool hdr, bool depthTex)
    {
        RenderTexture2D t = new RenderTexture2D();
        t.Id = Rlgl.LoadFramebuffer();
        Rlgl.EnableFramebuffer(t.Id);
        var fmt = hdr ? PixelFormat.UncompressedR16G16B16A16 : PixelFormat.UncompressedR8G8B8A8;
        t.Texture.Id = Rlgl.LoadTexture(null, w, h, fmt, 1);
        t.Texture.Width = w; t.Texture.Height = h; t.Texture.Format = fmt; t.Texture.Mipmaps = 1;
        Rlgl.FramebufferAttach(t.Id, t.Texture.Id, FramebufferAttachType.ColorChannel0, FramebufferAttachTextureType.Texture2D, 0);
        if (depthTex)
        {
            t.Depth.Id = Rlgl.LoadTextureDepth(w, h, false);
            t.Depth.Width = w; t.Depth.Height = h; t.Depth.Format = (PixelFormat)19; t.Depth.Mipmaps = 1;
            Rlgl.FramebufferAttach(t.Id, t.Depth.Id, FramebufferAttachType.Depth, FramebufferAttachTextureType.Texture2D, 0);
        }
        if (!Rlgl.FramebufferComplete(t.Id)) shaderErrors += "FBO" + w + "x" + h + " ";
        Rlgl.DisableFramebuffer();
        SetTextureFilter(t.Texture, TextureFilter.Bilinear); SetTextureWrap(t.Texture, TextureWrap.Clamp);
        return t;
    }

    const int BloomLevels = 5;
    static RenderTexture2D rtScene, rtLdr; static RenderTexture2D[] rtBloom = new RenderTexture2D[BloomLevels];
    static int tw, th;
    static void EnsureTargets(int w, int h)
    {
        if (w == tw && h == th) return;
        if (tw > 0) { UnloadRenderTexture(rtScene); UnloadRenderTexture(rtLdr); foreach (var b in rtBloom) UnloadRenderTexture(b); }
        tw = w; th = h;
        rtScene = MakeRT(w, h, true, true); rtLdr = MakeRT(w, h, false, false);
        for (int i = 0; i < BloomLevels; i++) rtBloom[i] = MakeRT(Math.Max(2, w >> (i + 1)), Math.Max(2, h >> (i + 1)), true, false);
    }

    static Mesh MakeParticles(int n)
    {
        var rng = new Rng(4242);
        Mesh m = new Mesh(); m.VertexCount = n * 6; m.TriangleCount = n * 2;
        m.Vertices = (float*)MemAlloc((uint)(n * 6 * 3 * sizeof(float)));
        m.TexCoords = (float*)MemAlloc((uint)(n * 6 * 2 * sizeof(float)));
        m.Colors = (byte*)MemAlloc((uint)(n * 6 * 4));
        float[] cx = { -1, 1, 1, -1, 1, -1 }, cy = { -1, -1, 1, -1, 1, 1 };
        for (int i = 0; i < n; i++)
        {
            float sx = rng.F(), sy = rng.F(), sz = rng.F();
            byte cr = (byte)(rng.F() * 255), cg = (byte)(rng.F() * 255), cb = (byte)(rng.F() * 255), ca = (byte)(rng.F() * 255);
            for (int k = 0; k < 6; k++)
            {
                int v = i * 6 + k;
                m.Vertices[v * 3] = sx; m.Vertices[v * 3 + 1] = sy; m.Vertices[v * 3 + 2] = sz;
                m.TexCoords[v * 2] = cx[k]; m.TexCoords[v * 2 + 1] = cy[k];
                m.Colors[v * 4] = cr; m.Colors[v * 4 + 1] = cg; m.Colors[v * 4 + 2] = cb; m.Colors[v * 4 + 3] = ca;
            }
        }
        UploadMesh(ref m, false);
        return m;
    }

    // die 8 naechsten Punktlichter (Tore, Checkpoints, Leuchtkoerper) fuer den Szenen-Shader
    static float[] lightPos = new float[32], lightCol = new float[24];
    static List<(Vector3 P, Vector3 C, float R)> lightTmp = new();
    static void GatherLights(Vector3 eye, float time, float mood)
    {
        float die = M.SStep(0.45f, 0.9f, mood);   // Lichter hinter dem Spieler erloeschen: die Dunkelheit folgt
        lightTmp.Clear();
        lightTmp.AddRange(cur.Lights);
        foreach (var l in next.Lights) lightTmp.Add((l.P + new Vector3(0, cur.ExitY, cur.L), l.C, l.R));
        if (prev != null) foreach (var l in prev.Lights) lightTmp.Add((l.P - new Vector3(0, prev.ExitY, prev.L), l.C, l.R));
        lightTmp.Sort((a, b) => (Vector3.Distance(a.P, eye) - a.R).CompareTo(Vector3.Distance(b.P, eye) - b.R));
        for (int i = 0; i < 8; i++)
        {
            bool has = i < lightTmp.Count;
            Vector3 p = has ? lightTmp[i].P : Vector3.Zero, c = has ? lightTmp[i].C : Vector3.Zero;
            float pulse = (0.85f + 0.15f * MathF.Sin(time * 1.7f + p.Z * 0.37f)) * (1f - die * M.SStep(4f, 16f, eye.Z - p.Z));
            float fk = MathF.Sin(MathF.Floor(time * 13) * 91.7f + i * 7.3f) * 43758.5453f; fk -= MathF.Floor(fk);
            if (fk > 0.93f) pulse *= 1f - 0.85f * mood;
            if (fxDarkT > 0) pulse = 0;   // im Terror flackern auch die sicheren Lichter
            lightPos[i * 4] = p.X; lightPos[i * 4 + 1] = p.Y; lightPos[i * 4 + 2] = p.Z; lightPos[i * 4 + 3] = has ? lightTmp[i].R : 1f;
            lightCol[i * 3] = c.X * pulse; lightCol[i * 3 + 1] = c.Y * pulse; lightCol[i * 3 + 2] = c.Z * pulse;
        }
    }

    static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception ex)
        {   // Absturzbericht neben die Programmdatei schreiben (das Spiel hat keine Konsole)
            try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "absturz.txt"), ex.ToString()); } catch { }
            Environment.Exit(1);
        }
    }

    // Waechter (nur in automatischen Laeufen): meldet, in welchem Abschnitt der Hauptschleife das Spiel haengt
    static volatile int phase, beatCount;
    static void Watchdog()
    {
        int last = -1, still = 0;
        while (true)
        {
            System.Threading.Thread.Sleep(500);
            if (beatCount == last) still++; else { still = 0; last = beatCount; }
            if (still >= 8)
            {
                try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "haenger.txt"), $"Haenger in Abschnitt {phase} bei Bild {beatCount}\n"); } catch { }
                Environment.Exit(2);
            }
        }
    }

    static void Run(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        savePath = Path.Combine(AppContext.BaseDirectory, "dreamy_hellcore.sav");
        long start = 1; string shot = null, botFile = null; float argYaw = 0, argPitch = 0, botSeconds = 0, argScale = 0, argZ = 0; bool full = false, argFlash = false;
        try { if (File.Exists(savePath)) start = long.Parse(File.ReadAllText(savePath).Trim(), CultureInfo.InvariantCulture); } catch { start = 1; }
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--stage" && i + 1 < args.Length) long.TryParse(args[++i], out start);
            else if (args[i] == "--screenshot" && i + 1 < args.Length) shot = args[++i];
            else if (args[i] == "--yaw" && i + 1 < args.Length) argYaw = float.Parse(args[++i], CultureInfo.InvariantCulture) * MathF.PI / 180;
            else if (args[i] == "--pitch" && i + 1 < args.Length) argPitch = float.Parse(args[++i], CultureInfo.InvariantCulture) * MathF.PI / 180;
            else if (args[i] == "--scale" && i + 1 < args.Length) argScale = float.Parse(args[++i], CultureInfo.InvariantCulture);
            else if (args[i] == "--fly" && i + 1 < args.Length) argZ = float.Parse(args[++i], CultureInfo.InvariantCulture);
            else if (args[i] == "--fullscreen") full = true;
            else if (args[i] == "--menu") argMenu = true;
            else if (args[i] == "--hint") argHint = true;
            else if (args[i] == "--chat") { chatOpen = true; chatMode = 1; chatText = "100000"; }   // nur fuer Testbilder
            else if (args[i] == "--flash") argFlash = true;
            else if (args[i] == "--audiotest" && i + 1 < args.Length) { AudioTest(args[++i]); return; }
            else if (args[i] == "--botwalk") botWalk = true;
            else if (args[i] == "--selftest" && i + 1 < args.Length) { SelfTest(args[++i]); return; }
            else if (args[i] == "--bot" && i + 2 < args.Length) { botSeconds = float.Parse(args[++i], CultureInfo.InvariantCulture); botFile = args[++i]; botOn = true; }
        }
        noSave = shot != null || botOn;
        bool auto = shot != null || botOn;   // automatischer Lauf: keine Maus, keine Pause

        SetTraceLogLevel(TraceLogLevel.Warning);
        // automatische Laeufe: auch minimiert weiterlaufen (sonst wartet raylib auf Fensterereignisse und der Test steht)
        SetConfigFlags(auto ? ConfigFlags.ResizableWindow | ConfigFlags.AlwaysRunWindow | (botOn ? 0 : ConfigFlags.VSyncHint) : ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
        InitWindow(1280, 720, "Dreamy Hellcore");
        SetExitKey(KeyboardKey.Null);        // Esc beendet das Spiel NICHT mehr (pausiert nur)
        string iconPath = Path.Combine(AppContext.BaseDirectory, "icon.png");   // Fenster-/Taskleisten-Icon (Windows und Linux)
        if (File.Exists(iconPath))
        {   // raylib/GLFW nehmen fuer das Fenstersymbol nur RGBA an; ein RGB-PNG wuerde stillschweigend verworfen
            Image ic = LoadImage(iconPath);
            ImageFormat(ref ic, PixelFormat.UncompressedR8G8B8A8);
            SetWindowIcon(ic);
            UnloadImage(ic);
        }
        SetTargetFPS(botOn ? 0 : 144); LoadUiFont();
        if (full) ToggleBorderlessWindowed();
        if (!auto) { DisableCursor(); Discord.Start(); }

        var shScene = new Sh(Shaders.SceneVS, Shaders.SceneFS, "scene");
        var shDepth = new Sh(Shaders.SceneVS, Shaders.DepthFS, "depth");
        var shPart = new Sh(Shaders.PartVS, Shaders.PartFS, "particles");
        var shShade = new Sh(Shaders.SceneVS, Shaders.ShadeFS, "shade");
        var shDown = new Sh(Shaders.PostVS, Shaders.DownFS, "down");
        var shUp = new Sh(Shaders.PostVS, Shaders.UpFS, "up");
        var shComp = new Sh(Shaders.PostVS, Shaders.CompFS, "composite");
        var shFinal = new Sh(Shaders.PostVS, Shaders.FinalFS, "final");
        if (shaderErrors.Length > 0) try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "shader_fehler.txt"), "Shader nicht kompiliert: " + shaderErrors); } catch { }
        Material matScene = LoadMaterialDefault(); matScene.Shader = shScene.S;
        Material matDepth = LoadMaterialDefault(); matDepth.Shader = shDepth.S;
        Material matPart = LoadMaterialDefault(); matPart.Shader = shPart.S;
        Material matShade = LoadMaterialDefault(); matShade.Shader = shShade.S;
        Mesh particles = MakeParticles(7000);

        InitAudioDevice();
        SetAudioStreamBufferSizeDefault(Audio.BL);
        AudioStream stream = LoadAudioStream(Audio.SR, 16, 2);
        PlayAudioStream(stream);

        LoadStage(start);
        OnRoomEnter(); Audio.Music(16); Mesh figure = Room.Figure(); float stalkY = 0;
        yaw = argYaw; pitch = argPitch;
        if (argZ > 0)
        {   // Testbilder: auf die Plattform unter dieser Stelle setzen (sonst schwebt die Kamera irgendwo)
            pos.Z = argZ; pos.Y = 3f;
            foreach (var pb in cur.PathBoxes) if (argZ >= pb.Min.Z && argZ <= pb.Max.Z) { pos.Y = pb.Max.Y + 0.05f; pos.X = Math.Clamp(pos.X, pb.Min.X + 0.4f, pb.Max.X - 0.4f); }
        }
        Vector3 sFog = cur.Fog, sAbyss = cur.Abyss, sEmis = cur.Emis, sGlow = cur.Glow;
        float sWeird = cur.Weird, sA = cur.Mood, sL = cur.L, sD = cur.D, sCell = cur.Cell, sW = cur.W, fovKick = 0, sPause = 0;
        float lampI = 1, lampOff = 0, flashAge = 99, flashNext = 18, figDist = 0.35f * cur.L, figX = 0, seenTime = 0, sProx = 0;
        int frame = 0; long botStart = stage; botRespawns = 0; bool paused = argMenu;

        while (!WindowShouldClose())
        {
            float dt = MathF.Min(GetFrameTime(), 0.05f); float time = (float)GetTime(); frame++;
            beatCount = frame; phase = 1;
            if (frame == 1 && auto) new System.Threading.Thread(Watchdog) { IsBackground = true }.Start();
            if (botOn) { dt = 1f / 60; time = frame / 60f; }

            // --- Pause: Esc gibt die Maus frei, Esc oder Klick setzt fort (kein Menue, nur entsaettigtes Bild) ---
            if (!auto)
            {
                if (!chatOpen && IsKeyPressed(KeyboardKey.Escape)) { paused = !paused; if (paused) EnableCursor(); else DisableCursor(); }
                // Klicks im Pausenmenue: siehe Game5.DrawUi
            }
            if (IsKeyPressed(KeyboardKey.F11)) ToggleBorderlessWindowed();
            float simDt = paused ? 0 : dt; simT += simDt;

            bool sprint = false; Vector3 wish = Vector3.Zero; bool frozen = !paused && (chatOpen || Frozen(dt));   // offene Konsole: das Spiel steht
            if (!paused && !frozen)
            {
                // --- Eingabe ---
                if (!auto && frame > 3)   // erste Frames ignorieren (Maussprung beim Einfangen)
                {
                    Vector2 md = GetMouseDelta();
                    yaw -= md.X * 0.0022f; pitch = Math.Clamp(pitch - md.Y * 0.0022f, -1.5f, 1.5f);
                }
                if (IsKeyPressed(KeyboardKey.PageUp)) LoadStage(stage + 1);
                if (IsKeyPressed(KeyboardKey.PageDown)) LoadStage(stage - 1);
                if (IsKeyPressed(KeyboardKey.Home)) LoadStage(stage > long.MaxValue / 10 ? stage : stage * 10);
                if (IsKeyPressed(KeyboardKey.End)) LoadStage(stage / 10);

                bool wantCrouch = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl) || IsKeyDown(KeyboardKey.C);
                Vector3 botWish = Vector3.Zero; bool botJump = false;
                if (botOn) Bot(out botWish, out wantCrouch, out botJump);
                if (wantCrouch) height = CrouchH;
                else if (height < StandH)
                {   // nur aufstehen, wenn ueber dem Kopf Platz ist
                    bool blocked = false;
                    foreach (var b in world) if (Hit(b, StandH)) { blocked = true; break; }
                    if (!blocked) height = StandH;
                }
                bool crouched = height < StandH;
                sprint = (IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift)) && !crouched;
                float speed = crouched ? 3f : sprint ? Room.SprintSpeed : Room.WalkSpeed;
                Vector3 fwd = new(MathF.Sin(yaw), 0, MathF.Cos(yaw)), right = new(-MathF.Cos(yaw), 0, MathF.Sin(yaw));
                if (IsKeyDown(KeyboardKey.W) || IsKeyDown(KeyboardKey.Up)) wish += fwd;
                if (IsKeyDown(KeyboardKey.S) || IsKeyDown(KeyboardKey.Down)) wish -= fwd;
                if (IsKeyDown(KeyboardKey.D) || IsKeyDown(KeyboardKey.Right)) wish += right;
                if (IsKeyDown(KeyboardKey.A) || IsKeyDown(KeyboardKey.Left)) wish -= right;
                if (wish.LengthSquared() > 0) wish = Vector3.Normalize(wish) * speed;
                if (fxMirrorT > 0) wish = 2 * Vector3.Dot(wish, fwd) * fwd - wish;   // Steuerung kurz gespiegelt (links/rechts vertauscht)
                if (IsKeyPressed(KeyboardKey.Space)) jumpBuf = 0.14f;
                if (botOn) { wish = botWish; sprint = !crouched; if (botJump) jumpBuf = 0.14f; yaw = MathF.Atan2(wish.X, MathF.Max(0.01f, wish.Z)) * 0.5f; }

                // --- Physik in festen Teilschritten ---
                if (shot != null && !botOn) { vel = Vector3.Zero; }   // Standbild fuer Screenshots
                else
                {
                    // Mitfahren auf bewegten Plattformen, dann die Kollider dieses Bildes aufbauen
                    if (grounded && groundKey >= 0) { Vector3 no = DynOffset(Slot(groundKey / 10000), groundKey % 10000, simT, out _, out _); pos += no - groundOff; groundOff = no; }
                    UpdateWorld();
                    gravNow = cur.Grav * (lurchT > 0 ? 0.3f : 1f);
                    Vector3 windV = cur.Wind * (0.55f + 0.45f * MathF.Sin(simT * 0.9f) + 0.3f * MathF.Sin(simT * 2.7f)); windNow = windV;
                    if (ride != null) UpdateRide(dt);
                    int sub = ride != null ? 0 : Math.Max(1, (int)MathF.Ceiling(dt / (1f / 240f)));
                    float h = dt / Math.Max(1, sub);
                    for (int i = 0; i < sub; i++)
                    {
                        coyote = grounded ? 0.12f : coyote - h; jumpBuf -= h;
                        if (jumpBuf > 0 && coyote > 0) { vel.Y = Room.JumpV; jumpBuf = 0; coyote = 0; Audio.Jump(); }
                        bool gPrev = grounded; float vyPre = vel.Y;
                        Step(h, wish + windV * (grounded ? 0.15f : 1f), grounded ? 14f : 7f);
                        if (grounded && !gPrev && vyPre < -3f) Audio.Land(MathF.Min(1f, -vyPre / 14f));
                        if (grounded) OnGround(h); else groundKey = -1;
                        if (ride != null) break;
                        if (grounded)
                        {
                            stepDist += MathF.Sqrt(vel.X * vel.X + vel.Z * vel.Z) * h;
                            if (stepDist > (sprint ? 2.7f : 2.0f)) { stepDist = 0; stepSide = -stepSide; Audio.Step(0.18f * stepSide, crouched ? 0.35f : 0.8f); }
                        }
                        if (grounded && groundBox.Checkpoint)
                        {
                            Vector3 nr = new((groundBox.Min.X + groundBox.Max.X) / 2, groundBox.Max.Y + 0.05f, (groundBox.Min.Z + groundBox.Max.Z) / 2);
                            if (Vector3.DistanceSquared(nr, respawn) > 4f) OnCheckpoint(groundBox.Fake);   // neuer Checkpoint: ein Klang der Erleichterung
                            respawn = nr;
                        }
                    }
                    // Schritte, die weitergehen, wenn man stehen bleibt (nur in spaeten Stages)
                    float hs = MathF.Sqrt(vel.X * vel.X + vel.Z * vel.Z);
                    if (hs > 2f && grounded) moveTime += dt;
                    if (hs < 0.5f)
                    {
                        if (moveTime > 2.5f && sA > 0.55f && grng.NextDouble() < 0.6) { ghostSteps = 2 + grng.Next(2); ghostTimer = 0.4f; ghostPan = grng.NextDouble() < 0.5 ? -0.75f : 0.75f; }
                        moveTime = 0;
                    }
                    if (ghostSteps > 0) { ghostTimer -= dt; if (ghostTimer <= 0) { Audio.Step(ghostPan, 0.6f); ghostSteps--; ghostTimer = 0.44f; } }
                    // kein Tod: Sturz -> zurueck zum letzten Checkpoint
                    if (ride == null && (pos.Y < cur.KillY || pos.Y < lastGroundY - 75f)) Respawn();
                }

                // --- Raumwechsel (nahtlos, Koordinaten werden auf den neuen Raum umgerechnet) ---
                if (pos.Z > cur.L + 0.5f)   // 0,5 m Hysterese: kein Hin-und-her-Generieren an der Raumgrenze
                {
                    float sh = cur.L, sy = cur.ExitY; prev?.Dispose(); prev = cur; cur = next; stage++; next = new Room(stage + 1);
                    pos.Z -= sh; pos.Y -= sy; respawn.Z -= sh; respawn.Y -= sy; lastGroundY -= sy; stalkY -= sy; BuildWorld(); Save(); botIdx = 0; OnRoomEnter();
                }
                else if (pos.Z < -0.5f && stage > 1)
                {
                    next.Dispose(); next = cur; cur = prev; stage--; prev = stage > 1 ? new Room(stage - 1) : null;
                    pos.Z += cur.L; pos.Y += cur.ExitY; respawn.Z += cur.L; respawn.Y += cur.ExitY; lastGroundY += cur.ExitY; stalkY += cur.ExitY; BuildWorld(); Save(); OnRoomEnter();
                }
            }

            phase = 2;
            // --- weiche Uebergaenge der Atmosphaere ---
            float sm = 1 - MathF.Exp(-dt * 1.5f);
            sFog = Vector3.Lerp(sFog, cur.Fog, sm); sAbyss = Vector3.Lerp(sAbyss, cur.Abyss, sm);
            sEmis = Vector3.Lerp(sEmis, cur.Emis, sm); sGlow = Vector3.Lerp(sGlow, cur.Glow, sm);
            sWeird += (cur.Weird - sWeird) * sm; sA += (cur.Mood - sA) * sm; sL += (cur.L - sL) * sm; sD += (cur.D - sD) * sm;
            sCell += (cur.Cell - sCell) * sm; sW += (cur.W - sW) * sm;
            sT += (cur.Terror - sT) * sm; sHeaven += ((cur.Heaven ? 1f : 0f) - sHeaven) * MathF.Min(1, dt * 0.9f);
            sPause += ((paused ? 1f : 0f) - sPause) * MathF.Min(1, dt * 8);
            fade = MathF.Min(1, fade + dt * 2.2f);
            eyeH += (height - 0.12f - eyeH) * MathF.Min(1, dt * 14);
            fovKick += ((sprint && wish.LengthSquared() > 0 ? 7f : 0f) - fovKick) * MathF.Min(1, dt * 6);
            float beat = Audio.BeatEnv;
            float dream = 1 - M.SStep(0f, 0.35f, sA);

            // --- Kamera ---
            Vector3 eye = pos + new Vector3(0, eyeH, 0) + fxShake;
            Vector3 look = new(MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Cos(yaw) * MathF.Cos(pitch));
            float rollA = 0.07f * sWeird * MathF.Sin(time * 0.31f) + fxRoll + sTilt + rideBank;
            Vector3 up = Vector3.Transform(Vector3.UnitY, Quaternion.CreateFromAxisAngle(look, rollA));
            float fov = 80f + fovKick + 5f * sWeird * MathF.Sin(time * 0.23f) + 1.5f * beat * sA + fxFov + (ride != null ? MathF.Min(14f, rideV * 0.5f) : 0);
            Camera3D cam = new Camera3D(eye, eye + look, up, fov, CameraProjection.Perspective);
            Vector3 camRight = Vector3.Normalize(Vector3.Cross(look, up)), camUp = Vector3.Cross(camRight, look);

            // --- das eigene Licht: im Traum ein weiches Mondlicht, im Terror klein, warm und unzuverlaessig ---
            if (!paused && !frozen) { EscapeTick(dt, look, sprint); Direct(dt, sT, look); }   // Regisseur: Spannungswellen und Ereignisse
            if (fxLampOff > 0) { lampOff = fxLampOff; fxLampOff = 0; Audio.LampClick(); }
            if (fxStalk) { figDist = MathF.Max(4f, figDist * 0.3f); fxStalk = false; }
            if (sA <= 0.6f) { if (lampOff > 0 && !paused) { lampOff -= dt; if (lampOff <= 0) Audio.LampClick(); } }
            else if (!paused)
            {
                if (lampOff > 0) { lampOff -= dt; if (lampOff <= 0) Audio.LampClick(); }
                else if (grng.NextDouble() < dt * 0.045 * M.SStep(0.6f, 1f, sA)) { lampOff = 0.5f + 2.2f * (float)grng.NextDouble(); Audio.LampClick(); }
            }
            float fl = MathF.Sin(MathF.Floor(time * 13) * 91.7f) * 43758.5453f; fl -= MathF.Floor(fl);
            lampI += ((lampOff > 0 ? 0f : 1f - 0.5f * sA * (fl > 0.9f ? 1 : 0)) - lampI) * MathF.Min(1, dt * 18);
            Vector3 lampC = Vector3.Lerp(new Vector3(0.62f, 0.68f, 0.95f) * 0.55f, new Vector3(1.0f, 0.74f, 0.5f) * 0.38f, M.SStep(0.2f, 0.7f, sA)) * lampI;
            float lampR = M.Lerp(10f, 4.8f, sA);

            // --- Blitz: selten, zeigt fuer einen Moment den ganzen Raum - und wer darin steht ---
            if (fxFlashNow) { flashAge = 0; fxFlashNow = false; Audio.Flash(); }   // Blitze loest jetzt der Regisseur aus
            flashAge += dt;
            float flash = flashAge < 1.2f ? MathF.Exp(-flashAge * 9) + (flashAge > 0.18f ? 0.8f * MathF.Exp(-(flashAge - 0.18f) * 13) : 0) : 0;
            if (argFlash) flash = 1;

            // --- der Verfolger: steht voraus im Nebel. Solange man ihn ansieht, ruehrt er sich nicht.
            //     Sieht man weg, kommt er naeher. Erreicht er den Spieler: Dunkelheit, ein Schrei - kein Tod. ---
            float figFar = 0.35f * sL, hunt = M.SStep(0.45f, 1f, sT);
            bool figOn = sT > 0.22f;
            if (figDist > figFar) figDist = figFar;
            if (!paused && figOn)
            {
                figX += (pos.X - figX) * MathF.Min(1, dt * 0.6f); stalkY += (pos.Y - stalkY) * MathF.Min(1, dt * 0.8f);
                Vector3 toFig = Vector3.Normalize(new Vector3(figX, stalkY + 1.6f, eye.Z + figDist) - eye);
                bool seen = Vector3.Dot(look, toFig) > 0.72f;
                seenTime = seen ? seenTime + dt : 0;
                if (!seen) figDist -= hunt * 5.5f * dt;
                else if (seenTime > 3f || hunt <= 0) figDist += 2.5f * dt;
                figDist = Math.Clamp(figDist, 1f, figFar);
                if (figDist < 2f && hunt > 0) { Audio.Touch(); fade = 0; figDist = figFar; lampOff = 1.8f; }
            }
            float prox = figOn && hunt > 0 ? M.C01(1 - (figDist - 2f) / 14f) : 0;
            sProx += (prox - sProx) * MathF.Min(1, dt * 4);
            Audio.T = sA; Audio.Prox = sProx; Audio.Vol = 1f - 0.7f * sPause; Audio.Tension = tension;
            float hv = sHeaven * (slipT > 0 || flash > 0.1f ? 0f : 1f); Audio.Heaven = hv;   // Himmel-Anteil; 0, solange die Maske verrutscht ist
            Audio.Size = M.C01(MathF.Log10(MathF.Max(16f, sW) / 16f) / 1.4f);

            // --- Render-Ziele (interne Hoehe auf 1440 begrenzt, ausser --scale) ---
            int sw = GetRenderWidth(), shh = GetRenderHeight();
            float scale = argScale > 0 ? argScale : MathF.Min(1f, 1440f / MathF.Max(1, shh));
            int rw = Math.Max(16, (int)(sw * scale)), rh = Math.Max(16, (int)(shh * scale));
            EnsureTargets(rw, rh);

            // --- Uniforms ---
            float fog1 = 0.7f * sL * (1 + 1.4f * hv);   // im falschen Himmel reicht der Blick weiter
            Vector3 Lg = Vector3.Normalize(new Vector3(0, 0.10f, 1));
            Vector2 sp = GetWorldToScreen(eye + Lg * 1000f, cam);
            Vector3 sun = new(sp.X / MathF.Max(1, GetScreenWidth()), 1 - sp.Y / MathF.Max(1, GetScreenHeight()), M.SStep(0.05f, 0.5f, Vector3.Dot(look, Lg)));
            // Aussehen dieser Stufe. Im falschen Himmel "verrutscht die Maske" bei Blitz und Fehlern: kurz ist die Hoelle darunter zu sehen.
            float slip = sHeaven - hv;
            Vector3 lkFog = Vector3.Lerp(sFog, new Vector3(0.035f, 0.004f, 0.003f), slip), lkAbyss = Vector3.Lerp(sAbyss, new Vector3(0.25f, 0.02f, 0f), slip);
            Vector3 lkGlow = Vector3.Lerp(sGlow, new Vector3(0.6f, 0.05f, 0.02f) * 0.3f, slip) * (1 + 3f * watchU);
            float lkA = M.Lerp(sA, 0.95f, slip), lkDream = dream * (1 - slip);
            GatherLights(eye, time, sA);
            foreach (var s in new[] { shScene, shDepth }) { s.F("uTime", time); s.F("uWeird", sWeird); s.F("uShift", fxShift); s.F("uGone", fxGoneT > 0 ? 1 : 0); s.V3("uCam", eye); }
            shShade.F("uTime", time); shShade.F("uWeird", 0); shShade.F("uShift", 0); shShade.V3("uCam", eye); shShade.V3("uFog", lkFog); shShade.V3("uGlow", lkGlow); shShade.F("uHeaven", hv);
            shShade.V3("uRes", new Vector3(rw, rh, 0)); shShade.F("uFog0", 0.12f * fog1); shShade.F("uFog1", fog1); shShade.F("uA", sA); shShade.F("uFlash", flash);
            shScene.V3("uCam", eye); shScene.V3("uFog", lkFog); shScene.V3("uAbyss", lkAbyss); shScene.V3("uEmis", sEmis); shScene.V3("uGlow", lkGlow);
            shScene.F("uFog0", 0.12f * fog1); shScene.F("uFog1", fog1); shScene.F("uDepth", sD);
            shScene.V3("uLampC", lampC); shScene.F("uLampR", lampR); shScene.F("uA", lkA); shScene.F("uDream", lkDream); shScene.F("uBeat", beat);
            shScene.F("uCell", sCell); shScene.F("uEyes", MathF.Max(fxEyes, watchU)); shScene.F("uHeaven", hv); shScene.F("uFront", frontOn ? frontZ : -99999f); shScene.F("uDark", fxDarkT > 0 ? 1 : 0); shScene.F("uFlash", flash * (1 - sHeaven)); shScene.F("uDie", M.SStep(0.45f, 0.9f, sA)); shScene.V3("uRes", new Vector3(rw, rh, 0));
            fixed (float* lp = lightPos) SetShaderValueV(shScene.S, shScene.Loc("uLP"), lp, ShaderUniformDataType.Vec4, 8);
            fixed (float* lc = lightCol) SetShaderValueV(shScene.S, shScene.Loc("uLC"), lc, ShaderUniformDataType.Vec3, 8);
            float box = Math.Clamp(26f + sW * 0.35f, 30f, 180f);
            shPart.V3("uCam", eye); shPart.V3("uRight", camRight); shPart.V3("uUp", camUp); shPart.V3("uBox", new Vector3(box, box, box));
            // Partikel: im Traum Gluehwuermchen, dann fallende Asche, im Terror nur noch Staub
            shPart.F("uTime", time); shPart.F("uRise", M.Lerp(0.15f, -0.45f, M.SStep(0.25f, 0.6f, sA)) * (1 - 0.8f * M.SStep(0.8f, 1f, sA)));
            shPart.F("uSize", M.Lerp(0.055f, 0.022f, M.SStep(0.15f, 0.45f, sA)) * box / 30f);
            shPart.F("uDensity", M.Lerp(M.Lerp(0.22f, 0.6f, M.SStep(0.25f, 0.6f, sA)), 0.12f, M.SStep(0.75f, 1f, sA)));
            shPart.V3("uPartCol", M.Key(sA, new Vector3(1.0f, 0.85f, 0.55f) * 2.4f, new Vector3(0.75f, 0.8f, 0.55f) * 0.9f,
                new Vector3(0.9f, 0.35f, 0.15f) * 0.5f, new Vector3(0.4f, 0.38f, 0.38f) * 0.14f, new Vector3(0.3f, 0.3f, 0.34f) * 0.08f));

            // --- 1) Szene in das HDR-Ziel: Tiefen-Vorpass, dann Farbe, dann Partikel ---
            phase = 3;
            BeginTextureMode(rtScene);
            ClearBackground(Color.Black);
            Rlgl.SetClipPlanes(0.08, 6000.0);
            BeginMode3D(cam);
            Rlgl.DisableBackfaceCulling();
            Rlgl.DisableColorBlend();
            Matrix4x4 mCur = Matrix4x4.Identity, mNext = Matrix4x4.Identity, mPrev = Matrix4x4.Identity, mFig = Matrix4x4.Identity;
            mNext.M34 = cur.L; mNext.M24 = cur.ExitY; if (prev != null) { mPrev.M34 = -prev.L; mPrev.M24 = -prev.ExitY; }
            float figH = 2.9f + 2.6f * sA;
            mFig.M11 = figH; mFig.M22 = figH; mFig.M33 = figH;
            mFig.M14 = figX; mFig.M24 = stalkY - 0.15f; mFig.M34 = eye.Z + figDist;
            for (int pass = 0; pass < 2; pass++)
            {
                Material mt = pass == 0 ? matDepth : matScene;
                if (pass == 0) Rlgl.ColorMask(false, false, false, false); else Rlgl.ColorMask(true, true, true, true);
                DrawMesh(cur.Mesh, mt, mCur);
                DrawMesh(next.Mesh, mt, mNext);
                if (prev != null) DrawMesh(prev.Mesh, mt, mPrev);
                DrawDyn(mt);
            }
            // Schattengestalten erst nach dem Tiefen-Vorpass (sie verwerfen Pixel, das Distanzfeld zeichnet die Silhouette)
            if (cur.HasShades) DrawMesh(cur.ShadeMesh, matShade, mCur);
            if (next.HasShades) DrawMesh(next.ShadeMesh, matShade, mNext);
            if (prev != null && prev.HasShades) DrawMesh(prev.ShadeMesh, matShade, mPrev);
            if (flash > 0.12f || sHeaven > 0.5f)   // im falschen Himmel steht der Chor immer da
            {   // die stumme Menge existiert nur fuer die Dauer eines Blitzes
                if (cur.HasCrowd) DrawMesh(cur.CrowdMesh, matShade, mCur);
                if (next.HasCrowd) DrawMesh(next.CrowdMesh, matShade, mNext);
                if (prev != null && prev.HasCrowd) DrawMesh(prev.CrowdMesh, matShade, mPrev);
            }
            if (figOn) DrawMesh(figure, matShade, mFig);
            Rlgl.EnableColorBlend();
            Rlgl.SetBlendFactors(1, 1, 0x8006);   // GL_ONE, GL_ONE, GL_FUNC_ADD
            BeginBlendMode(BlendMode.Custom);
            Rlgl.DisableDepthMask();
            DrawMesh(particles, matPart, Matrix4x4.Identity);
            Rlgl.EnableDepthMask();
            EndBlendMode();
            EndMode3D();
            EndTextureMode();

            phase = 4;
            // --- 2) Bloom: 5 Stufen runter, dann additiv wieder hoch ---
            for (int i = 0; i < BloomLevels; i++)
            {
                var t = rtBloom[i];
                BeginTextureMode(t); BeginShaderMode(shDown.S);
                shDown.Tex("uSrc", i == 0 ? rtScene.Texture : rtBloom[i - 1].Texture);
                shDown.V3("uRes", new Vector3(t.Texture.Width, t.Texture.Height, i == 0 ? 1 : 0));
                DrawRectangle(0, 0, t.Texture.Width, t.Texture.Height, Color.White);
                EndShaderMode(); EndTextureMode();
            }
            for (int i = BloomLevels - 2; i >= 0; i--)
            {
                var t = rtBloom[i];
                BeginTextureMode(t); BeginBlendMode(BlendMode.Additive); BeginShaderMode(shUp.S);
                shUp.Tex("uSrc", rtBloom[i + 1].Texture);
                shUp.V3("uRes", new Vector3(t.Texture.Width, t.Texture.Height, 0));
                DrawRectangle(0, 0, t.Texture.Width, t.Texture.Height, Color.White);
                EndShaderMode(); EndBlendMode(); EndTextureMode();
            }

            // --- 3) Composite: SSAO, Lichtstrahlen, Bloom, Linse, Tonemapping ---
            BeginTextureMode(rtLdr); BeginShaderMode(shComp.S);
            shComp.Tex("uScene", rtScene.Texture); shComp.Tex("uDepth", rtScene.Depth); shComp.Tex("uBloom", rtBloom[0].Texture);
            shComp.V3("uRes", new Vector3(rw, rh, 0)); shComp.V3("uSun", new Vector3(sun.X, sun.Y, sun.Z * (1 - 0.7f * hv)));
            shComp.F("uTime", time); shComp.F("uWeird", sWeird); shComp.F("uA", sA); shComp.F("uPause", sPause);
            shComp.F("uTanFov", MathF.Tan(fov * MathF.PI / 360f)); shComp.F("uBeat", beat); shComp.F("uExposure", M.Lerp(1.1f, 0.9f, sA) * (1 - 0.42f * hv));
            shComp.F("uDream", lkDream * (1 - 0.75f * hv)); shComp.F("uInvert", fxInvertT > 0 ? 1 : 0); shComp.F("uGlitch", fxGlitch); shComp.F("uSweep", fxSweep); shComp.F("uProx", sProx); shComp.F("uFlash", flash);
            DrawRectangle(0, 0, rw, rh, Color.White);
            EndShaderMode(); EndTextureMode();

            // --- 4) Bildschirm: FXAA, Korn, Vignette. Kein HUD. ---
            phase = 6;
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginShaderMode(shFinal.S);
            shFinal.Tex("uSrc", rtLdr.Texture);
            shFinal.V3("uRes", new Vector3(sw, shh, 0)); shFinal.V3("uSrcRes", new Vector3(rw, rh, 0));
            shFinal.F("uTime", time); shFinal.F("uFade", fade); shFinal.F("uA", sA); shFinal.F("uProx", sProx);
            DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), Color.White);
            EndShaderMode();
            DrawUi(stage, ref paused, dt, cam);   // Hellstage-Anzeige und Pausenmenue
            EndDrawing();
            if (menuReset) { menuReset = false; LoadStage(1); paused = false; DisableCursor(); }   // Fortschritt zuruecksetzen
            if (chatLoad > 0) { long cl = chatLoad; chatLoad = 0; LoadStage(cl); }   // versteckte Konsole: lvlchoice
            if (menuSkip)
            {   // Raetsel bzw. Pruefung ueberspringen: Tor oeffnen, Dunkelheitsfront abschalten
                menuSkip = false; OpenGate(); frontOn = false; frontZ = -99999f; Audio.Front = 0; Audio.Watch = 0; paused = false; DisableCursor();
            }
            if (menuQuit) break;

            // --- Audio ---
            phase = 7;
            for (int i = 0; i < 2; i++)
                if (IsAudioStreamProcessed(stream)) { phase = 8; Audio.Fill(); phase = 9; fixed (short* p = Audio.Buf) UpdateAudioStream(stream, p, Audio.BL); }
            phase = 10;

            if (botOn && frame % 60 == 0)   // Diagnose: eine Zeile pro Spielsekunde
                File.AppendAllText(botFile + ".log", FormattableString.Invariant($"spiel {frame / 60} s  echt {GetTime():F1} s  stage {stage} z {pos.Z:F0}  blitz {flash:F2}  verfolger {figDist:F1} m  lampe {lampI:F2}\n"));
            if (botOn && frame >= botSeconds * 60)
            {
                File.WriteAllText(botFile, FormattableString.Invariant($"BOT-LAUF: Start Hellstage {botStart}, nach {botSeconds:F0} s Spielzeit in Hellstage {stage} (z={pos.Z:F1} von {cur.L:F1} m), {stage - botStart} Raumwechsel, {botRespawns} Respawns, {frame / Math.Max(0.001, GetTime()):F0} Bilder/s ungebremst\n"));
                if (shot != null) { Image bi = LoadImageFromScreen(); ExportImage(bi, shot); UnloadImage(bi); }
                break;
            }
            if (shot != null && !botOn && frame == 60)
            {
                Image img = LoadImageFromScreen(); ExportImage(img, shot); UnloadImage(img);
                break;
            }
        }
        if (!auto) Save();
        Discord.Stop();
        CloseAudioDevice();
        CloseWindow();
    }
}
