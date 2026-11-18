// Dreamy Hellcore - Logik der zehn klassischen Escape-Raeume (Raetselkammer, siehe Puzzles.cs).
// Eingabe ist immer nur: auf eine Platte treten. Der Zustand steht in pz[] und in Room.IndOn (Leuchtanzeigen).
// Fehler toeten nicht - sie machen den Raum feindseliger (Dunkelheit, Schrei, der Verfolger rueckt naeher,
// im falschen Himmel verrutscht die Maske).
using System;
using System.Numerics;

namespace DreamyHellcore;

static unsafe partial class Game
{
    static readonly int[] pz = new int[24];
    static float pzLock; static int pzTries;

    static void PuzzleInit(Room r)
    {
        Array.Clear(pz); pzLock = 0; pzTries = 0; lastPlate = -1;
        if (r.Escape < 11 || r.Open) return;
        Array.Copy(r.IndInit, r.IndOn, r.IndInit.Length);
        if (r.Escape == 19) for (int d = 0; d < r.PD[0]; d++) pz[d] = r.PD[2 + d];
    }
    static void Lit(Room r, int plate, bool v) { int id = r.PlateLit[plate]; if (id >= 0) r.IndOn[id] = v; }
    static bool IsLit(Room r, int plate) { int id = r.PlateLit[plate]; return id >= 0 && r.IndOn[id]; }

    /// <summary>Fehlversuch: kein Tod, aber der Raum antwortet. Jeder dritte Fehler wiegt schwerer.</summary>
    static void Punish(Room r, float level)
    {
        pzTries++; Audio.Chime(true); pzLock = 0.7f;
        if (r.Heaven) slipT = 0.5f + 0.6f * level; else { fxDarkT = 0.8f + level; fxShakeT = 0.3f; }
        if (level > 0.5f || pzTries % 3 == 0) { Audio.Scream(0.3f + 0.5f * level); fxStalk = true; fxGlitch = 0.7f; }
    }

    static void PuzzleTick(Room r, float dt)
    {
        pzLock -= dt;
        int on = -1; float best = 1e9f;
        if (grounded)
            for (int i = 0; i < r.Plates.Count; i++)
            {
                float dx = MathF.Abs(pos.X - r.Plates[i].X), dz = MathF.Abs(pos.Z - r.Plates[i].Z);
                if (dx < 0.8f && dz < 0.8f && MathF.Abs(pos.Y - r.Plates[i].Y) < 0.6f && dx + dz < best) { best = dx + dz; on = i; }
            }
        if (on == lastPlate) return;
        lastPlate = on;
        if (on < 0 || pzLock > 0) return;
        hintDirty = true; int[] D = r.PD; int n = r.Plates.Count;
        switch (r.Escape)
        {
            case 11:
            {   // Lichter aus
                int g = D[0], rr = on / g, cc = on % g;
                Lit(r, on, !IsLit(r, on));
                if (rr > 0) Lit(r, on - g, !IsLit(r, on - g)); if (rr < g - 1) Lit(r, on + g, !IsLit(r, on + g));
                if (cc > 0) Lit(r, on - 1, !IsLit(r, on - 1)); if (cc < g - 1) Lit(r, on + 1, !IsLit(r, on + 1));
                bool all = true; for (int i = 0; i < n; i++) all &= IsLit(r, i);
                Audio.Tone(330 + 40 * on); if (all) OpenGate();
                break;
            }
            case 12:
            {   // Summe
                Lit(r, on, !IsLit(r, on)); int sum = 0; for (int i = 0; i < n; i++) if (IsLit(r, i)) sum += D[2 + i];
                Audio.Tone(300 + 30 * D[2 + on]); if (sum == D[1]) OpenGate();
                break;
            }
            case 13:
            {   // Zaehlschloss
                pz[on] = (pz[on] + 1) % 7;
                for (int k = 0; k < 6; k++) r.IndOn[D[4 + on * 8 + k]] = k < pz[on];
                Audio.Tone(280 + 45 * pz[on]);
                if (pz[0] == D[0] && pz[1] == D[1] && pz[2] == D[2]) OpenGate();
                break;
            }
            case 14:
            {   // Jeder Stein einmal: pz[0] = letzte Zelle + 1, pz[1] = Maske der betretenen Platten, pz[2] = Anzahl
                int C = D[1], cell = D[40 + on], last = pz[0] - 1;
                bool adj = last < 0 || Math.Abs(cell / C - last / C) + Math.Abs(cell % C - last % C) == 1;
                if ((pz[1] & (1 << on)) != 0 || !adj)
                {
                    for (int i = 0; i < n; i++) Lit(r, i, true);
                    pz[0] = 0; pz[1] = 0; pz[2] = 0; Punish(r, 0.3f);
                }
                else
                {
                    pz[1] |= 1 << on; pz[0] = cell + 1; pz[2]++; Lit(r, on, false); Audio.SealTone(1 + pz[2] % 7);
                    if (pz[2] >= n) OpenGate();
                }
                break;
            }
            case 15:
                if (on == D[1]) OpenGate(); else { Punish(r, 0.8f); pzLock = 2.5f; }   // Der Falsche
                break;
            case 16:
            {   // Tonleiter: pz[0] = naechster erwarteter Rang
                int rank = D[2 + on];
                Audio.Tone(D[10] * Math.Pow(2, rank * D[1] / 12.0));
                if (rank == pz[0]) { Lit(r, on, true); pz[0]++; if (pz[0] >= n) OpenGate(); }
                else
                {
                    for (int i = 0; i < n; i++) Lit(r, i, false);
                    pz[0] = rank == 0 ? 1 : 0; if (rank == 0) Lit(r, on, true);
                    if (++pzTries % 6 == 0) Punish(r, 0.4f);
                }
                break;
            }
            case 17:
            {   // Spiegelbild
                Lit(r, on, !IsLit(r, on)); bool ok = true;
                for (int i = 0; i < n; i++) ok &= IsLit(r, i) == (D[2 + i] == 1);
                Audio.Tone(360 + 25 * on); if (ok) OpenGate();
                break;
            }
            case 18:
            {   // Zaehler: Platte 0 zaehlt um a hoch, Platte 1 um b herunter
                int v = pz[0] + (on == 0 ? D[1] : -D[2]);
                if (v < 0 || v > D[0]) { v = 0; Punish(r, 0.4f); } else Audio.Tone(260 + 22 * v);
                pz[0] = v;
                for (int k = 1; k <= D[0]; k++) r.IndOn[D[4 + k]] = k <= v;
                if (v == D[3]) OpenGate();
                break;
            }
            case 19:
            {   // Gekoppelte Raeder: Platte j dreht Zaehlwerk j und j+1
                int nd = D[0], m = D[1]; bool full = true;
                pz[on] = (pz[on] + 1) % m; pz[(on + 1) % nd] = (pz[(on + 1) % nd] + 1) % m;
                for (int d = 0; d < nd; d++) { for (int k = 0; k < m - 1; k++) r.IndOn[D[8 + d * 4 + k]] = k < pz[d]; full &= pz[d] == m - 1; }
                Audio.Tone(300 + 60 * on); if (full) OpenGate();
                break;
            }
            case 20:
            {   // Geheimcode: pz[0] = Zahl der Eingaben, pz[1..] = Eingaben
                int len = D[1];
                pz[1 + pz[0]] = on; pz[0]++; Audio.Tone(320 + 70 * on);
                for (int i = 0; i < len; i++) r.IndOn[D[8 + i]] = i < pz[0];
                if (pz[0] >= len)
                {
                    int match = 0; for (int i = 0; i < len; i++) if (pz[1 + i] == D[2 + i]) match++;
                    for (int i = 0; i < len; i++) { r.IndOn[D[14 + i]] = i < match; r.IndOn[D[8 + i]] = false; }
                    pz[0] = 0;
                    if (match == len) OpenGate(); else if (++pzTries % 3 == 0) Punish(r, 0.5f); else Audio.Chime(true);
                }
                break;
            }
        }
    }
}
