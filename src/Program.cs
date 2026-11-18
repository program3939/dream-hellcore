// Dreamy Hellcore - endloses, prozedurales Horror-Jump-and-Run.
// Eine Datei: Generator (Room), Spielerphysik, Shader, Audio-Drone, Hauptschleife.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace DreamyHellcore;

/// <summary>Kollisionsquader. Kind: 0 fest, 1 bewegt (Amp*sin), 2 verschwindend (Rate = Periode), 3 broeckelnd, 4 Sprungfeld, 5 Foerderband (Amp = Schub).</summary>
struct Box { public Vector3 Min, Max; public bool Checkpoint, Rail, Fake; public byte Kind; public Vector3 Amp; public float Rate, Phase; public int Dyn; }

/// <summary>SplitMix64 - plattformunabhaengig deterministisch.</summary>
sealed class Rng
{
    ulong s;
    public Rng(ulong seed) { s = seed * 0x9E3779B97F4A7C15UL + 0x1234567UL; Next(); Next(); }
    public ulong Next()
    {
        s += 0x9E3779B97F4A7C15UL; ulong z = s;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
    public float F() => (Next() >> 40) / 16777216f;
}

static class M
{
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    public static float C01(float x) => Math.Clamp(x, 0f, 1f);
    public static float Sq(float x) => x * x;
    /// <summary>Farbverlauf ueber die Stimmung: Stuetzstellen bei 0 / 0.35 / 0.6 / 0.85 / 1.0.</summary>
    public static Vector3 Key(float a, Vector3 k0, Vector3 k1, Vector3 k2, Vector3 k3, Vector3 k4)
    {
        float S(float t) { t = C01(t); return t * t * (3 - 2 * t); }
        if (a < 0.35f) return Vector3.Lerp(k0, k1, S(a / 0.35f));
        if (a < 0.6f) return Vector3.Lerp(k1, k2, S((a - 0.35f) / 0.25f));
        if (a < 0.85f) return Vector3.Lerp(k2, k3, S((a - 0.6f) / 0.25f));
        return Vector3.Lerp(k3, k4, S((a - 0.85f) / 0.15f));
    }
    public static float SStep(float e0, float e1, float x) { float t = C01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); }
    public static Vector3 Hsv(float h, float s, float v)
    {
        h -= MathF.Floor(h); s = C01(s); v = C01(v);
        float i = MathF.Floor(h * 6), f = h * 6 - i, p = v * (1 - s), q = v * (1 - f * s), t = v * (1 - (1 - f) * s);
        switch ((int)i % 6)
        {
            case 0: return new Vector3(v, t, p);
            case 1: return new Vector3(q, v, p);
            case 2: return new Vector3(p, v, t);
            case 3: return new Vector3(p, q, v);
            case 4: return new Vector3(t, p, v);
            default: return new Vector3(v, p, q);
        }
    }
}

/// <summary>Ein Raum (= eine Hellstage). Lokale Koordinaten: Z von 0 bis L, Pfad startet bei (0,0,0).</summary>
sealed unsafe partial class Room
{
    public const float JumpV = 8.5f, Gravity = 22f, WalkSpeed = 6f, SprintSpeed = 10f;
    public long N; public float W, H, L, D, A, A1, Weird;
    public Vector3 Wall, Plat, Fog, Emis, Abyss, Glow; public float Cell = 2.5f;
    public List<Box> Col = new();
    public Mesh Mesh;
    List<float> vp = new(), vn = new(), vt = new(); List<byte> vc = new();

    public static void Dims(long n, out float w, out float h, out float l, out float a)
    {
        double s = Math.Sqrt(Math.Min(n, 10000)), ex = Math.Log10(Math.Max(n, 10000) / 10000.0);
        w = (float)(12 + 4 * s + 100 * ex); h = (float)(7 + 2.5 * s + 60 * ex); l = (float)(60 + 6 * s + 150 * ex);
        a = (float)Math.Min(Math.Log10(n) / 4, 1.25);
    }

    public Room(long n)
    {
        N = n; Dims(n, out W, out H, out L, out A); A1 = MathF.Min(A, 1f); D = 4 + H * 0.5f;
        // STIMMUNG (Mood) 0..1: der dramaturgische Bogen. 0 = Traum, ~0.35 = Unbehagen, ~0.6 = Grauen,
        // ab ~0.85 = psychologischer Terror. Steigt langsam mit der Stage (1: 0, 10: 0.23, 25: 0.5, 50: 0.75,
        // 80: 0.9) und ist nie kleiner als die geometrische Abstraktion A.
        Terror = Mood = MathF.Min(1f, MathF.Max(1f - MathF.Exp(-(n - 1) / 12f), A));   // frueh spuerbar: Stage 5: 0.28, 10: 0.53, 20: 0.79, 30: 0.91
        // Escape-Raum: Terror bleibt die echte Stufe (Regisseur, Verfolger), Mood wird zum Aussehen der gewaehlten Design-Stufe
        Escape = EscapeOf(n);
        if (Escape > 0) { Style = StyleOf(n, Terror); Heaven = Style == 0; Mood = Style == 0 ? 0f : Style == 1 ? 0.36f : Style == 2 ? 0.6f : Style == 3 ? 0.85f : 1f; }
        float j = new Rng((ulong)n).F() - 0.5f;
        Vector3 K(Vector3 k0, Vector3 k1, Vector3 k2, Vector3 k3, Vector3 k4) => M.Key(Mood, k0, k1, k2, k3, k4);
        //            Traum                     Unbehagen                  Grauen                     Terror                     Leere
        Wall = K(new(0.40f, 0.38f, 0.60f), new(0.34f, 0.38f, 0.33f), new(0.32f, 0.12f, 0.10f), new(0.15f, 0.13f, 0.14f), new(0.09f, 0.09f, 0.10f));
        Plat = K(new(0.90f, 0.86f, 0.80f), new(0.74f, 0.73f, 0.62f), new(0.60f, 0.50f, 0.45f), new(0.48f, 0.46f, 0.45f), new(0.40f, 0.40f, 0.42f));
        Fog = K(new(0.030f, 0.028f, 0.075f), new(0.024f, 0.036f, 0.032f), new(0.040f, 0.007f, 0.005f), new(0.010f, 0.010f, 0.012f), new(0.003f, 0.003f, 0.004f));
        Emis = K(new(1.00f, 0.80f, 0.62f), new(0.86f, 0.90f, 0.60f), new(1.00f, 0.26f, 0.08f), new(0.90f, 0.10f, 0.05f), new(0.80f, 0.86f, 1.00f));
        Abyss = K(new(0.03f, 0.03f, 0.10f), new(0.03f, 0.05f, 0.04f), new(0.30f, 0.035f, 0.0f), new(0.10f, 0.0f, 0.0f), new(0.012f, 0.0f, 0.0f));
        Glow = K(new Vector3(0.55f, 0.62f, 1.0f) * 0.34f, new Vector3(0.6f, 0.7f, 0.5f) * 0.28f, new Vector3(1.0f, 0.3f, 0.1f) * 0.30f, new Vector3(0.6f, 0.05f, 0.03f) * 0.16f, new Vector3(0.5f, 0.5f, 0.6f) * 0.05f);
        Wall += new Vector3(j, -j * 0.5f, -j) * 0.06f;   // leichte Farbabweichung pro Stage
        if (Heaven)
        {   // falscher Himmel: Elfenbein, Gold, heller Nebel, ein Wolkenmeer statt des Abgrunds
            Wall = new Vector3(0.80f, 0.77f, 0.70f); Plat = new Vector3(0.95f, 0.93f, 0.88f); Fog = new Vector3(0.20f, 0.17f, 0.12f);
            Emis = new Vector3(1.0f, 0.84f, 0.48f); Abyss = new Vector3(0.30f, 0.29f, 0.31f); Glow = new Vector3(1.0f, 0.90f, 0.66f) * 0.38f;
        }
        Weird = M.SStep(0.6f, 1.0f, Mood);                // "atmende" Waende, schwankende Kamera
        Generate();
        if (!NoGpu)
        {
            Upload(); BuildDyn();
            for (int mode = 1; mode <= 2; mode++)
            {   // zwei Meshes: Augenwinkel-Gestalten (immer gezeichnet) und die Blitz-Menge (nur waehrend eines Blitzes)
                var s = new Room(); s.mat = 6; int cnt = 0;
                foreach (var q in shades) if ((int)q.Mode == mode) { s.AddQuad(q.P, q.H, q.Mode); cnt++; }
                if (cnt == 0) continue;
                s.Upload();
                if (mode == 1) { ShadeMesh = s.Mesh; HasShades = true; } else { CrowdMesh = s.Mesh; HasCrowd = true; }
            }
        }
    }
    Room() { }
    public float Mood; public Mesh ShadeMesh, CrowdMesh; public bool HasShades, HasCrowd;
    // Schattengestalten: (Fusspunkt, Hoehe, Modus) - Modus 1 = nur im Augenwinkel sichtbar, 2 = nur im Blitz sichtbar
    readonly List<(Vector3 P, float H, float Mode)> shades = new();
    /// <summary>Die Gestalt, die dem Spieler folgt: ein Rechteck in Einheitsgroesse (Fusspunkt im Ursprung),
    /// die Silhouette selbst zeichnet der Shader (Material 6).</summary>
    public static Mesh Figure()
    {
        var r = new Room(); r.mat = 6;
        r.AddQuad(Vector3.Zero, 1f, 0f);
        r.Upload(); return r.Mesh;
    }
    void AddQuad(Vector3 p, float h, float mode)
    {
        float w = 0.36f * h; Vector3 n = new(0, 0, -1);
        Vector3 bl = new(p.X - w / 2, p.Y, p.Z), br = new(p.X + w / 2, p.Y, p.Z), tr = new(p.X + w / 2, p.Y + h, p.Z), tl = new(p.X - w / 2, p.Y + h, p.Z);
        V(bl, n, 0, 0, 0, 0, mode, 0, 0, w, h); V(br, n, 0, 0, 0, 0, mode, w, 0, w, h); V(tr, n, 0, 0, 0, 0, mode, w, h, w, h);
        V(bl, n, 0, 0, 0, 0, mode, 0, 0, w, h); V(tr, n, 0, 0, 0, 0, mode, w, h, w, h); V(tl, n, 0, 0, 0, 0, mode, 0, h, w, h);
    }
    public static bool NoGpu;
    public List<Box> PathBoxes = new();

    // ---- Mesh-Aufbau -------------------------------------------------------------------------
    // Material-ID (landet im Alpha der Vertexfarbe): 0 Struktur, 1 Plattform, 2 Abgrund/Lava, 3 Gestalt,
    // 4 reines Leuchten, 5 Raumhuelle. Wird vor dem Erzeugen einer Gruppe von Quadern gesetzt.
    int mat;
    List<float> vg = new();
    public List<(Vector3 P, Vector3 C, float R)> Lights = new();
    void V(Vector3 p, Vector3 n, byte r, byte g, byte b, float wob, float em, float tu, float tv, float fw, float fh)
    {
        vp.Add(p.X); vp.Add(p.Y); vp.Add(p.Z); vn.Add(n.X); vn.Add(n.Y); vn.Add(n.Z);
        vc.Add(r); vc.Add(g); vc.Add(b); vc.Add((byte)mat); vt.Add(wob); vt.Add(em);
        vg.Add(tu); vg.Add(tv); vg.Add(fw); vg.Add(fh);
    }
    void AddBox(Vector3 c, Vector3 h, Quaternion q, Vector3 col, float em = 0, float wob = 0)
    {
        Vector3 ax = Vector3.Transform(Vector3.UnitX, q) * h.X, ay = Vector3.Transform(Vector3.UnitY, q) * h.Y,
                az = Vector3.Transform(Vector3.UnitZ, q) * h.Z;
        byte r = (byte)(M.C01(col.X) * 255), g = (byte)(M.C01(col.Y) * 255), b = (byte)(M.C01(col.Z) * 255);
        for (int k = 0; k < 3; k++)
        {
            Vector3 a = k == 0 ? ax : k == 1 ? ay : az, u = k == 0 ? ay : k == 1 ? az : ax, v = k == 0 ? az : k == 1 ? ax : ay;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 o = c + a * s, n = Vector3.Normalize(a) * s;
                float fw = 2 * u.Length(), fh = 2 * v.Length();   // Flaechenmasse in Metern -> Kanten/Fugen/Augen im Shader
                V(o - u - v, n, r, g, b, wob, em, 0, 0, fw, fh); V(o + u - v, n, r, g, b, wob, em, fw, 0, fw, fh); V(o + u + v, n, r, g, b, wob, em, fw, fh, fw, fh);
                V(o - u - v, n, r, g, b, wob, em, 0, 0, fw, fh); V(o + u + v, n, r, g, b, wob, em, fw, fh, fw, fh); V(o - u + v, n, r, g, b, wob, em, 0, fh, fw, fh);
            }
        }
    }
    void Vis(Vector3 mn, Vector3 mx, Vector3 col, float em = 0) => AddBox((mn + mx) * 0.5f, (mx - mn) * 0.5f, Quaternion.Identity, col, em);
    void Solid(Vector3 mn, Vector3 mx, bool cp = false) => Col.Add(new Box { Min = mn, Max = mx, Checkpoint = cp, Dyn = -1 });

    // ---- Generator ---------------------------------------------------------------------------
    static float Reach(float dy, float speed)
    {
        float disc = JumpV * JumpV - 2 * Gravity * dy;
        return speed * (JumpV + MathF.Sqrt(MathF.Max(0, disc))) / Gravity;
    }

    void Upload()
    {
        int n = vp.Count / 3;
        Mesh m = new Mesh(); m.VertexCount = n; m.TriangleCount = n / 3;
        m.Vertices = (float*)MemAlloc((uint)(n * 3 * sizeof(float)));
        m.Normals = (float*)MemAlloc((uint)(n * 3 * sizeof(float)));
        m.TexCoords = (float*)MemAlloc((uint)(n * 2 * sizeof(float)));
        m.Colors = (byte*)MemAlloc((uint)(n * 4));
        m.Tangents = (float*)MemAlloc((uint)(n * 4 * sizeof(float)));
        CollectionsMarshal.AsSpan(vg).CopyTo(new Span<float>(m.Tangents, n * 4));
        vg = null;
        CollectionsMarshal.AsSpan(vp).CopyTo(new Span<float>(m.Vertices, n * 3));
        CollectionsMarshal.AsSpan(vn).CopyTo(new Span<float>(m.Normals, n * 3));
        CollectionsMarshal.AsSpan(vt).CopyTo(new Span<float>(m.TexCoords, n * 2));
        CollectionsMarshal.AsSpan(vc).CopyTo(new Span<byte>(m.Colors, n * 4));
        UploadMesh(ref m, false);
        Mesh = m; vp = null; vn = null; vt = null; vc = null;
    }
    public void Dispose() { UnloadMesh(Mesh); if (HasShades) UnloadMesh(ShadeMesh); if (HasCrowd) UnloadMesh(CrowdMesh); foreach (var m in DynMesh) UnloadMesh(m); }
}
