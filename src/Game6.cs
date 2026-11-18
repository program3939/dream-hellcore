// Dreamy Hellcore - Hinweise fuer die Escape-Raeume: Titel und Regel als Text, und ein Loeser je Raetsel,
// der die naechste richtige Platte bestimmt (Taste H, oder von selbst nach 75 s bzw. 4 Fehlversuchen).
using System;
using System.Collections.Generic;
using System.Numerics;

namespace DreamyHellcore;

static unsafe partial class Game
{
    static int hintPlate = -1; static bool hintDirty = true, hintOn, menuSkip, argHint; static float chamberT, roomT;

    public static readonly string[] PuzTitle =
    {
        "", "Seals", "Sequence", "Don't Look", "Stand Still", "Flee", "", "", "", "", "",
        "Lights Out", "Sum", "Counting Lock", "Every Stone Once", "The Odd One", "Scale", "Mirror Image", "Counter", "Coupled Wheels", "Secret Code"
    };
    public static readonly string[] PuzRule =
    {
        "", "Collect every seal.", "Step on the plates in the order of their pins.",
        "The gate is open only while you are not looking at it. Walk through backwards.",
        "When the tone swells, freeze. If you move while it watches, it takes you.",
        "The dark is coming from behind. Do not stop.", "", "", "", "", "",
        "Each plate flips itself and its four neighbours. Make every plate glow.",
        "Each plate is worth its pins. Light plates that add up to the pins before the gate.",
        "Count the large figures of each kind. Set each counter to that number - the small figure shows which kind.",
        "Step on every plate exactly once, always onto a direct neighbour.",
        "Every pillar has a twin except one. Stand before the one without a twin.",
        "Every plate sings. Step on them from the lowest tone to the highest.",
        "Copy the light pattern on the left as its mirror image on the right.",
        "One plate counts up, the other counts down. Stop exactly at the tall pin.",
        "Each plate turns the two counters next to it. Fill every counter.",
        "Step on the symbols one after another. The gate shows how many are in the right place."
    };

    /// <summary>Selbsttest: das Raetsel nur durch Befolgen der Hinweise loesen (prueft Loeser und Raetsellogik zugleich).</summary>
    static string SolveByHints(Room r)
    {
        Room keep = cur; bool g0 = grounded; Vector3 p0 = pos; cur = r;
        PuzzleInit(r); int steps = 0;
        for (; steps < 150 && !r.Open; steps++)
        {
            int p = ComputeHint(r); if (p < 0) break;
            lastPlate = -1; pzLock = 0; grounded = true; pos = r.Plates[p];
            PuzzleTick(r, 0.016f);
        }
        bool ok = r.Open; r.Open = false; PuzzleInit(r); cur = keep; grounded = g0; pos = p0;
        return ok ? $"per Hinweis geloest in {steps} Schritten" : $"NICHT geloest (nach {steps} Schritten)";
    }

    static void HintReset() { hintOn = argHint; hintDirty = true; hintPlate = -1; chamberT = 0; roomT = 0; }

    /// <summary>Naechste richtige Platte fuer den aktuellen Zustand des Raetsels (-1 = keine).</summary>
    static int ComputeHint(Room r)
    {
        int[] D = r.PD; int n = r.Plates.Count;
        switch (r.Escape)
        {
            case 11:
            {   // Lichter aus: alle Kombinationen durchprobieren (2^9 bzw. 2^16), die mit den wenigsten Schritten nehmen
                int g = D[0], N2 = g * g, curM = 0, full = (1 << N2) - 1, best = -1, bestC = 99;
                var tg = new int[N2];
                for (int i = 0; i < N2; i++)
                {
                    if (IsLit(r, i)) curM |= 1 << i;
                    int rr = i / g, cc = i % g, m = 1 << i;
                    if (rr > 0) m |= 1 << (i - g); if (rr < g - 1) m |= 1 << (i + g); if (cc > 0) m |= 1 << (i - 1); if (cc < g - 1) m |= 1 << (i + 1);
                    tg[i] = m;
                }
                for (int mask = 1; mask <= full; mask++)
                {
                    int s = curM, c = 0;
                    for (int i = 0; i < N2; i++) if ((mask >> i & 1) != 0) { s ^= tg[i]; c++; }
                    if (s == full && c < bestC) { bestC = c; best = mask; }
                }
                if (best > 0) for (int i = 0; i < N2; i++) if ((best >> i & 1) != 0) return i;
                return -1;
            }
            case 12:
            {   // Summe: die passende Auswahl, die dem aktuellen Stand am naechsten ist
                int cnt = D[0], curM = 0, best = -1, bd = 99;
                for (int i = 0; i < cnt; i++) if (IsLit(r, i)) curM |= 1 << i;
                for (int mask = 0; mask < 1 << cnt; mask++)
                {
                    int sum = 0; for (int i = 0; i < cnt; i++) if ((mask >> i & 1) != 0) sum += D[2 + i];
                    int d = BitOperations.PopCount((uint)(mask ^ curM));
                    if (sum == D[1] && d < bd) { bd = d; best = mask; }
                }
                if (best < 0 || bd == 0) return -1;
                int diff = best ^ curM; for (int i = 0; i < cnt; i++) if ((diff >> i & 1) != 0) return i;
                return -1;
            }
            case 13: for (int d = 0; d < 3; d++) if (pz[d] != D[d]) return d; return -1;
            case 14:
            {   // Jeder Stein einmal: Tiefensuche nach einem Weg, der alle uebrigen Platten genau einmal trifft
                int R = D[0], C = D[1], visited = pz[1], last = pz[0] - 1, left = n - pz[2];
                bool Dfs(int cell, int vis, int rem)
                {
                    if (rem == 0) return true;
                    int rr = cell / C, cc = cell % C;
                    for (int k = 0; k < 4; k++)
                    {
                        int r2 = rr + (k == 0 ? 1 : k == 1 ? -1 : 0), c2 = cc + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (r2 < 0 || c2 < 0 || r2 >= R || c2 >= C) continue;
                        int p = D[2 + r2 * C + c2]; if (p < 0 || (vis >> p & 1) != 0) continue;
                        if (Dfs(r2 * C + c2, vis | 1 << p, rem - 1)) return true;
                    }
                    return false;
                }
                if (last < 0) { for (int p = 0; p < n; p++) if (Dfs(D[40 + p], 1 << p, n - 1)) return p; return -1; }
                int lr = last / C, lc = last % C;
                for (int k = 0; k < 4; k++)
                {
                    int r2 = lr + (k == 0 ? 1 : k == 1 ? -1 : 0), c2 = lc + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (r2 < 0 || c2 < 0 || r2 >= R || c2 >= C) continue;
                    int p = D[2 + r2 * C + c2]; if (p < 0 || (visited >> p & 1) != 0) continue;
                    if (Dfs(r2 * C + c2, visited | 1 << p, left - 1)) return p;
                }
                for (int p = 0; p < n; p++) if ((visited >> p & 1) != 0) return p;   // Sackgasse: eine betretene Platte setzt zurueck
                return -1;
            }
            case 15: return D[1];
            case 16: for (int i = 0; i < n; i++) if (D[2 + i] == pz[0]) return i; return -1;
            case 17: for (int i = 0; i < n; i++) if (IsLit(r, i) != (D[2 + i] == 1)) return i; return -1;
            case 18:
            {   // Zaehler: Breitensuche vom aktuellen Stand zum Ziel; liefert die erste Platte des kuerzesten Weges
                int max = D[0], a = D[1], b = D[2], t = D[3], v0 = pz[0];
                if (v0 == t) return -1;
                var first = new int[max + 1]; Array.Fill(first, -2); first[v0] = -1;
                var q = new Queue<int>(); q.Enqueue(v0);
                while (q.Count > 0)
                {
                    int v = q.Dequeue();
                    for (int mv = 0; mv < 2; mv++)
                    {
                        int nv = mv == 0 ? v + a : v - b;
                        if (nv < 0 || nv > max || first[nv] != -2) continue;
                        first[nv] = first[v] == -1 ? mv : first[v];
                        if (nv == t) return first[nv];
                        q.Enqueue(nv);
                    }
                }
                return -1;
            }
            case 19:
            {   // Gekoppelte Raeder: alle Druckzahlen durchprobieren (3^nd), die kuerzeste Loesung nehmen
                int nd = D[0], m = D[1], combos = 1, bestSum = 99, bestCode = -1;
                for (int d = 0; d < nd; d++) combos *= m;
                var x = new int[nd];
                for (int code = 0; code < combos; code++)
                {
                    int c = code, sum = 0; for (int d = 0; d < nd; d++) { x[d] = c % m; c /= m; sum += x[d]; }
                    bool ok = true;
                    for (int d = 0; d < nd && ok; d++) ok = (pz[d] + x[d] + x[(d - 1 + nd) % nd]) % m == m - 1;
                    if (ok && sum < bestSum) { bestSum = sum; bestCode = code; }
                }
                if (bestCode <= 0) return -1;
                for (int d = 0; d < nd; d++) { if (bestCode % m > 0) return d; bestCode /= m; }
                return -1;
            }
            case 20: return pz[0] < D[1] ? D[2 + pz[0]] : -1;
        }
        return -1;
    }
}
