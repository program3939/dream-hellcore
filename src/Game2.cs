// Dreamy Hellcore - Phase "nie repetitiv": dynamische Plattformen, Schienenfahrt, Regisseur der Schreckmomente, Bot.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace DreamyHellcore;

static unsafe partial class Game
{
    static readonly List<Box> statics = new();
    static float simT, gravNow = Room.Gravity, lastGroundY;
    static int groundKey = -1; static Vector3 groundOff; static Vector3 windNow;

    // ---- Raum-Slots: 0 = aktueller Raum, 1 = naechster, 2 = vorheriger. Jeder Raum endet auf eigener Hoehe (ExitY). ----
    static Room Slot(int s) => s == 0 ? cur : s == 1 ? next : prev;
    static Vector3 Off(int s) => s == 0 ? Vector3.Zero : s == 1 ? new Vector3(0, cur.ExitY, cur.L) : new Vector3(0, -prev.ExitY, -prev.L);

    /// <summary>Versatz einer dynamischen Plattform zur Spielzeit t. solid = traegt, visible = wird gezeichnet.</summary>
    static Vector3 DynOffset(Room r, int i, float t, out bool solid, out bool visible)
    {
        Box b = r.Dyn[i]; solid = visible = true;
        if (b.Kind == 6) { solid = visible = !(r.Open || r.GateSoft); return Vector3.Zero; }                       // Tor des Escape-Raums
        if (b.Kind == 7) { solid = false; visible = !r.SealTaken[(int)b.Rate]; return new Vector3(0, 0.15f * MathF.Sin(t * 1.3f + b.Phase), 0); }   // Siegel
        if (b.Kind == 9) { solid = false; visible = r.IndOn[(int)b.Rate]; return Vector3.Zero; }                    // Leuchtanzeige eines Raetsels
        if (b.Kind == 1) return b.Amp * MathF.Sin(t * b.Rate + b.Phase);                    // bewegt
        if (b.Kind == 2)
        {   // verschwindend: 66 % der Periode da, blinkt vor dem Verschwinden
            float c = (t + b.Phase) % b.Rate / b.Rate; solid = c < 0.66f;
            visible = c < 0.5f || (solid && (int)(t * 12) % 2 == 0); return Vector3.Zero;
        }
        if (b.Kind == 3)
        {   // broeckelnd: zittert 1,1 s nach dem Betreten, faellt, kehrt nach 6 s zurueck
            float tt = r.DynT[i]; if (tt < 0) return Vector3.Zero;
            float age = t - tt; if (age > 6f) { r.DynT[i] = -1; return Vector3.Zero; }
            if (age < 1.1f) return new Vector3(0, -0.04f * MathF.Sin(age * 45), 0);
            solid = false; visible = age < 3.5f; float f = age - 1.1f; return new Vector3(0, -11f * f * f, 0);
        }
        return Vector3.Zero;
    }

    static void BuildWorld()
    {
        statics.Clear();
        for (int s = 0; s < 3; s++)
        {
            Room r = Slot(s); if (r == null) continue; Vector3 o = Off(s);
            foreach (var b in r.Col) { var c = b; c.Min += o; c.Max += o; statics.Add(c); }
        }
        groundKey = -1; UpdateWorld();
        SetWindowTitle("Dreamy Hellcore - Hellstage " + stage.ToString(CultureInfo.InvariantCulture)); Discord.Set(stage, cur);
    }

    /// <summary>Kollider dieses Bildes: feste Quader plus dynamische an ihrer momentanen Position.</summary>
    static void UpdateWorld()
    {
        world.Clear(); world.AddRange(statics);
        for (int s = 0; s < 3; s++)
        {
            Room r = Slot(s); if (r == null) continue; Vector3 o = Off(s);
            for (int i = 0; i < r.Dyn.Count; i++)
            {
                Vector3 d = DynOffset(r, i, simT, out bool solid, out _); if (!solid) continue;
                var c = r.Dyn[i]; c.Min += o + d; c.Max += o + d; c.Dyn = s * 10000 + i; world.Add(c);
            }
        }
    }

    static void DrawDyn(Material mt)
    {
        for (int s = 0; s < 3; s++)
        {
            Room r = Slot(s); if (r == null) continue; Vector3 o = Off(s);
            for (int i = 0; i < r.DynMesh.Count; i++)
            {
                Vector3 d = DynOffset(r, i, simT, out _, out bool vis); if (!vis) continue;
                Matrix4x4 m = Matrix4x4.Identity; m.M14 = o.X + d.X; m.M24 = o.Y + d.Y; m.M34 = o.Z + d.Z;
                DrawMesh(r.DynMesh[i], mt, m);
            }
        }
    }

    // ---- nach jedem Physikschritt mit Bodenkontakt ----
    static void OnGround(float h)
    {
        lastGroundY = pos.Y;
        if (groundBox.Dyn >= 0)
        {
            int key = groundBox.Dyn; Room r = Slot(key / 10000); int i = key % 10000;
            if (key != groundKey) { groundKey = key; groundOff = DynOffset(r, i, simT, out _, out _); }
            if (r.Dyn[i].Kind == 3 && r.DynT[i] < 0) { r.DynT[i] = simT; Audio.Crack(); }
        }
        else groundKey = -1;
        if (groundBox.Kind == 4) { vel.Y = Room.BounceV; grounded = false; coyote = 0; Audio.Bounce(); }   // Sprungfeld
        else if (groundBox.Kind == 5) pos += groundBox.Amp * h;                                           // Foerderband
        if (groundBox.Rail && pos.Z > groundBox.Max.Z - 1.0f) StartRide();
    }

    // ---- Schienenfahrt (Achterbahn): der Spieler wird getragen, Schwung aus dem Gefaelle ----
    static Room.RailT ride; static float rideS, rideV, rideBank; static int rideIdx; static Vector3 rideOff, rideDir;
    static void StartRide()
    {
        for (int s = 0; s < 3; s++)
        {
            Room r = Slot(s); if (r == null) continue; Vector3 o = Off(s);
            foreach (var rt in r.Rails)
                if (rt.Start.Min + o == groundBox.Min && rt.Start.Max + o == groundBox.Max)
                { ride = rt; rideOff = o; rideS = 0; rideV = 9; rideIdx = 0; rideDir = Vector3.UnitZ; groundKey = -1; return; }
        }
    }
    static void UpdateRide(float dt)
    {
        var P = ride.P; var S = ride.S;
        while (rideIdx < S.Length - 2 && S[rideIdx + 1] < rideS) rideIdx++;
        int i = rideIdx;
        float t = M.C01((rideS - S[i]) / MathF.Max(0.001f, S[i + 1] - S[i]));
        Vector3 dir = Vector3.Normalize(P[i + 1] - P[i]);
        rideBank += (Math.Clamp((dir.X - rideDir.X) / MathF.Max(dt, 0.001f) * -0.05f, -0.4f, 0.4f) - rideBank) * MathF.Min(1, dt * 4);
        rideDir = dir;
        rideV = Math.Clamp(rideV + (-dir.Y * 16f + 1.5f) * dt, 7f, 30f);
        rideS += rideV * dt;
        pos = Vector3.Lerp(P[i], P[i + 1], t) + rideOff - new Vector3(0, 0.2f, 0); vel = dir * rideV; grounded = false;
        Audio.RailSpeed = rideV;
        if (rideS >= S[^1])
        {
            pos = ride.End + rideOff; vel = new Vector3(0, 0, MathF.Min(rideV, 7f)); lastGroundY = pos.Y;
            ride = null; rideBank = 0; Audio.RailSpeed = 0; Audio.Land(0.8f);
        }
    }

    // =====================================================================================================
    // REGISSEUR: Wellen aus Anspannung und Entlastung (Andersen 2020), Erwartungsbruch (Miller et al.),
    // nie zweimal dasselbe Ereignis (Gewoehnung verhindern), Stille vor dem Ereignis - und manchmal
    // bleibt der erwartete Schrecken einfach aus.
    // =====================================================================================================
    public static float tension;
    static float tPhase, tPeriod = 55, relief = 14, nextEvt = 24, pendT; static int pendEvt = -1, lastEvt = -1, lastEvt2 = -1;
    static float fxRoll, fxRollT, fxRollTarget, fxFov, fxZoomT, fxInvertT, fxGlitch, fxSweep = -9, fxEyes, fxDarkT, fxGoneT, fxMirrorT,
                 lurchT, stutterT, fxShakeT, sTilt, fxShift, fxLampOff, lookAwayT;
    static Vector3 fxShake; static bool fxFlashNow, fxStalk;
    //                                 0 Fluestern 1 Licht aus 2 Blitz 3 Schrei 4 Schlag 5 Raum kippt 6 Schwindel-Zoom 7 Negativ 8 Stille 9 Musik
    static readonly float[] evtMin = { 0.45f, 0.50f, 0.50f, 0.55f, 0.30f, 0.40f, 0.35f, 0.60f, 0.25f, 0.00f,
    //                                 10 Beben 11 alle Lichter aus 12 Augen auf 13 Zeitstottern 14 falscher Klang 15 Atem 16 Schwerkraft-Ruck
                                       0.30f, 0.65f, 0.50f, 0.60f, 0.30f, 0.50f, 0.45f,
    //                                 17 Schatten zieht vorbei 18 Verfolger springt naeher 19 Herzrasen 20 ferner Chor 21 rennende Schritte
                                       0.35f, 0.60f, 0.40f, 0.30f, 0.50f,
    //                                 22 Raum verschiebt sich 23 Weg hinter dir verschwindet 24 Steuerung gespiegelt
                                       0.30f, 0.70f, 0.80f };
    static float Rnd() => (float)grng.NextDouble();

    static bool Frozen(float dt) { if (stutterT <= 0) return false; stutterT -= dt; return true; }

    static void OnCheckpoint(bool fake)
    {   // Checkpoint = Entlastung (Chaiyanan und Teeravarunyou 2025). Ein "falscher" traegt trotzdem - nur sein Licht stirbt.
        Audio.Chime(fake); relief = fake ? 2f : 7f + 4f * Rnd(); tPhase *= 0.35f;
        if (fake) fxDarkT = 3.5f;
    }
    static void OnRoomEnter() { EscapeReset(); Audio.AmbientSet(stage, cur.Theme, cur.Mood); if (cur.Mood < 0.3f && Rnd() < 0.5f) Audio.Music(9 + 8 * Rnd()); }

    static void Direct(float dt, float mood, Vector3 look)
    {
        // --- Spannungswelle: baut sich auf, loest sich, Grundniveau steigt mit der Stimmung ---
        if (relief > 0) { relief -= dt; tension += (mood * 0.2f - tension) * MathF.Min(1, dt * 0.8f); }
        else
        {
            tPhase += dt / tPeriod;
            if (tPhase >= 1) { tPhase = 0; tPeriod = 40 + 45 * Rnd(); relief = 8 + 8 * Rnd(); }
            tension += (mood * (0.4f + 0.6f * MathF.Pow(tPhase, 1.5f)) - tension) * MathF.Min(1, dt * 0.5f);
        }
        // --- laufende Effekte ---
        fxRollT -= dt; fxRoll += ((fxRollT > 0 ? fxRollTarget : 0) - fxRoll) * MathF.Min(1, dt * 1.4f);
        fxZoomT -= dt; fxFov = fxZoomT > 0 ? -24f * MathF.Sin(MathF.PI * (1 - fxZoomT / 3f)) : 0;
        fxInvertT -= dt; fxGlitch *= MathF.Exp(-dt * 2.5f); fxEyes = MathF.Max(0, fxEyes - dt / 3f);
        if (fxSweep > -5) { fxSweep += dt * 1.7f; if (fxSweep > 1.6f) fxSweep = -9; }
        fxDarkT -= dt; fxGoneT -= dt; fxMirrorT -= dt; lurchT -= dt; fxShakeT -= dt;
        fxShake = fxShakeT > 0 ? new Vector3(Rnd() - 0.5f, Rnd() - 0.5f, 0) * 0.09f * MathF.Min(1, fxShakeT) : Vector3.Zero;
        sTilt += (cur.Tilt - sTilt) * MathF.Min(1, dt * 0.7f);
        // --- der Raum veraendert sich, waehrend man wegsieht ---
        if (lookAwayT < 0) lookAwayT += dt;
        else if (look.Z < -0.15f) { lookAwayT += dt; if (lookAwayT > 1.2f && mood > 0.18f) { fxShift += 1; lookAwayT = -5 - 12 * Rnd(); } }
        else lookAwayT = 0;
        // --- Ereignisse ---
        if (pendEvt >= 0) { pendT -= dt; if (pendT <= 0) { Fire(pendEvt); pendEvt = -1; } return; }
        if (relief > 0) return;
        nextEvt -= dt * (0.5f + 1.5f * tension);
        if (nextEvt > 0) return;
        nextEvt = (9 + 17 * Rnd()) * (1.35f - 0.6f * mood);
        if (mood > 0.25f && Rnd() < 0.2f) { Audio.Silence(1.5f + 2.5f * Rnd()); return; }   // die Stille kommt - und nichts geschieht
        int e = 9;
        for (int k = 0; k < 14; k++) { int c = grng.Next(evtMin.Length); if (evtMin[c] <= mood && c != lastEvt && c != lastEvt2) { e = c; break; } }
        if (e == lastEvt) return;
        lastEvt2 = lastEvt; lastEvt = e;
        bool strong = e == 2 || e == 3 || e == 4 || e == 7 || e == 11;
        if (strong && Rnd() < 0.7f) { pendEvt = e; pendT = 0.8f + 2.4f * Rnd(); Audio.Silence(pendT); }   // Stille vor dem Ereignis
        else Fire(e);
    }

    static void Fire(int e)
    {
        switch (e)
        {
            case 0: Audio.Whisper(); break;
            case 1: fxLampOff = 1f + 2.5f * Rnd(); break;
            case 2: fxFlashNow = true; break;
            case 3: Audio.Scream(1f); fxGlitch = 1; break;
            case 4: Audio.Bang(Rnd() < 0.5f ? -0.8f : 0.8f); fxShakeT = 0.4f; break;
            case 5: fxRollTarget = (Rnd() < 0.5f ? -1 : 1) * (0.2f + 0.25f * Rnd()); fxRollT = 4 + 3 * Rnd(); break;
            case 6: fxZoomT = 3f; Audio.Drop(); break;
            case 7: fxInvertT = 0.1f + 0.12f * Rnd(); Audio.Scream(0.3f); break;
            case 8: Audio.Silence(3 + 3 * Rnd()); break;
            case 9: Audio.Music(9 + 10 * Rnd()); break;
            case 10: fxShakeT = 2.6f; Audio.Bang(0); Audio.Drop(); break;
            case 11: fxDarkT = 2.5f + 2 * Rnd(); Audio.LampClick(); break;
            case 12: fxEyes = 1; Audio.Choir(2.5f); break;
            case 13: stutterT = 0.35f; Audio.Buzz(); break;
            case 14: Audio.Chime(Rnd() < 0.5f); break;
            case 15: Audio.BreathNow(); break;
            case 16: lurchT = 1.6f; Audio.Drop(); break;
            case 17: fxSweep = -0.5f; break;
            case 18: fxStalk = true; break;
            case 19: Audio.Tachy(5 + 3 * Rnd()); break;
            case 20: Audio.Choir(5 + 4 * Rnd()); break;
            case 21: Audio.Steps(5 + grng.Next(4), Rnd() < 0.5f ? -0.8f : 0.8f); break;
            case 22: fxShift += 1; break;
            case 23: fxGoneT = 15 + 10 * Rnd(); break;
            case 24: fxMirrorT = 4f; Audio.Buzz(); break;
        }
    }

    // ---- Bot (nur Test): kennt bewegte/verschwindende Plattformen, Schienen und Hoehenversatz ----
    static Box Moved(Box b, int slot)
    {
        Vector3 o = Off(slot);
        if (b.Dyn >= 0) { o += DynOffset(Slot(slot), b.Dyn, simT, out _, out _); b.Dyn += slot * 10000; }
        b.Min += o; b.Max += o; return b;
    }
    static void Bot(out Vector3 wish, out bool crouch, out bool jump)
    {
        var pts = new List<Box>();
        foreach (var p in cur.PathBoxes) pts.Add(Moved(p, 0));
        for (int k = 0; k < 2 && k < next.PathBoxes.Count; k++) pts.Add(Moved(next.PathBoxes[k], 1));
        if (grounded)
            for (int i = 0; i < pts.Count - 1; i++)
                if (groundBox.Dyn >= 0 ? pts[i].Dyn == groundBox.Dyn : (pts[i].Dyn < 0 && pts[i].Min == groundBox.Min && pts[i].Max == groundBox.Max)) { botIdx = i; break; }
        botIdx = Math.Clamp(botIdx, 0, pts.Count - 2);
        Box a = pts[botIdx], b = pts[botIdx + 1];
        if (b.Kind == 1 && b.Dyn >= 0)
        {   // bewegtes Ziel: dorthin zielen, wo es in einer halben Sekunde ist
            Room rr = Slot(b.Dyn / 10000); int bi = b.Dyn % 10000;
            Vector3 dd = DynOffset(rr, bi, simT + 0.5f, out _, out _) - DynOffset(rr, bi, simT, out _, out _);
            b.Min += dd; b.Max += dd;
        }
        float tx = Math.Clamp((a.Min.X + a.Max.X) / 2, b.Min.X + 0.35f, MathF.Max(b.Min.X + 0.35f, b.Max.X - 0.35f));
        float tz = b.Min.Z + MathF.Min(0.8f, (b.Max.Z - b.Min.Z) / 2);
        bool onA = grounded && pos.Z < a.Max.Z + 0.2f;
        if (onA) { tx = Math.Clamp(tx, a.Min.X + 0.05f, MathF.Max(a.Min.X + 0.05f, a.Max.X - 0.05f)); tz = a.Max.Z - 0.05f; }
        crouch = false;
        foreach (var c in world)
            if (c.Min.Y > pos.Y + 0.5f && c.Min.Y < pos.Y + StandH + 0.1f && pos.X + R > c.Min.X && pos.X - R < c.Max.X
                && pos.Z + 1.3f > c.Min.Z && pos.Z - 1.3f < c.Max.Z) { crouch = true; break; }
        Vector3 d = new(tx - pos.X, 0, tz - pos.Z); float dl = d.Length();
        wish = dl > 0.15f ? d / dl * (crouch ? 3f : botWalk ? 4.5f : Room.SprintSpeed) : Vector3.Zero;
        wish -= windNow * (grounded ? 0.15f : 1f);   // der Bot haelt gegen den Wind
        // auf eine verschwindende Plattform nur springen, wenn sie noch lange genug da ist
        bool wait = b.Kind == 2 && (simT + b.Phase) % b.Rate / b.Rate > 0.3f;
        jump = onA && !crouch && !wait && !a.Rail && pos.Z >= a.Max.Z - 0.3f;
        if (wait && onA && pos.Z >= a.Max.Z - 0.6f) wish = Vector3.Zero;
        if (botFreeze && grounded) { wish = Vector3.Zero; jump = false; }   // Escape-Raum "Stillstehen"
    }
}
