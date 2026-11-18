// Dreamy Hellcore - Raumgenerator (Phase "nie repetitiv"): Pfad mit Mechaniken, Schienen, Raumtypen, Hoehenverlauf.
// Alles deterministisch aus der Stage-Nummer. Fairness: jeder Sprung wird gegen die Sprungphysik begrenzt (ReachV),
// Schienen tragen den Spieler automatisch, Mechaniken liegen AUF dem geprueften Pfad.
using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace DreamyHellcore;

sealed unsafe partial class Room
{
    public const float BounceV = 14f;
    public int Theme; public bool DejaVu;
    public float ExitY, KillY, YLo, YHi, Grav = Gravity, Tilt; public Vector3 Wind;
    /// <summary>Bewegliche / verschwindende / broeckelnde Plattformen (nicht in Col, werden pro Bild platziert).</summary>
    public List<Box> Dyn = new(); public List<Mesh> DynMesh = new(); public float[] DynT = Array.Empty<float>();
    readonly List<(Vector3 C, float E)> dynLook = new();
    public sealed class RailT { public Vector3[] P; public float[] S; public Box Start; public Vector3 End; }
    public List<RailT> Rails = new();

    // ---- Escape-Raeume: jede vierte Hellstage (3, 7, 11, ...) ----
    public int Escape, Style; public bool Heaven, Open, GateSoft; public float Terror, GateZ;
    public List<Vector3> Seals = new(), Plates = new(); public bool[] SealTaken = Array.Empty<bool>();
    (Vector3 A, Vector3 B) gateVis;
    public static readonly string[] EscapeNames = { "-", "Siegel", "Reihenfolge", "Nicht hinsehen", "Stillstehen", "Flucht", "-", "-", "-", "-", "-",
        "Lichter aus", "Summe", "Zaehlschloss", "Jeder Stein einmal", "Der Falsche", "Tonleiter", "Spiegelbild", "Zaehler", "Gekoppelte Raeder", "Geheimcode" };
    public static readonly string[] StyleNames = { "Falscher Himmel", "Unbehagen", "Grauen", "Terror", "Leere" };
    static int EscapeKind(long n)
    {
        long k = (n - 3) / 4; int[] first = { 11, 16, 15, 12, 17, 14, 13, 18, 20, 19 };   // die ersten zehn Escape-Raeume zeigen jedes Raetsel einmal
        return k < 10 ? first[k] : 11 + (int)(new Rng((ulong)n * 977UL + 1).F() * 9.999f);
    }
    /// <summary>Art des Escape-Raums (0 = keiner). Nie zweimal hintereinander dieselbe Art.</summary>
    public static int EscapeOf(long n)
    {
        if (n >= 9 && n % 8 == 1) return 3 + (int)((n / 8) % 3);   // Pruefungen (keine Raetsel): Nicht hinsehen, Stillstehen, Flucht
        if (n < 3 || n % 4 != 3) return 0;                      // klassische Escape-Raeume: 3, 7, 11, 15, ...
        int e = EscapeKind(n);
        return n >= 47 && e == EscapeKind(n - 4) ? 11 + (e - 10) % 10 : e;
    }
    /// <summary>Design-Stufe des Escape-Raums: meist die aktuelle, oft aber wieder der falsche Himmel - auch tief in der Hoelle.</summary>
    public static int StyleOf(long n, float terror)
    {
        if (n == 3) return 0;
        int tier = terror < 0.3f ? 0 : terror < 0.5f ? 1 : terror < 0.7f ? 2 : terror < 0.9f ? 3 : 4;
        var r = new Rng((ulong)n * 131UL + 7); float a = r.F();
        return a < 0.45f ? tier : a < 0.72f ? 0 : Math.Min(tier, (int)(r.F() * (tier + 1)));
    }

    public static readonly string[] ThemeNames =
    {
        "Halle", "Saeulenwald", "Liminaler Flur", "Treppenhaus", "Abstieg", "Balkenbruecke", "Trittsteine", "Symmetrie",
        "Leere", "Kriechgang", "Sturmraum", "Schweberaum", "Schiefer Raum", "Enge", "Mechanik-Raum", "Achterbahn-Raum"
    };
    static readonly int[] themeMin = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 14, 9 };

    static int PickTheme(long n)
    {
        if (n <= 1) return 0;
        var r = new Rng((ulong)n * 7919UL + 3);
        if (r.F() < MathF.Max(0.12f, 0.55f - n * 0.03f)) return 0;
        Span<int> c = stackalloc int[16]; int cnt = 0;
        for (int i = 1; i < themeMin.Length; i++) if (themeMin[i] <= n) c[cnt++] = i;
        return cnt == 0 ? 0 : c[Math.Min(cnt - 1, (int)(r.F() * cnt))];
    }
    /// <summary>Raumtyp; nie zweimal hintereinander derselbe Sondertyp.</summary>
    public static int ThemeOf(long n) { int t = PickTheme(n); return t != 0 && n > 1 && t == PickTheme(n - 1) ? 0 : t; }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (3 * p1 - p0 - 3 * p2 + p3) * t3);
    }

    static float ReachV(float dy, float speed, float v0)
    {
        float disc = v0 * v0 - 2 * Gravity * dy;
        return speed * (v0 + MathF.Sqrt(MathF.Max(0, disc))) / Gravity;
    }

    void Generate()
    {
        long seedN = N;
        DejaVu = N > 12 && new Rng((ulong)N * 31337UL + 5).F() < 0.06f && Escape == 0;
        if (DejaVu) seedN = N - 1;                       // Deja-vu: derselbe Raum noch einmal - fast
        Theme = Escape > 0 ? 0 : ThemeOf(seedN);
        var r = new Rng((ulong)seedN * 2654435761UL + 17);
        float T = W, z0 = N == 1 ? -4.2f : 0f;
        float pathHalf = MathF.Min(W / 2 - 3, 2 + W * 0.15f);

        // ---- Raumtyp-Parameter ----
        float gapMul = 1, yTrend = 0, beamP = 0.22f + 0.2f * A1, dynMul = 1, reliefMul = 1, decorMul = 1;
        float railP = N >= 9 ? 0.13f + 0.08f * A1 : 0, dropP = N >= 8 ? 0.05f + 0.10f * A1 : 0;
        int sizeMode = 0;
        switch (Theme)
        {
            case 2: decorMul = 0; break;
            case 3: yTrend = 0.55f; break;
            case 4: yTrend = -1.3f; dropP += 0.22f; break;
            case 5: sizeMode = 1; gapMul = 0.7f; beamP = 0; break;
            case 6: sizeMode = 2; gapMul = 0.62f; beamP = 0; break;
            case 8: reliefMul = 0; decorMul = 0; break;
            case 9: beamP = 0.85f; break;
            case 10: Wind = new Vector3((r.F() < 0.5f ? -1 : 1) * (1.2f + 0.8f * A1), 0, 0); break;
            case 11: Grav = 12f; break;
            case 12: Tilt = (r.F() < 0.5f ? -1 : 1) * (0.10f + 0.08f * r.F()); break;
            case 13: reliefMul = 1.8f; break;
            case 14: dynMul = 2.6f; break;
            case 15: railP = 0.55f; break;
        }

        if (Escape > 0)
        {   // Escape-Raum: ruhige Halle ohne Schienen und Stuerze; bei "Stillstehen" und "Flucht" nichts, das zum Warten zwingt
            railP = 0; dropP = 0;
            if (Escape == 4 || Escape == 5) dynMul = 0;
            if (Escape == 5) beamP = 0;
        }
        var path = new List<Vector3>();
        bool pillars = A < 0.25f && Theme != 3 && Theme != 4;
        float yLo = 0, yHi = 0;

        // ---- Plattform setzen (statisch oder dynamisch) ----
        void Place(Vector3 mn, Vector3 mx, bool cp, byte kind, Vector3 amp, float rate, float phase, bool rail, bool fake)
        {
            var b = new Box { Min = mn, Max = mx, Checkpoint = cp, Kind = kind, Amp = amp, Rate = rate, Phase = phase, Rail = rail, Fake = fake, Dyn = -1 };
            Vector3 col = Plat * (0.8f + 0.3f * r.F()); float em = 0;
            if (cp) { col = Plat; em = 0.35f; if (Escape >= 11 && mx.X - mn.X > 15f) { col = Plat * 0.42f; em = 0.015f; } }   // Raetselkammer: dunkler Boden, damit Platten und Stifte lesbar sind
            else if (kind == 2) { col = Plat * 1.15f; em = 0.10f; }
            else if (kind == 3) col = Plat * 0.55f;
            else if (kind == 4) { col = Vector3.Lerp(Plat, Emis, 0.5f); em = 0.22f; }
            else if (kind == 5) col = Plat * 0.7f;
            if (kind >= 1 && kind <= 3) { b.Dyn = Dyn.Count; Dyn.Add(b); dynLook.Add((col, em)); }
            else { mat = 1; Vis(mn, mx, col, em); Col.Add(b); }
            PathBoxes.Add(b);
            yLo = MathF.Min(yLo, mx.Y); yHi = MathF.Max(yHi, mx.Y);
        }
        void CheckpointDeco(float cx, float top, float cz, float wd, float ln)
        {
            Lights.Add((new Vector3(cx, top + 1.7f, cz), Heaven ? Emis * 0.25f : Emis, 14f));
            mat = 4;
            for (int cxs = -1; cxs <= 1; cxs += 2)
                for (int czs = -1; czs <= 1; czs += 2)
                {
                    Vector3 pc = new(cx + cxs * (wd / 2 - 0.1f), top, cz + czs * (ln / 2 - 0.1f));
                    Vis(pc - new Vector3(0.07f, 0, 0.07f), pc + new Vector3(0.07f, 2.6f, 0.07f), Emis, 1f);
                }
        }

        // ---- Startplattform (Checkpoint) mit leuchtendem Tor ----
        Vector3 smn = new(-4, pillars ? -400f : -0.8f, -4), smx = new(4, 0, 6);
        mat = 1; Vis(smn, smx, Plat, 0.10f);
        var sb = new Box { Min = smn, Max = smx, Checkpoint = true, Dyn = -1 }; Col.Add(sb); PathBoxes.Add(sb);
        mat = 4;
        Lights.Add((new Vector3(0, 2.4f, 0.4f), Emis, 12f));
        Vis(new Vector3(-3.9f, 0, -0.1f), new Vector3(-3.7f, 3.4f, 0.1f), Emis, 1f);
        Vis(new Vector3(3.7f, 0, -0.1f), new Vector3(3.9f, 3.4f, 0.1f), Emis, 1f);
        Vis(new Vector3(-3.9f, 3.4f, -0.1f), new Vector3(3.9f, 3.6f, 0.1f), Emis, 1f);
        path.Add(new Vector3(0, 0, 1));

        // ---- Pfad ----
        float x = 0, y = 0, z = 6, pw = 8, endZ = L - 4;
        float endP = Escape >= 11 ? endZ - 18 : Escape > 0 ? endZ - 12 : endZ;   // Escape-Raum: Platz fuer den Altar vor dem Tor
        var mains = new List<(float X, float Y, float N, float F, float W, byte K)>();
        float diff = M.C01(0.1f + 0.9f * A1);
        int cnt = 0, sinceCp = 0, rails = 0; bool prevBounce = false;
        while (endP - z > 4f)
        {
            float rem = endP - z; cnt++; sinceCp++;
            bool cp = sinceCp >= 8;
            float ln, wd;
            if (cp) { ln = 5; wd = 5; }
            else if (sizeMode == 1) { ln = 7 + 5 * r.F(); wd = 1.0f + 0.5f * r.F(); }
            else if (sizeMode == 2) { ln = 1.3f + 0.7f * r.F(); wd = 1.3f + 0.7f * r.F(); }
            else { ln = M.Lerp(M.Lerp(3, 7, r.F()), M.Lerp(1.2f, 9, M.Sq(r.F())), A1); wd = M.Lerp(M.Lerp(3, 5, r.F()), M.Lerp(0.9f, 5, M.Sq(r.F())), A1); }
            float remC = MathF.Max(2, rem - 6 - ln);
            // Hoehe laeuft frei (kein Zwang zurueck auf 0): der Raum endet auf ExitY -> Abstiege, Treppen, weite Stuerze
            float dy = Math.Clamp((r.F() - 0.5f) * (0.8f + 3f * A1) + yTrend * (0.4f + r.F()), -2.5f - 4 * A1, 1.1f);
            float v0 = JumpV;
            if (prevBounce) { dy = 1.6f + 1.6f * r.F(); v0 = BounceV; }
            bool drop = !cp && !prevBounce && cnt > 2 && rem > 30 && r.F() < dropP;
            if (drop) { dy = -(6f + r.F() * (10f + 30f * A1)); ln = 6; wd = 6; }
            float yn = y + dy;
            float allowed = M.Lerp(ReachV(dy, WalkSpeed, v0) * 0.62f, ReachV(dy, SprintSpeed, v0) * 0.70f, diff);
            if (prevBounce) allowed = MathF.Min(allowed, 5f);
            if (drop) allowed = 3.2f;
            allowed = MathF.Min(allowed, 7.5f);
            float g = MathF.Max(0.9f, M.Lerp(1.2f, allowed, MathF.Pow(r.F(), 1 - 0.5f * diff)) * (cp ? 1 : gapMul));
            float xl = MathF.Min(pathHalf, MathF.Max(1.5f, 0.3f * remC));
            float half = (pw + wd) / 2, dxMax = half - 0.4f + A1 * 2f * r.F();
            float xn = Math.Clamp(x + (r.F() * 2 - 1) * dxMax, -xl, xl);
            float gx = MathF.Max(0, MathF.Abs(xn - x) - half);
            if (gx > 0.8f * g) { gx = 0.8f * g; xn = x + MathF.Sign(xn - x) * (half + gx); }
            float gz = MathF.Sqrt(MathF.Max(0.09f, g * g - gx * gx));
            bool last = rem - gz - ln < 5.5f;
            if (last) { gz = MathF.Min(gz, rem - 3.2f); ln = rem - gz - 2f; }
            float near = z + gz, far = near + ln;

            // Mechanik dieser Plattform (wird schrittweise freigeschaltet, siehe README)
            byte kind = 0; Vector3 amp = default; float rate = 0, phase = r.F() * 6.28f, kr = r.F(), k2 = r.F(), k3 = r.F();
            bool plain = !cp && !last && cnt > 1 && !drop && !prevBounce;
            bool rail = plain && rails < 3 && cnt > 2 && rem - gz - ln > 50 && k3 < railP;
            if (rail) { ln = MathF.Max(ln, 4f); wd = MathF.Max(wd, 3f); far = near + ln; }
            else if (plain)
            {
                float pMove = N >= 4 ? (0.10f + 0.12f * A1) * dynMul : 0, pVan = N >= 7 ? (0.07f + 0.10f * A1) * dynMul : 0;
                float pCr = N >= 10 ? (0.07f + 0.12f * A1) * dynMul : 0, pBo = N >= 6 ? 0.08f * dynMul : 0, pCo = N >= 12 ? 0.07f * dynMul : 0;
                if (kr < pMove && wd >= 2.4f) { kind = 1; amp = new Vector3(MathF.Min(1.3f, wd / 2 - 0.7f), 0, 0); rate = 0.7f + 0.8f * k2; }
                else if (kr < pMove + pVan && ln >= 2.5f) { kind = 2; rate = 3.2f + 1.5f * k2; }
                else if (kr < pMove + pVan + pCr) kind = 3;
                else if (kr < pMove + pVan + pCr + pBo && ln >= 2.5f && wd >= 2.5f && rem > 25) kind = 4;
                else if (kr < pMove + pVan + pCr + pBo + pCo && ln >= 5f) { kind = 5; amp = k2 < 0.6f ? new Vector3(0, 0, 2.6f) : new Vector3((k2 < 0.8f ? -1 : 1) * 1.4f, 0, 0); }
            }
            float th = pillars ? 400f : M.Lerp(0.5f, 0.4f + 6 * r.F() * r.F() * r.F(), A1);
            if (kind >= 1 && kind <= 3) th = MathF.Min(th, 0.6f);
            bool fake = cp && Terror > 0.4f && r.F() < 0.25f;      // "falscher" Checkpoint: funktioniert, aber sein Licht stirbt
            Vector3 mn = new(xn - wd / 2, yn - th, near), mx = new(xn + wd / 2, yn, far);
            Place(mn, mx, cp, kind, amp, rate, phase, rail, fake);
            if (cp) { CheckpointDeco(xn, yn, (near + far) / 2, wd, ln); sinceCp = 0; }
            else if (kind == 0 && !rail && !drop && ln >= 5f && cnt > 1 && r.F() < beamP)
            {   // Duck-Hindernis: Balken 1,2 m ueber der Plattform
                float zc = (near + far) / 2;
                Vector3 bmn = new(xn - wd / 2 - 1.5f, yn + 1.2f, zc - 0.8f), bmx = new(xn + wd / 2 + 1.5f, yn + 2.8f + 3 * r.F(), zc + 0.8f);
                mat = 0; Vis(bmn, bmx, Wall * 1.3f); Solid(bmn, bmx);
            }
            path.Add(new Vector3(xn, yn, (near + far) / 2));
            mains.Add((xn, yn, near, far, wd, kind));
            x = xn; y = yn; z = far; pw = wd; prevBounce = kind == 4;

            if (rail)
            {   // ---- Achterbahn: Schiene von dieser Plattform zu einer grossen Landeplattform (immer ein Checkpoint) ----
                rails++;
                float lr = MathF.Min(endZ - far - 15f, 28f + r.F() * (40f + 90f * A1));
                float bx = Math.Clamp(xn + (r.F() - 0.5f) * 2 * pathHalf, -pathHalf, pathHalf), by = yn + 3f - r.F() * (9f + 30f * A1);
                var cps = new List<Vector3> { new(xn, yn + 0.9f, far - 0.7f) };
                int nc = 3 + (int)(r.F() * 3);
                for (int i = 1; i <= nc; i++)
                {
                    float t = i / (nc + 1f);
                    cps.Add(new Vector3(Math.Clamp(M.Lerp(xn, bx, t) + (r.F() - 0.5f) * 1.6f * pathHalf, -pathHalf - 2, pathHalf + 2),
                        M.Lerp(yn, by, t) + 1.2f - r.F() * (4f + 16f * A1) + (i % 2 == 0 ? 3f : 0f), far + lr * t));
                }
                cps.Add(new Vector3(bx, by + 1.0f, far + lr + 1.5f));
                var pts = new List<Vector3>();
                for (int i = 0; i < cps.Count - 1; i++)
                {
                    Vector3 p0 = cps[Math.Max(i - 1, 0)], p1 = cps[i], p2 = cps[i + 1], p3 = cps[Math.Min(i + 2, cps.Count - 1)];
                    for (int k = 0; k < 14; k++) pts.Add(CatmullRom(p0, p1, p2, p3, k / 14f));
                }
                pts.Add(cps[^1]);
                var rt = new RailT { P = pts.ToArray(), S = new float[pts.Count], Start = PathBoxes[^1], End = new Vector3(bx, by + 0.05f, far + lr + 1.6f) };
                for (int i = 1; i < pts.Count; i++)
                {
                    Vector3 a = pts[i - 1], b = pts[i], d = b - a; float len = d.Length(); rt.S[i] = rt.S[i - 1] + len;
                    if (len < 0.01f) continue;
                    d /= len;
                    var q = Quaternion.CreateFromYawPitchRoll(MathF.Atan2(d.X, d.Z), -MathF.Asin(Math.Clamp(d.Y, -1f, 1f)), 0);
                    Vector3 lat = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, d)), mid = (a + b) / 2 - new Vector3(0, 0.25f, 0);
                    mat = 4;   // die Schiene leuchtet schwach: eine Linie aus Licht durch die Dunkelheit
                    AddBox(mid + lat * 0.45f, new Vector3(0.05f, 0.05f, len / 2 + 0.03f), q, Emis * 0.22f);
                    AddBox(mid - lat * 0.45f, new Vector3(0.05f, 0.05f, len / 2 + 0.03f), q, Emis * 0.22f);
                    if (i % 2 == 0) { mat = 0; AddBox(mid - new Vector3(0, 0.08f, 0), new Vector3(0.62f, 0.04f, 0.09f), q, Wall * 0.9f); }
                    if (i % 4 == 0) path.Add(b);
                    yLo = MathF.Min(yLo, b.Y - 1.5f); yHi = MathF.Max(yHi, b.Y);
                }
                Rails.Add(rt);
                Vector3 lmn = new(bx - 3, by - (pillars ? 400f : 0.8f), far + lr), lmx = new(bx + 3, by, far + lr + 7);
                Place(lmn, lmx, true, 0, default, 0, 0, false, false);
                CheckpointDeco(bx, by, far + lr + 3.5f, 6, 7); sinceCp = 0;
                path.Add(new Vector3(bx, by, far + lr + 3.5f));
                x = bx; y = by; z = far + lr + 7; pw = 6;
            }
        }
        float gateZ = 0;
        if (Escape > 0)
        {   // ---- Altar: grosse Checkpoint-Plattform vor dem verschlossenen Tor ----
            float an = endP, af = endZ - 2;
            float hw = Escape >= 11 ? 8f : 5f; AltarZ = an;   // Raetselkammer: 16 x 16 m
            Place(new Vector3(-hw, y - (pillars ? 400f : 1f), an), new Vector3(hw, y, af), true, 0, default, 0, 0, false, false);
            CheckpointDeco(0, y, (an + af) / 2, 2 * hw, af - an);
            path.Add(new Vector3(0, y, (an + af) / 2));
            gateZ = af - 0.6f;
            mat = 4;
            Vis(new Vector3(-3.5f, y, gateZ - 0.15f), new Vector3(-3.1f, y + 4.6f, gateZ + 0.15f), Emis, 1f);
            Vis(new Vector3(3.1f, y, gateZ - 0.15f), new Vector3(3.5f, y + 4.6f, gateZ + 0.15f), Emis, 1f);
            Vis(new Vector3(-3.5f, y + 4.6f, gateZ - 0.15f), new Vector3(3.5f, y + 5.0f, gateZ + 0.15f), Emis, 1f);
            Lights.Add((new Vector3(0, y + 3f, gateZ - 1f), Heaven ? Emis * 0.2f : Emis * 0.6f, 13f));
            x = 0;
            if (Escape >= 11) BuildPuzzle(r, y, an, af);
            if (Escape == 2)
            {   // Platten auf dem Altar; die Zahl der Stifte an jeder Platte verraet die Reihenfolge
                int k = 3 + Math.Min(2, (int)(N / 15));
                var cells = new List<Vector2>();
                foreach (float px in new[] { -3.2f, 0f, 3.2f }) foreach (float pz in new[] { 2.0f, 4.6f, 7.2f }) cells.Add(new Vector2(px, an + pz));
                for (int i = 0; i < k; i++)
                {
                    int ci = (int)(r.F() * cells.Count * 0.999f); Vector2 c = cells[ci]; cells.RemoveAt(ci);
                    Plates.Add(new Vector3(c.X, y, c.Y));
                    mat = 1; Vis(new Vector3(c.X - 0.8f, y, c.Y - 0.8f), new Vector3(c.X + 0.8f, y + 0.03f, c.Y + 0.8f), Vector3.Lerp(Plat, Emis, 0.45f), 0.3f);
                    mat = 4;
                    for (int m = 0; m <= i; m++)
                        Vis(new Vector3(c.X - 0.6f + 0.28f * m, y, c.Y + 0.62f), new Vector3(c.X - 0.52f + 0.28f * m, y + 0.3f, c.Y + 0.7f), Emis, 1f);
                }
            }
            if (Escape == 1)
            {   // Siegel: abseits des Pfades, jedes auf einer eigenen kleinen Insel (gleiche Hoehe, kurzer Sprung hin und zurueck)
                int want = 3 + Math.Min(3, (int)(N / 12)), placed = 0;
                for (int tries = 0; tries < 80 && placed < want && mains.Count > 2; tries++)
                {
                    var mp = mains[1 + (int)(r.F() * (mains.Count - 1.01f))];
                    int side = r.F() < 0.5f ? -1 : 1; float gap = 1.6f + 0.9f * r.F(), zc = (mp.N + mp.F) / 2;
                    if (mp.K != 0 || mp.W < 1.6f) continue;
                    float bx = mp.X + side * (mp.W / 2 + gap + 1.1f);
                    if (MathF.Abs(bx) > W / 2 - 2.5f) continue;
                    Vector3 bmn = new(bx - 1.1f, mp.Y - 0.6f, zc - 1.1f), bmx = new(bx + 1.1f, mp.Y, zc + 1.1f);
                    bool clash = false;
                    foreach (var pb in PathBoxes)
                        if (bmn.X - 0.9f < pb.Max.X && bmx.X + 0.9f > pb.Min.X && bmn.Z - 0.9f < pb.Max.Z && bmx.Z + 0.9f > pb.Min.Z && MathF.Abs(pb.Max.Y - mp.Y) < 4f) { clash = true; break; }
                    foreach (var sp0 in Seals) if (Vector3.Distance(sp0, new Vector3(bx, mp.Y + 1.3f, zc)) < 5f) { clash = true; break; }
                    if (clash) continue;
                    mat = 1; Vis(bmn, bmx, Plat * 0.9f, 0.12f); Col.Add(new Box { Min = bmn, Max = bmx, Dyn = -1 });
                    Vector3 sp = new(bx, mp.Y + 1.3f, zc);
                    Dyn.Add(new Box { Min = sp - new Vector3(0.22f), Max = sp + new Vector3(0.22f), Kind = 7, Rate = placed, Phase = r.F() * 6f, Dyn = Dyn.Count });
                    dynLook.Add((Emis, 1f)); Seals.Add(sp); Lights.Add((sp, Emis, 7f)); placed++;
                }
                SealTaken = new bool[Seals.Count];
            }
        }
        ExitY = y; YLo = yLo; YHi = yHi; KillY = yLo - D + 0.6f;
        DynT = new float[Dyn.Count]; Array.Fill(DynT, -1f);

        // ---- Huelle: Seitenwaende, Decke, Abgrundboden. Keine Stirnwand. ----
        float fl = yLo - D, ce = yHi + H;
        mat = 5;
        Vis(new Vector3(-W / 2 - T, fl - T, z0), new Vector3(-W / 2, ce + T, L), Wall);
        Vis(new Vector3(W / 2, fl - T, z0), new Vector3(W / 2 + T, ce + T, L), Wall);
        Vis(new Vector3(-W / 2, ce, z0), new Vector3(W / 2, ce + T, L), Wall * 0.7f);
        mat = 2;
        Vis(new Vector3(-W / 2, fl - T, z0), new Vector3(W / 2, fl, L), Abyss, 0.6f);
        mat = 5;
        Solid(new Vector3(-W / 2 - T, fl - T, z0), new Vector3(-W / 2, ce + T, L));
        Solid(new Vector3(W / 2, fl - T, z0), new Vector3(W / 2 + T, ce + T, L));
        Solid(new Vector3(-W / 2, ce, z0), new Vector3(W / 2, ce + T, L));
        if (N == 1)
        {   // nur der allererste Raum hat eine Rueckwand
            Vis(new Vector3(-W / 2 - T, fl - T, z0 - T), new Vector3(W / 2 + T, ce + T, z0), Wall * 0.9f);
            Solid(new Vector3(-W / 2 - T, fl - T, z0 - T), new Vector3(W / 2 + T, ce + T, z0));
        }

        if (Escape > 0)
        {   // das verschlossene Tor: sperrt den ganzen Querschnitt, sichtbar ist nur die leuchtende Flaeche im Bogen
            GateZ = gateZ;
            Dyn.Add(new Box { Min = new Vector3(-W / 2, fl, gateZ - 0.15f), Max = new Vector3(W / 2, ce, gateZ + 0.15f), Kind = 6, Dyn = Dyn.Count });
            dynLook.Add((Emis * 0.16f, 1f));
            gateVis = (new Vector3(-3.1f, ExitY, gateZ - 0.06f), new Vector3(3.1f, ExitY + 4.6f, gateZ + 0.06f));
            DynT = new float[Dyn.Count]; Array.Fill(DynT, -1f);
        }

        // ---- Wandrelief ----
        float maxProt = Theme == 13 ? W / 2 - pathHalf - 0.6f : W / 2 - pathHalf - 2;
        float cs = MathF.Max(2.5f, W / 7); Cell = cs;
        while ((L / cs) * ((ce - fl) / cs) * 2 > 2600) cs *= 1.25f;
        int nz = (int)(L / cs), ny = (int)((ce - fl) / cs) + 1;
        bool regular = A < 0.2f || Theme == 2;
        mat = 0;
        for (int side = -1; side <= 1; side += 2)
            for (int iz = 0; iz < nz; iz++)
                for (int iy = 0; iy < ny; iy++)
                {
                    float dp, roll = r.F();
                    if (regular && iz % 2 == 0) dp = 0.45f;
                    else if (roll < (regular ? 0.06f : 0.25f + 0.45f * A1) * reliefMul) dp = cs * (0.1f + r.F() * (0.3f + 2.4f * A1 * A1)) * (Theme == 13 ? 1.8f : 1f);
                    else continue;
                    if (reliefMul <= 0) continue;
                    dp = MathF.Min(dp, maxProt);
                    float hh = cs * (1 - 0.8f * A1 * r.F()), ww = cs * (1 - 0.7f * A1 * r.F());
                    var q = A1 > 0.35f && !regular
                        ? Quaternion.CreateFromYawPitchRoll((r.F() - 0.5f) * A1 * 0.8f, (r.F() - 0.5f) * A1 * 0.8f, (r.F() - 0.5f) * A1 * 0.8f)
                        : Quaternion.Identity;
                    AddBox(new Vector3(side * (W / 2 - dp / 2 + 0.5f), fl + (iy + 0.5f) * cs, (iz + 0.5f) * cs),
                        new Vector3(dp / 2 + 0.5f, hh / 2, ww / 2), q, Wall * (0.75f + 0.5f * r.F()), 0f, dp * 0.03f);
                }

        bool NearPath(Vector3 c, float s)
        {
            foreach (var p in path)
                if (MathF.Abs(c.Z - p.Z) < s + 7 && MathF.Abs(c.X - p.X) < s + 6 && MathF.Abs(c.Y - p.Y) < s + 6) return true;
            return false;
        }
        bool NearPathXZ(float cx, float cz, float s)
        {
            foreach (var p in path) if (MathF.Abs(cz - p.Z) < s + 5 && MathF.Abs(cx - p.X) < s + 4.5f) return true;
            return false;
        }

        // ---- Dekor (kollisionsfrei, vom Pfad ferngehalten) ----
        mat = 0;
        bool sym = Theme == 7;
        int nMono = (int)((4 + 40 * A1 + L / 30) * decorMul), skip = sym ? (int)(r.F() * nMono) : -1;
        for (int i = 0; i < nMono; i++)
        {
            float s = M.Lerp(1.0f, W * 0.12f, M.Sq(r.F())) * (0.5f + A1);
            float cx = (r.F() - 0.5f) * (W - s), cz = r.F() * L, ln = r.F() * H * (0.1f + 0.4f * A1);
            Vis(new Vector3(cx - s / 2, ce - ln, cz - s / 2), new Vector3(cx + s / 2, ce + 1, cz + s / 2), Wall * 0.55f);
            // Symmetrie-Raum: alles gespiegelt - bis auf ein einziges Teil, das fehlt
            if (sym && i != skip) Vis(new Vector3(-cx - s / 2, ce - ln, cz - s / 2), new Vector3(-cx + s / 2, ce + 1, cz + s / 2), Wall * 0.55f);
        }
        int nFloat = (int)(70 * MathF.Max(0, A - 0.3f) * (L / 200f + 0.5f) * decorMul);
        for (int i = 0; i < nFloat; i++)
        {
            float s = M.Lerp(0.6f, W * 0.09f, M.Sq(r.F()) * r.F());
            Vector3 c = new((r.F() - 0.5f) * (W - s), M.Lerp(fl + D * 0.4f, ce - H * 0.1f, r.F()), r.F() * L);
            var q = Quaternion.CreateFromYawPitchRoll(r.F() * 6.28f, r.F() * 6.28f, r.F() * 6.28f);
            float e2 = r.F(), e3 = r.F();
            if (NearPath(c, s)) continue;
            AddBox(c, new Vector3(s / 2, s / 2 * (0.3f + 1.4f * e2), s / 2 * (0.3f + 1.4f * e3)), q, Wall * 0.8f, 0f, s * 0.12f);
            if (sym && !NearPath(new Vector3(-c.X, c.Y, c.Z), s))
                AddBox(new Vector3(-c.X, c.Y, c.Z), new Vector3(s / 2, s / 2 * (0.3f + 1.4f * e2), s / 2 * (0.3f + 1.4f * e3)), q, Wall * 0.8f, 0f, s * 0.12f);
        }
        if (A > 0.6f && decorMul > 0)
        {   // riesige dunkle Ringe um die Raumachse
            int nRing = (int)(L / (70 + 110 * Weird));
            for (int k = 1; k <= nRing; k++)
            {
                float zc = k * L / (nRing + 1f), rad = MathF.Min(W, ce - fl) * 0.36f, s = rad * 0.16f, cyc = (ce + fl) / 2;
                float tilt = (r.F() - 0.5f) * 0.5f * A1;
                for (int i = 0; i < 18; i++)
                {
                    float an = i / 18f * 6.2832f + k;
                    Vector3 c = new(MathF.Cos(an) * rad, cyc + MathF.Sin(an) * rad, zc + MathF.Sin(an) * rad * tilt);
                    if (NearPath(c, s)) continue;
                    AddBox(c, new Vector3(s / 2, s / 2, s / 3), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, an), Wall * 0.6f, 0f, s * 0.05f);
                }
            }
        }
        if (Theme == 1)
        {   // Saeulenwald: duenne Saeulen vom Abgrund bis zur Decke
            for (int i = 0; i < 46; i++)
            {
                float s = (0.5f + 1.2f * r.F()) * (1 + W / 70f), cx = (r.F() - 0.5f) * (W - 2), cz = r.F() * L;
                if (NearPathXZ(cx, cz, s)) continue;
                Vis(new Vector3(cx - s / 2, fl - 1, cz - s / 2), new Vector3(cx + s / 2, ce + 1, cz + s / 2), Wall * 0.7f);
            }
        }
        if (Theme == 2)
        {   // Liminaler Flur: gleichmaessige Rahmen im festen Takt - einer davon stimmt nicht
            int nf = (int)(L / 8), wrong = 1 + (int)(r.F() * MathF.Max(1, nf - 2));
            float px = MathF.Min(W / 2 - 1.2f, pathHalf + 2.2f);
            for (int i = 1; i < nf; i++)
            {
                float zc = i * 8f, off = i == wrong ? -1.3f : 0f, ww = i == wrong ? 0.75f : 0.5f;
                Vis(new Vector3(-px - ww + off * 0.4f, fl, zc - 0.25f), new Vector3(-px + off * 0.4f, ce, zc + 0.25f), Wall * 1.15f);
                Vis(new Vector3(px + off, fl, zc - 0.25f), new Vector3(px + ww + off, ce, zc + 0.25f), Wall * 1.15f);
                Vis(new Vector3(-px - ww, ce - 1.1f - (i == wrong ? 0.7f : 0f), zc - 0.25f), new Vector3(px + ww, ce, zc + 0.25f), Wall * 1.15f);
            }
        }

        // ---- Schattengestalten ----
        int nPer = Terror > 0.4f ? (int)(1 + 7 * M.SStep(0.4f, 0.95f, Terror)) : 0;
        for (int i = 0; i < nPer && path.Count > 3; i++)
        {
            Vector3 p = path[2 + (int)(r.F() * (path.Count - 2.01f))]; int side = r.F() < 0.5f ? 1 : -1;
            shades.Add((new Vector3(p.X + side * (2.6f + 3.5f * r.F()), p.Y - 0.1f, p.Z + (r.F() - 0.5f) * 4f), 2.6f + 1.4f * r.F(), 1f));
        }
        if (DejaVu) shades.Add((new Vector3(2.2f, -0.1f, 5.2f), 3.1f, 1f));   // das Einzige, was im Deja-vu-Raum anders ist
        if (Heaven)
        {   // falscher Himmel: ein Chor aus weissen Gestalten in zu perfekten Reihen - und einer, der viel zu gross ist
            float rx = MathF.Min(pathHalf + 3.2f, W / 2 - 1.5f), rx2 = MathF.Min(pathHalf + 6.5f, W / 2 - 0.8f);
            for (float zc = 4; zc < L; zc += 4.5f)
            {
                float py = 0, best = 1e9f;
                foreach (var p in path) { float d = MathF.Abs(p.Z - zc); if (d < best) { best = d; py = p.Y; } }
                for (int side = -1; side <= 1; side += 2)
                {
                    shades.Add((new Vector3(side * rx, py - 0.1f, zc), 3.2f, 2f));
                    shades.Add((new Vector3(side * rx2, py + 1.6f, zc + 2.2f), 3.2f, 2f));
                }
            }
            shades.Add((new Vector3(0, yLo - 16f, L * 0.75f), 48f, 2f));
        }
        int nCrowd = Terror > 0.55f ? (int)(12 + 70 * M.SStep(0.55f, 1f, Terror)) : 0;
        for (int i = 0; i < nCrowd && path.Count > 3; i++)
        {
            Vector3 p = path[1 + (int)(r.F() * (path.Count - 1.01f))];
            float ang = r.F() * 6.283f, rad = 3.5f + r.F() * r.F() * MathF.Min(40f, W * 0.4f);
            float hh = 2.5f + 1.5f * r.F() + (r.F() < 0.12f ? 6f + 14f * r.F() * Terror : 0f);
            shades.Add((new Vector3(p.X + MathF.Cos(ang) * rad, p.Y - 0.1f - (hh > 5 ? hh * 0.3f : 0), p.Z + MathF.Sin(ang) * rad), hh, 2f));
        }
    }

    /// <summary>Eigenes kleines Mesh je dynamischer Plattform (wird pro Bild verschoben gezeichnet).</summary>
    void BuildDyn()
    {
        for (int i = 0; i < Dyn.Count; i++)
        {
            var s = new Room(); s.mat = 1;
            var d = Dyn[i]; if (d.Kind >= 6) s.mat = 4;   // Tor und Siegel: reines Leuchten
            if (d.Kind == 6) s.Vis(gateVis.A, gateVis.B, dynLook[i].C, 1f); else s.Vis(d.Min, d.Max, dynLook[i].C, dynLook[i].E);
            s.Upload(); DynMesh.Add(s.Mesh);
        }
    }
}
