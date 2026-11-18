// Dreamy Hellcore - Escape-Raeume: jede vierte Hellstage (3, 7, 11, ...) ist eine Pruefung mit verschlossenem Tor.
// Fuenf Arten (Room.Escape): 1 Siegel sammeln, 2 Platten in der richtigen Reihenfolge, 3 "Nicht hinsehen",
// 4 "Stillstehen, wenn es schaut", 5 Flucht vor der Dunkelheit. Jede Art erscheint im Design einer Stufe
// (Room.Style: 0 falscher Himmel, 1 Unbehagen, 2 Grauen, 3 Terror, 4 Leere). Man stirbt nie: wer scheitert,
// wird ergriffen (Schrei, Schwarzblende) und steht wieder am letzten Checkpoint.
using System;
using System.Numerics;

namespace DreamyHellcore;

static unsafe partial class Game
{
    static float sT, sHeaven, slipT, watchT = 4, watchU, frontZ = -99999f, gateLookAway, watchGrace;
    static int sealsGot, plateNext, lastPlate = -1, watchState;   // watchState: 0 ruht, 1 Vorwarnung, 2 schaut
    static bool frontOn, botFreeze;

    static void EscapeReset()
    {
        sealsGot = 0; plateNext = 0; lastPlate = -1; frontOn = false; frontZ = -99999f; watchT = 3 + 3 * Rnd(); watchState = 0; watchU = 0;
        gateLookAway = 0; botFreeze = false; if (cur != null) PuzzleInit(cur); HintReset(); Audio.Watch = 0; Audio.Front = 0;
    }
    static void OpenGate()
    {
        if (cur.Open) return;
        cur.Open = true; Audio.GateOpen(); relief = 10f; tPhase = 0;   // geloest: die groesste Entlastung im Spiel
    }
    /// <summary>Ergriffen: kein Tod - ein Schrei, die Maske verrutscht, zurueck zum Checkpoint.</summary>
    static void Caught()
    {
        Audio.Scream(0.8f); fxGlitch = 1; slipT = 1.3f; fxShakeT = 0.5f; Respawn();
        if (cur.Escape == 5) frontZ = respawn.Z - 15f;
    }

    static void EscapeTick(float dt, Vector3 look, bool sprinting)
    {
        slipT -= dt;
        Room r = cur; int kind = r.Escape;
        botFreeze = false;
        if (kind == 0 || r.Open) { watchU += (0 - watchU) * MathF.Min(1, dt * 3); Audio.Watch = watchU; Audio.Front = 0; frontOn = false; return; }
        float diffE = M.C01(0.15f + stage / 50f);
        bool onAltar = pos.Z > r.AltarZ && grounded;
        if (botOn && onAltar && (kind <= 3 || kind >= 11)) { OpenGate(); return; }   // der Test-Bot loest keine Raetsel
        if (kind >= 11) { PuzzleTick(r, dt); return; }                              // klassische Escape-Raeume: siehe Game4.cs

        if (kind == 1)
        {   // ---- Siegel: alle einsammeln. Mit jedem Siegel wird der Raum feindseliger. ----
            if (r.Seals.Count == 0) { if (onAltar) OpenGate(); return; }
            for (int i = 0; i < r.Seals.Count; i++)
            {
                if (r.SealTaken[i] || Vector3.Distance(pos + new Vector3(0, 0.9f, 0), r.Seals[i]) > 1.5f) continue;
                r.SealTaken[i] = true; sealsGot++; Audio.SealTone(sealsGot);
                if (sealsGot >= r.Seals.Count) OpenGate();
                else if (r.Heaven) slipT = 0.35f;                     // im falschen Himmel: die Maske verrutscht kurz
                else { fxDarkT = 0.8f + 0.4f * sealsGot; if (sealsGot >= 2) fxStalk = true; }
            }
        }
        else if (kind == 2)
        {   // ---- Platten: in der Reihenfolge ihrer Markierungen (1, 2, 3 ... Stifte) betreten. Fehler setzt zurueck. ----
            int on = -1;
            if (grounded) for (int i = 0; i < r.Plates.Count; i++)
                    if (MathF.Abs(pos.X - r.Plates[i].X) < 0.8f && MathF.Abs(pos.Z - r.Plates[i].Z) < 0.8f) on = i;
            if (on != lastPlate)
            {
                lastPlate = on;
                if (on == plateNext) { plateNext++; Audio.SealTone(plateNext); if (plateNext >= r.Plates.Count) OpenGate(); }
                else if (on > plateNext)
                {   // falsche Platte
                    plateNext = 0; Audio.Chime(true); Audio.Scream(0.35f + 0.4f * diffE);
                    if (r.Heaven) slipT = 0.9f; else { fxDarkT = 1.4f; fxShakeT = 0.4f; }
                    if (diffE > 0.5f) fxStalk = true;
                }
            }
        }
        else if (kind == 3)
        {   // ---- Nicht hinsehen: das Tor ist nur offen, solange man es NICHT ansieht. Man muss rueckwaerts hindurch,
            //      waehrend hinter dem Tor etwas naeher kommt, sobald man wegsieht. ----
            gateLookAway = look.Z < 0.15f ? gateLookAway + dt : 0;
            bool soft = gateLookAway > 0.3f + 2.2f * diffE;
            r.GateSoft = soft;
            if (pos.Z > r.GateZ + 0.7f) OpenGate();
            Audio.Watch = soft ? 0 : 0.25f;
        }
        else if (kind == 4)
        {   // ---- Stillstehen: etwas am Ende des Raums oeffnet in unregelmaessigen Abstaenden die Augen.
            //      Wer sich dann bewegt, wird ergriffen. Vorwarnung: ein anschwellender Ton. ----
            bool safe = pos.Z < 7f || pos.Z > r.AltarZ;
            if (pos.Z > r.AltarZ && grounded) { OpenGate(); return; }
            watchT -= dt;
            if (watchT <= 0)
            {
                watchState = (watchState + 1) % 3;
                watchT = watchState == 1 ? 0.95f : watchState == 2 ? 1.8f + 1.6f * Rnd() + 1.2f * diffE : 2.6f + 4.2f * Rnd() * (1.2f - 0.6f * diffE);
                if (watchState == 2) watchGrace = 0.28f;
            }
            float target = watchState == 1 ? 0.45f : watchState == 2 ? 1f : 0f;
            watchU += (target - watchU) * MathF.Min(1, dt * (watchState == 2 ? 14 : 4));
            Audio.Watch = watchU;
            botFreeze = watchState != 0 && !safe;
            if (watchState == 2 && !safe)
            {
                watchGrace -= dt;
                float hs = MathF.Sqrt(vel.X * vel.X + vel.Z * vel.Z);
                if (watchGrace <= 0 && (hs > 0.9f || !grounded)) { watchState = 0; watchT = 3f; Caught(); }
            }
        }
        else if (kind == 5)
        {   // ---- Flucht: hinter dem Spieler frisst sich Dunkelheit durch den Raum. Nicht stehen bleiben. ----
            if (pos.Z > r.L - 16.2f && grounded) { OpenGate(); frontOn = false; frontZ = -99999f; Audio.Front = 0; return; }
            if (!frontOn && pos.Z > 9f) { frontOn = true; frontZ = pos.Z - 17f; Audio.Bang(0); }
            if (frontOn)
            {
                frontZ += M.Lerp(6.0f, 8.3f, diffE) * dt;
                Audio.Front = M.C01(1 - (pos.Z - frontZ) / 26f);
                if (frontZ > pos.Z - 0.4f) Caught();
            }
        }
    }
}
