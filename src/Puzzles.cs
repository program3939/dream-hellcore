// Dreamy Hellcore - klassische Escape-Raeume: eine Raetselkammer (16 x 16 m) vor dem verschlossenen Tor.
// Zehn Logikraetsel, die man mit dem Kopf loest - ohne Text, nur mit Platten, Stiften, Lichtern und Toenen.
// Dieser Teil baut die Kammer (Geometrie + Loesungsdaten in PD), die Spiellogik steht in Game4.cs.
//   11 Lichter aus        12 Summe             13 Zaehlschloss      14 Jeder Stein einmal   15 Der Falsche
//   16 Tonleiter          17 Spiegelbild       18 Zaehler           19 Gekoppelte Raeder    20 Geheimcode
using System;
using System.Collections.Generic;
using System.Numerics;

namespace DreamyHellcore;

sealed unsafe partial class Room
{
    public float AltarZ;
    /// <summary>Anzeigen (leuchtende Elemente, die das Spiel an- und ausschaltet); IndInit = Ausgangszustand.</summary>
    public bool[] IndOn = Array.Empty<bool>(), IndInit = Array.Empty<bool>();
    /// <summary>Raetseldaten (Loesung, Groessen, Anzeige-IDs) - Belegung je Art siehe BuildPuzzle.</summary>
    public int[] PD = new int[128];
    public List<int> PlateLit = new();      // Platte -> Anzeige-ID ihrer Leuchtflaeche (-1 = keine)
    readonly List<bool> indInit = new();

    int AddInd(Vector3 mn, Vector3 mx, Vector3 col, bool on = false)
    {
        int id = indInit.Count;
        Dyn.Add(new Box { Min = mn, Max = mx, Kind = 9, Rate = id, Dyn = Dyn.Count }); dynLook.Add((col, 1f)); indInit.Add(on);
        return id;
    }
    int AddPlate(Vector3 c, bool overlay, bool on = false, float half = 0.8f)
    {
        mat = 1; Vis(c - new Vector3(half, 0, half), c + new Vector3(half, 0.03f, half), Plat * 0.14f, 0f);
        Plates.Add(c);
        PlateLit.Add(overlay ? AddInd(c + new Vector3(-half, 0.03f, -half), c + new Vector3(half, 0.07f, half), Emis * 0.2f, on) : -1);
        return Plates.Count - 1;
    }
    void Post(Vector3 p, float w, float h, Vector3 col, int m = 4) { mat = m; Vis(p - new Vector3(w / 2, 0, w / 2), p + new Vector3(w / 2, h, w / 2), col, m == 4 ? 1f : 0f); }
    /// <summary>n kleine Stifte in einer Reihe: die "Zahl" einer Platte.</summary>
    void Pips(Vector3 c, int n, float zoff, Vector3 col)
    {
        for (int m = 0; m < n; m++) Post(new Vector3(c.X - 0.09f * (n - 1) + 0.18f * m, c.Y, c.Z + zoff), 0.08f, 0.26f, col);
    }
    static void Shuffle(int[] a, Rng r) { for (int i = a.Length - 1; i > 0; i--) { int j = (int)(r.F() * (i + 0.999f)); (a[i], a[j]) = (a[j], a[i]); } }

    static bool HamFrom(int R, int C, bool[] used, int cell, int left)
    {
        if (left == 0) return true;
        int rr = cell / C, cc = cell % C;
        for (int k = 0; k < 4; k++)
        {
            int r2 = rr + (k == 0 ? 1 : k == 1 ? -1 : 0), c2 = cc + (k == 2 ? 1 : k == 3 ? -1 : 0);
            if (r2 < 0 || c2 < 0 || r2 >= R || c2 >= C) continue;
            int n = r2 * C + c2; if (used[n]) continue;
            used[n] = true; bool ok = HamFrom(R, C, used, n, left - 1); used[n] = false;
            if (ok) return true;
        }
        return false;
    }
    static bool HasHamPath(int R, int C, bool[] hole)
    {
        int free = 0; foreach (bool h in hole) if (!h) free++;
        for (int s = 0; s < R * C; s++)
        {
            if (hole[s]) continue;
            var used = (bool[])hole.Clone(); used[s] = true;
            if (HamFrom(R, C, used, s, free - 1)) return true;
        }
        return false;
    }

    void BuildPuzzle(Rng r, float y, float an, float af)
    {
        float zc = an + 7.5f;
        Vector3 gold = Emis, dim = Plat * 0.2f;
        switch (Escape)
        {
            case 11:
            {   // ---- LICHTER AUS: eine Platte schaltet sich selbst und ihre vier Nachbarn um. Ziel: alle leuchten. ----
                int g = N >= 43 ? 4 : 3; PD[0] = g; var st = new bool[g * g]; Array.Fill(st, true);
                int presses = 3 + (int)(r.F() * 4);
                for (int k = 0; k < presses || AllTrue(st); k++)
                {
                    int cell = (int)(r.F() * g * g * 0.999f), rr = cell / g, cc = cell % g;
                    st[cell] = !st[cell];
                    if (rr > 0) st[cell - g] = !st[cell - g]; if (rr < g - 1) st[cell + g] = !st[cell + g];
                    if (cc > 0) st[cell - 1] = !st[cell - 1]; if (cc < g - 1) st[cell + 1] = !st[cell + 1];
                }
                for (int i = 0; i < g * g; i++)
                    AddPlate(new Vector3((i % g - (g - 1) / 2f) * 2.4f, y, zc + (i / g - (g - 1) / 2f) * 2.4f), true, st[i]);
                break;
            }
            case 12:
            {   // ---- SUMME: jede Platte hat einen Wert (Stifte). Die leuchtenden Werte muessen genau die Zahl am Tor ergeben. ----
                int n = N >= 30 ? 6 : 5; PD[0] = n; int target = 0, picked = 0;
                var idx = new int[n]; for (int i = 0; i < n; i++) idx[i] = i; Shuffle(idx, r);
                int take = 2 + (int)(r.F() * 1.999f);
                for (int i = 0; i < n; i++)
                {
                    int v = 1 + (int)(r.F() * 7.999f); PD[2 + i] = v;
                    Vector3 c = new((i - (n - 1) / 2f) * 2.6f, y, zc);
                    AddPlate(c, true); Pips(c, v, 1.05f, gold);
                }
                for (int i = 0; i < n && picked < take; i++) { target += PD[2 + idx[i]]; picked++; }
                PD[1] = target;
                for (int k = 0; k < target; k++) Post(new Vector3(-0.3f * (target - 1) / 2f + 0.3f * k, y, af - 2.2f), 0.13f, 0.55f, gold);
                break;
            }
            case 13:
            {   // ---- ZAEHLSCHLOSS: im Raum stehen drei Arten von Figuren. Jedes Zaehlwerk muss die Anzahl seiner Art zeigen. ----
                var slots = new List<Vector3>();
                for (int k = 0; k < 6; k++) { slots.Add(new Vector3(-7.0f, y, an + 1.6f + k * 2.2f)); slots.Add(new Vector3(7.0f, y, an + 1.6f + k * 2.2f)); }
                for (int k = 0; k < 4; k++) slots.Add(new Vector3(-4.2f + k * 2.8f, y, an + 0.9f));
                int total = 0;
                for (int d = 0; d < 3; d++)
                {
                    int cnt = 1 + (int)(r.F() * 4.999f); PD[d] = cnt; total += cnt;
                    Vector3 c = new(-3.4f + d * 3.4f, y, zc + 1.2f);
                    AddPlate(c, false);
                    Totem(new Vector3(c.X, y, c.Z - 1.5f), d, 0.38f);                       // kleines Muster: wofuer dieses Zaehlwerk steht
                    for (int k = 0; k < 6; k++)
                    {
                        Vector3 p = new(c.X - 0.5f + 0.2f * k, y, c.Z + 1.1f);
                        Post(p, 0.08f, 0.3f, dim, 0);
                        PD[4 + d * 8 + k] = AddInd(p + new Vector3(-0.05f, 0, -0.05f), p + new Vector3(0.05f, 0.42f, 0.05f), gold);
                    }
                    for (int k = 0; k < cnt && slots.Count > 0; k++)
                    {
                        int si = (int)(r.F() * slots.Count * 0.999f); Totem(slots[si], d, 1f); slots.RemoveAt(si);
                    }
                }
                break;
            }
            case 14:
            {   // ---- JEDER STEIN EINMAL: alle Platten genau einmal betreten, immer nur auf eine direkt benachbarte. ----
                int R = N < 20 ? 3 : N < 45 ? 3 : 4, C = N < 20 ? 3 : 4, holes = N < 20 ? 0 : N < 45 ? 1 : 2;
                var hole = new bool[R * C];
                for (int t = 0; t < 40; t++)
                {
                    Array.Clear(hole);
                    for (int h = 0; h < holes; h++) hole[(int)(r.F() * R * C * 0.999f)] = true;
                    if (HasHamPath(R, C, hole)) break;
                    if (t == 39) Array.Clear(hole);
                }
                PD[0] = R; PD[1] = C;
                for (int i = 0; i < R * C; i++)
                {
                    PD[2 + i] = -1; if (hole[i]) continue;
                    int p = AddPlate(new Vector3((i % C - (C - 1) / 2f) * 2.3f, y, zc + (i / C - (R - 1) / 2f) * 2.3f), true, true);
                    PD[2 + i] = p; PD[40 + p] = i;
                }
                break;
            }
            case 15:
            {   // ---- DER FALSCHE: alle Saeulen haben einen Zwilling - bis auf eine. Vor diese muss man treten. ----
                bool hard = N >= 25; int m = hard ? 7 : 6; PD[0] = m;
                var kinds = new int[m];                       // Variante = Ringe (2..4) * 2 + Kappe (0/1)
                if (hard) { int[] v = { 4, 5, 6, 7, 8, 9 }; Shuffle(v, r); for (int i = 0; i < 6; i++) kinds[i] = v[i / 2]; kinds[6] = v[3]; }
                else { int baseK = 3 * 2 + 1; for (int i = 0; i < m; i++) kinds[i] = baseK; kinds[m - 1] = new[] { 2 * 2 + 1, 4 * 2 + 1, 3 * 2 }[(int)(r.F() * 2.999f)]; }
                var order = new int[m]; for (int i = 0; i < m; i++) order[i] = i; Shuffle(order, r);
                for (int s = 0; s < m; s++)
                {
                    int id = order[s]; if (id == m - 1) PD[1] = s;
                    float px = (s - (m - 1) / 2f) * 2.05f; int rings = kinds[id] / 2; bool cap = kinds[id] % 2 == 1;
                    AddPlate(new Vector3(px, y, zc + 1.2f), false);
                    Post(new Vector3(px, y, zc + 3.2f), 0.45f, 2.6f, Wall * 1.2f, 0);
                    for (int k = 0; k < rings; k++) { mat = 0; Vis(new Vector3(px - 0.42f, y + 0.6f + 0.5f * k, zc + 2.78f), new Vector3(px + 0.42f, y + 0.74f + 0.5f * k, zc + 3.62f), Wall * 1.5f); }
                    if (cap) Post(new Vector3(px, y + 2.6f, zc + 3.2f), 0.7f, 0.22f, gold);
                }
                break;
            }
            case 16:
            {   // ---- TONLEITER: jede Platte klingt. In der Reihenfolge vom tiefsten zum hoechsten Ton betreten. ----
                int n = 4 + Math.Min(2, (int)(N / 20)); PD[0] = n; PD[1] = N < 25 ? 3 : N < 50 ? 2 : 1; PD[10] = 300 + (int)(r.F() * 180);
                var rank = new int[n]; for (int i = 0; i < n; i++) rank[i] = i; Shuffle(rank, r);
                for (int i = 0; i < n; i++) { PD[2 + i] = rank[i]; AddPlate(new Vector3((i - (n - 1) / 2f) * 2.6f, y, zc), true); }
                break;
            }
            case 17:
            {   // ---- SPIEGELBILD: links ein festes Lichtmuster, in der Mitte eine leuchtende Achse. Rechts das Spiegelbild legen. ----
                int g = N >= 40 ? 4 : 3; PD[0] = g; int lit = 0;
                mat = 4; Vis(new Vector3(-0.05f, y, zc - g * 0.9f), new Vector3(0.05f, y + 0.05f, zc + g * 0.9f), gold, 1f);
                for (int i = 0; i < g * g; i++)
                {
                    int rr = i / g, cc = i % g; bool on = r.F() < 0.45f; if (on) lit++;
                    if (i == g * g - 1 && lit == 0) on = true;
                    PD[2 + i] = on ? 1 : 0;
                    float dz = zc + (rr - (g - 1) / 2f) * 1.75f, dx = 1.15f + cc * 1.75f;
                    mat = on ? 4 : 1; Vis(new Vector3(-dx - 0.75f, y, dz - 0.75f), new Vector3(-dx + 0.75f, y + 0.05f, dz + 0.75f), on ? gold * 0.2f : dim, on ? 1f : 0f);
                    AddPlate(new Vector3(dx, y, dz), true, false, 0.75f);
                }
                break;
            }
            case 18:
            {   // ---- ZAEHLER: eine Platte zaehlt um a hoch, die andere um b herunter. Den markierten Stand genau treffen. ----
                int max = N >= 30 ? 18 : 12; int[][] pairs = { new[] { 5, 3 }, new[] { 4, 3 }, new[] { 7, 4 }, new[] { 5, 2 }, new[] { 7, 5 } };
                var pr = pairs[(int)(r.F() * (max > 12 ? 4.999f : 1.999f))]; int a = pr[0], b = pr[1];
                var dist = new int[max + 1]; Array.Fill(dist, -1); dist[0] = 0; var q = new Queue<int>(); q.Enqueue(0);
                while (q.Count > 0) { int v = q.Dequeue(); foreach (int nv in new[] { v + a, v - b }) if (nv >= 0 && nv <= max && dist[nv] < 0) { dist[nv] = dist[v] + 1; q.Enqueue(nv); } }
                int target = a, best = 0;
                for (int v = 1; v <= max; v++) if (dist[v] >= 4 && (dist[v] > best || (dist[v] == best && r.F() < 0.4f))) { best = dist[v]; target = v; }
                PD[0] = max; PD[1] = a; PD[2] = b; PD[3] = target;
                Vector3 pa = new(-2.2f, y, zc), pb = new(2.2f, y, zc);
                AddPlate(pa, false); Pips(pa, a, 1.05f, gold); AddPlate(pb, false); Pips(pb, b, -1.05f, Wall * 2.2f);
                for (int k = 0; k <= max; k++)
                {
                    Vector3 p = new((k - max / 2f) * 0.6f, y, af - 2.4f);
                    Post(p, 0.1f, k == target ? 1.3f : 0.3f, k == target ? gold : dim, k == target ? 4 : 0);
                    if (k > 0) PD[4 + k] = AddInd(p + new Vector3(-0.07f, 0, -0.2f), p + new Vector3(0.07f, 0.55f, -0.06f), gold);
                }
                break;
            }
            case 19:
            {   // ---- GEKOPPELTE RAEDER: jede Platte dreht zwei Zaehlwerke zugleich weiter. Ziel: alle Zaehlwerke voll. ----
                int nd = N >= 35 ? 4 : 3, m = 3; PD[0] = nd; PD[1] = m;
                var val = new int[nd]; Array.Fill(val, m - 1);
                bool any = false;
                for (int j = 0; j < nd; j++) { int t = (int)(r.F() * (m - 0.001f)); if (j == nd - 1 && !any) t = 1; if (t > 0) any = true; for (int k = 0; k < t; k++) { val[j] = (val[j] + 1) % m; val[(j + 1) % nd] = (val[(j + 1) % nd] + 1) % m; } }
                if (AllFull(val, m)) { val[0] = (val[0] + 1) % m; val[1] = (val[1] + 1) % m; }
                for (int d = 0; d < nd; d++)
                {
                    PD[2 + d] = val[d]; float dx = (d - (nd - 1) / 2f) * 3.4f;
                    Post(new Vector3(dx, y, zc + 2.6f), 0.5f, 0.5f, Wall * 1.3f, 0);
                    for (int k = 0; k < m - 1; k++)
                    {
                        Vector3 p = new(dx, y + 0.55f + 0.5f * k, zc + 2.6f);
                        PD[8 + d * 4 + k] = AddInd(p - new Vector3(0.22f, 0, 0.22f), p + new Vector3(0.22f, 0.4f, 0.22f), gold, k < val[d]);
                    }
                }
                for (int j = 0; j < nd; j++)
                {   // Platte j steht zwischen ihren beiden Zaehlwerken; die letzte (verbindet Ende und Anfang) steht vorn in der Mitte
                    Vector3 c = j < nd - 1 ? new Vector3((j + 0.5f - (nd - 1) / 2f) * 3.4f, y, zc + 0.4f) : new Vector3(0, y, zc - 2.4f);
                    AddPlate(c, false);
                }
                break;
            }
            case 20:
            {   // ---- GEHEIMCODE: Symbolplatten nacheinander betreten. Nach jedem Versuch zeigt das Tor, wie viele Stellen stimmen. ----
                int k = N >= 35 ? 4 : 3, len = k; PD[0] = k; PD[1] = len;
                for (int i = 0; i < len; i++) PD[2 + i] = (int)(r.F() * k * 0.999f);
                for (int i = 0; i < k; i++) { Vector3 c = new((i - (k - 1) / 2f) * 2.8f, y, zc); AddPlate(c, false); Pips(c, i + 1, 1.05f, gold); }
                for (int i = 0; i < len; i++)
                {
                    Vector3 pe = new((i - (len - 1) / 2f) * 0.5f, y, zc + 2.6f), pf = new((i - (len - 1) / 2f) * 0.8f, y, af - 2.4f);
                    Post(pe, 0.12f, 0.25f, dim, 0); PD[8 + i] = AddInd(pe + new Vector3(-0.08f, 0, -0.08f), pe + new Vector3(0.08f, 0.5f, 0.08f), Wall * 2.5f);
                    Post(pf, 0.2f, 0.3f, dim, 0); PD[14 + i] = AddInd(pf + new Vector3(-0.14f, 0, -0.14f), pf + new Vector3(0.14f, 1.1f, 0.14f), gold);
                }
                break;
            }
        }
        IndInit = indInit.ToArray(); IndOn = (bool[])IndInit.Clone();
    }

    static bool AllTrue(bool[] a) { foreach (bool b in a) if (!b) return false; return true; }
    static bool AllFull(int[] a, int m) { foreach (int v in a) if (v != m - 1) return false; return true; }

    /// <summary>Figuren des Zaehlschlosses: 0 = hoher duenner Pfahl, 1 = gedrungener Block, 2 = schwebender Wuerfel ueber einem Stab.</summary>
    void Totem(Vector3 p, int kind, float s)
    {
        if (kind == 0) Post(p, 0.28f * s, 3.2f * s, Wall * 1.4f, 0);
        else if (kind == 1) Post(p, 1.0f * s, 0.7f * s, Wall * 1.4f, 0);
        else { Post(p, 0.12f * s, 1.5f * s, Wall * 1.1f, 0); Post(p + new Vector3(0, 1.7f * s, 0), 0.55f * s, 0.55f * s, Wall * 1.4f, 0); }
    }
}
