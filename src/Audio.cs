// Dreamy Hellcore - prozedurale Stereo-Klangwelt (44,1 kHz), komplett zur Laufzeit erzeugt, keine Dateien.
// Der Klang folgt demselben Bogen wie das Bild (T = Stimmung 0..1):
//   Traum     : warmer Akkord (Dadd9), Spieluhr in Dur-Pentatonik, viel Hall, sanfter Wind.
//   Unbehagen : der Akkord kippt nach Moll, die Spieluhr wird langsamer und trifft falsche Toene.
//   Grauen    : Dissonanz, Bass-Droehnen, Herzschlag, Atmen, metallisches Aechzen in der Ferne.
//   Terror    : fast nur noch Bass, Herzschlag und Stille; Fluestern direkt am Ohr, eine sterbende Spieluhr,
//               Schritte, die weitergehen, wenn man stehen bleibt.
using System;

namespace DreamyHellcore;

static partial class Audio
{
    public const int SR = 44100, BL = 1024;
    public static readonly short[] Buf = new short[BL * 2];

    // ---- vom Spiel gesetzt ----
    public static float T;              // Stimmung 0..1
    public static float Vol = 1f;       // Gesamtlautstaerke (Pause)
    public static float Prox;           // Naehe des Verfolgers 0..1
    public static float Size = 0.2f;    // Raumgroesse 0..1 -> Halllaenge
    public static float BeatEnv;        // Huellkurve des Herzschlags fuer die Grafik (pulsierende Adern)

    const double Tau = Math.PI * 2, Dt = 1.0 / SR;
    static readonly Random rng = new Random(11);
    static double Rnd() => rng.NextDouble();
    static double White() => rng.NextDouble() - 0.5;
    static double SStep(double e0, double e1, double x) { double t = Math.Clamp((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t); }
    static double Lerp(double a, double b, double t) => a + (b - a) * t;

    // ---- Glocken / Spieluhr ----
    struct Bell { public double F, Ph, Age, Dec, Amp, Pan, Delay, Glide; public bool On; }
    static readonly Bell[] bells = new Bell[14];
    static void AddBell(double f, double amp, double dec, double pan, double delay = 0, double glide = 0)
    {
        int slot = 0; double oldest = -1;
        for (int i = 0; i < bells.Length; i++)
        {
            if (!bells[i].On) { slot = i; oldest = double.MaxValue; break; }
            if (bells[i].Age > oldest) { oldest = bells[i].Age; slot = i; }
        }
        bells[slot] = new Bell { F = f, Amp = amp, Dec = dec, Pan = pan, Delay = delay, Glide = glide, On = true };
    }
    static readonly double[] scale = { 440.00, 493.88, 587.33, 659.25, 739.99, 880.00, 987.77, 1174.66 };  // D-Dur-Pentatonik
    static double noteTimer = 2.0; static int noteIdx = 3;

    // ---- Flaechenakkord: wandert von Dadd9 ueber Moll und vermindert zu einem tiefen Cluster ----
    static readonly double[][] chords =
    {
        new[] { 146.83, 220.00, 369.99, 329.63 },
        new[] { 146.83, 220.00, 349.23, 329.63 },
        new[] { 146.83, 207.65, 349.23, 311.13 },
        new[] { 138.59, 146.83, 207.65, 220.00 },
    };
    static readonly double[] chordPos = { 0, 0.4, 0.7, 1.0 };
    static readonly double[] padF = new double[4], padPh = new double[4], padPh2 = new double[4], padLfo = { 0, 0.3, 0.55, 0.8 };
    static readonly double[] padPan = { -0.5, 0.5, -0.25, 0.25 }, padRate = { 0.05, 0.07, 0.09, 0.11 };

    // ---- Zustandsfilter (Bandpass) ----
    struct Svf { public double Low, Band; public double Run(double x, double f, double q) { Low += f * Band; double hi = x - Low - q * Band; Band += f * hi; return Band; } }
    static double Fc(double hz) => 2 * Math.Sin(Math.PI * Math.Min(hz, SR / 7.0) / SR);
    static Svf windL, windR, breathF, wh1, wh2, jumpF;

    // ---- laufende Zustaende ----
    static double subPh, subPh2, rumble, windLfo, wowPh, hbT = 1, breathPh, tinPh, tinPh2;
    static double whT, whDur, whNext = 12, whPan, whF1 = 500, whF2 = 1700, whSyl, whSeg, whGate = 1;
    static double grAge = 99, grDur = 4, grNext = 20, grF = 90, grPh, grPhM, grPan;
    static double thunderAge = 99, thLpL, thLpR, touchAge = 99, clickAge = 99, swellAge = 99, jumpAge = 99, duck = 1, silenceT, silenceNext = 60;
    static double thumpAge = 99, thumpAmp;
    struct StepV { public double Age, Gain, Pan, Lp; }
    static readonly StepV[] steps = new StepV[4];

    // ---- Hall: Feedback-Delay-Netz mit 4 Leitungen ----
    static readonly int[] dLen = { 3001, 3613, 4217, 4799 };
    static readonly float[][] dBuf = { new float[3001], new float[3613], new float[4217], new float[4799] };
    static readonly int[] dPos = new int[4]; static readonly double[] dLp = new double[4];

    // ---- Ereignisse aus dem Spiel ----
    public static void Step(float pan, float gain)
    {
        int s = 0; for (int i = 1; i < steps.Length; i++) if (steps[i].Age > steps[s].Age) s = i;
        steps[s] = new StepV { Age = 0, Gain = gain, Pan = pan };
    }
    public static void Land(float strength) { thumpAge = 0; thumpAmp = strength; }
    public static void Jump() { jumpAge = 0; }
    public static void Flash() { thunderAge = 0; }
    public static void Touch() { touchAge = 0; }
    public static void LampClick() { clickAge = 0; }
    public static void Respawn() { swellAge = 0; }
    /// <summary>Checkpoint erreicht: im Traum ein reiner Dreiklang, im Terror derselbe Klang - verstimmt und absackend.</summary>
    public static void Chime()
    {
        double t = T;
        if (t < 0.5) { AddBell(1174.66, 0.10, 2.6, -0.3); AddBell(1479.98, 0.08, 2.6, 0.3, 0.10); AddBell(1760.00, 0.07, 3.0, 0.0, 0.20); }
        else { AddBell(1174.66, 0.09, 3.0, -0.3, 0, 0.01 * t); AddBell(1244.51, 0.07, 3.0, 0.3, 0.14, 0.02 * t); AddBell(830.61, 0.07, 3.6, 0.0, 0.30, 0.03 * t); }
    }

    public static void Fill()
    {
        double t = T, dtBlock = (double)BL / SR; ExtraBlock(dtBlock);
        // ---- Blockweise: Akkord, Spieluhr, Ereignis-Zeitgeber ----
        int seg = 0; while (seg < 2 && t > chordPos[seg + 1]) seg++;
        double ct = Math.Clamp((t - chordPos[seg]) / (chordPos[seg + 1] - chordPos[seg]), 0, 1);
        for (int i = 0; i < 4; i++) padF[i] = chords[seg][i] * Math.Pow(chords[seg + 1][i] / chords[seg][i], ct);

        if (musicT > 0) noteTimer -= dtBlock;   // Musik nur noch als seltenes Ereignis
        if (musicT > 0 && noteTimer <= 0)
        {
            int[] moves = { -2, -1, -1, 1, 1, 2 };
            noteIdx += moves[rng.Next(moves.Length)];
            if (noteIdx < 0) noteIdx = 1; if (noteIdx > 7) noteIdx = 6;
            double f = scale[noteIdx];
            if (Rnd() < SStep(0.3, 0.9, t) * 0.55) f *= Rnd() < 0.5 ? 0.943874 : 0.707107;   // falscher Ton: Halbton bzw. Tritonus
            f *= Math.Pow(2, (Rnd() - 0.5) * 2 * t * 35 / 1200.0);                          // verstimmt
            if (Rnd() < 0.35 * t) f *= 0.5;
            AddBell(f, 0.10 * (1 - 0.35 * t), Lerp(1.0, 2.4, t), (Rnd() - 0.5) * 1.2, 0, t > 0.5 ? 0.02 * t * Rnd() : 0);
            if (t < 0.85) noteTimer = Rnd() < 0.3 * (1 - t) ? 0.22 : Lerp(0.5, 2.6, t) * (0.55 + 0.9 * Rnd());
            else noteTimer = 1.6 + 3.5 * Rnd();                                                  // Terror: eine einsame, sterbende Spieluhr
        }
        if (t > 0.6 && whT <= 0) { whNext -= dtBlock; if (whNext <= 0) { whDur = whT = 1.2 + 1.6 * Rnd(); whPan = Rnd() < 0.5 ? -0.85 : 0.85; whNext = Lerp(22, 7, SStep(0.6, 1, t)) * (0.6 + 0.8 * Rnd()); } }
        if (t > 0.3 && grAge > grDur) { grNext -= dtBlock; if (grNext <= 0) { grAge = 0; grDur = 3 + 2.5 * Rnd(); grF = 60 + 80 * Rnd(); grPan = (Rnd() - 0.5) * 1.6; grNext = Lerp(45, 14, t) * (0.6 + 0.8 * Rnd()); } }
        // ploetzliche Stille (nur Herzschlag bleibt)
        if (t > 0.6) { silenceNext -= dtBlock; if (silenceNext <= 0) { silenceT = 2.5 + 2.5 * Rnd(); silenceNext = 40 + 50 * Rnd(); } }
        if (silenceT > 0) silenceT -= dtBlock;

        double padGain = Lerp(0.050, 0.030, t) * musicGate, subGain = 0.13 * SStep(0.2, 1, t) * (0.25 + 0.75 * Tension), rumGain = 3.2 * t * t * (0.3 + 0.7 * Tension);
        double windGain = Lerp(0.10, 0.16, t) * amb[0], breathGain = 0.5 * SStep(0.5, 0.9, t), whGain = 0.30 * SStep(0.6, 1, t);
        double hbRate = 0.85 + 0.45 * t + 0.9 * Prox + (tachyT > 0 ? 1.3 : 0), hbGain = 0.42 * SStep(0.22, 0.7, t) * (0.4 + 0.6 * Tension) + 0.5 * Prox + (tachyT > 0 ? 0.4 : 0);
        double subF = Lerp(36.7, 30.9, t), fbk = 0.80 + 0.13 * Size + 0.03 * t;
        double duckTarget = (silenceT > 0 ? 0.04 : 1.0) * (touchAge > 0.9 && touchAge < 2.2 ? 0.0 : 1.0);
        double wowDepth = 0.012 * t;

        for (int n = 0; n < BL; n++)
        {
            double wL = 0, wR = 0, pL = 0, pR = 0, wet = 0;   // Welt-Bus, Spieler-Bus, Hall-Send
            duck += (duckTarget - duck) * 0.0004;
            wowPh += 0.37 * Dt; double wow = 1 + Math.Sin(Tau * wowPh) * wowDepth;

            // Flaeche
            for (int i = 0; i < 4; i++)
            {
                padPh[i] += padF[i] * wow * Dt; padPh2[i] += padF[i] * 1.004 * Dt; padLfo[i] += padRate[i] * Dt;
                double s = (Math.Sin(Tau * padPh[i]) + 0.5 * Math.Sin(Tau * padPh2[i]) + 0.18 * Math.Sin(Tau * 2 * padPh[i]))
                           * (0.6 + 0.4 * Math.Sin(Tau * padLfo[i])) * padGain;
                wL += s * (0.5 - 0.5 * padPan[i]); wR += s * (0.5 + 0.5 * padPan[i]); wet += s * 0.3;
            }
            // Bass-Droehnen und Grollen
            subPh += subF * Dt; subPh2 += subF * 1.012 * Dt;
            rumble = rumble * 0.995 + White() * 0.005;
            double sub = (Math.Sin(Tau * subPh) + 0.6 * Math.Sin(Tau * subPh2)) * subGain + rumble * rumGain;
            wL += sub; wR += sub;
            // Wind
            windLfo += 0.05 * Dt;
            double wf = 520 + 330 * Math.Sin(Tau * windLfo) + 150 * Math.Sin(Tau * windLfo * 0.27);
            wL += windL.Run(White(), Fc(wf), 0.5) * windGain; wR += windR.Run(White(), Fc(wf * 1.13), 0.5) * windGain;
            // Spieluhr / Glocken
            for (int i = 0; i < bells.Length; i++)
            {
                if (!bells[i].On) continue;
                ref Bell b = ref bells[i];
                if (b.Delay > 0) { b.Delay -= Dt; continue; }
                b.Age += Dt; b.Ph += b.F * (1 - b.Glide * b.Age) * wow * Dt;
                double env = Math.Exp(-b.Age / b.Dec) * Math.Min(1, b.Age * 400);
                double s = (Math.Sin(Tau * b.Ph) + 0.28 * Math.Sin(Tau * b.Ph * 2.756) * Math.Exp(-b.Age * 6) + 0.10 * Math.Sin(Tau * b.Ph * 5.404) * Math.Exp(-b.Age * 12)) * env * b.Amp;
                wL += s * (0.5 - 0.5 * b.Pan); wR += s * (0.5 + 0.5 * b.Pan); wet += s * 0.55;
                if (env < 0.0004 && b.Age > 0.05) b.On = false;
            }
            // Atmen
            if (breathGain > 0.001)
            {
                breathPh += (0.24 + 0.25 * Prox) * Dt; double x = breathPh - Math.Floor(breathPh);
                double e = x < 0.38 ? Math.Pow(Math.Sin(Math.PI * x / 0.38), 2) : (x > 0.45 && x < 0.9 ? 0.8 * Math.Pow(Math.Sin(Math.PI * (x - 0.45) / 0.45), 2) : 0);
                double s = breathF.Run(White(), Fc(x < 0.42 ? 950 : 560), 0.6) * e * breathGain;
                wL += s * 0.8; wR += s * 0.6;
            }
            // Fluestern direkt an einem Ohr
            if (whT > 0)
            {
                whT -= Dt; whSeg -= Dt; whSyl += 5.5 * Dt;
                if (whSeg <= 0) { whSeg = 0.11; whF1 = 300 + 500 * Rnd(); whF2 = 1100 + 1500 * Rnd(); whGate = Rnd() < 0.8 ? 1 : 0; }
                double nz = White();
                double s = (wh1.Run(nz, Fc(whF1), 0.14) + 0.7 * wh2.Run(nz, Fc(whF2), 0.12)) * Math.Pow(Math.Max(0, Math.Sin(Tau * whSyl)), 0.6) * whGate
                           * Math.Min(1, Math.Min(whT, whDur - whT) / 0.15) * whGain;
                wL += s * (0.5 - 0.5 * whPan); wR += s * (0.5 + 0.5 * whPan); wet += s * 0.15;
            }
            // metallisches Aechzen in der Ferne
            if (grAge <= grDur)
            {
                grAge += Dt; double k = grAge / grDur, f = grF * (1 - 0.4 * k);
                grPhM += f * 1.41 * Dt; grPh += f * Dt;
                double s = Math.Sin(Tau * grPh + (3.5 - 3 * k) * Math.Sin(Tau * grPhM)) * Math.Min(1, grAge / 0.8) * Math.Min(1, (grDur - grAge) / 1.2) * 0.07;
                wL += s * (0.5 - 0.5 * grPan); wR += s * (0.5 + 0.5 * grPan); wet += s * 0.8;
            }
            // Pfeifen im Ohr, wenn der Verfolger nah ist
            if (Prox > 0.01) { tinPh += 6100 * Dt; tinPh2 += 6163 * Dt; double s = (Math.Sin(Tau * tinPh) + Math.Sin(Tau * tinPh2)) * Prox * Prox * 0.03; wL += s; wR += s; }

            // ---- Spieler-Bus: wird von der Stille nicht gedaempft ----
            hbT += Dt; if (hbT * hbRate >= 1) hbT = 0;
            double he = Math.Exp(-hbT * 26) + (hbT > 0.24 ? 0.7 * Math.Exp(-(hbT - 0.24) * 26) : 0);
            double hb = Math.Sin(Tau * 48 * hbT) * he * hbGain; pL += hb; pR += hb;
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i].Age > 0.3) continue;
                ref StepV sv = ref steps[i]; sv.Age += Dt;
                sv.Lp += 0.22 * (White() - sv.Lp);
                double s = (sv.Lp * Math.Exp(-sv.Age * 70) * 1.6 + Math.Sin(Tau * 95 * sv.Age) * Math.Exp(-sv.Age * 45) * 0.5) * sv.Gain;
                pL += s * (0.5 - 0.5 * sv.Pan); pR += s * (0.5 + 0.5 * sv.Pan); wet += s * 0.25;
            }
            if (thumpAge < 0.6) { thumpAge += Dt; double s = Math.Sin(Tau * 62 * thumpAge) * Math.Exp(-thumpAge * 24) * thumpAmp * 0.7; pL += s; pR += s; wet += s * 0.2; }
            if (jumpAge < 0.2) { jumpAge += Dt; double s = jumpF.Run(White(), Fc(400 + 4000 * jumpAge), 0.7) * Math.Sin(Math.PI * jumpAge / 0.2) * 0.05; pL += s; pR += s; }
            if (clickAge < 0.5) { clickAge += Dt; double s = White() * Math.Exp(-clickAge * 300) * 0.5 + Math.Sin(Tau * 50 * clickAge) * Math.Exp(-clickAge * 9) * 0.10; pL += s; pR += s; }
            if (swellAge < 0.7) { swellAge += Dt; double s = White() * Math.Pow(swellAge / 0.7, 3) * (swellAge > 0.66 ? 0 : 1) * 0.16; pL += s; pR -= s; wet += s * 0.5; }
            if (thunderAge < 4.5)
            {   // Blitz: scharfer Knall, dann langes Grollen
                thunderAge += Dt; double a = Lerp(0.6, 0.012, Math.Min(1, thunderAge / 1.4));
                thLpL += a * (White() - thLpL); thLpR += a * (White() - thLpR);
                double env = thunderAge < 0.02 ? thunderAge / 0.02 : Math.Exp(-(thunderAge - 0.02) * 1.0);
                double cr = thunderAge < 0.15 ? White() * Math.Exp(-thunderAge * 28) * 0.7 : 0;
                pL += (thLpL * 2.6 * env + cr) * 0.55; pR += (thLpR * 2.6 * env + cr) * 0.55; wet += (thLpL + thLpR) * env * 0.5;
            }
            if (touchAge < 0.9)
            {   // Beruehrung durch den Verfolger: ein kurzer, haesslicher Schrei aus Rauschen und Dissonanz
                touchAge += Dt; double env = Math.Min(1, touchAge * 200) * Math.Exp(-touchAge * 2.2), tr = 0.6 + 0.4 * Math.Sin(Tau * 23 * touchAge);
                double s = (White() * 0.7 + Math.Sin(Tau * 311 * touchAge) + Math.Sin(Tau * 329.6 * touchAge) + Math.Sin(Tau * 466 * touchAge * (1 + 0.3 * touchAge))) * env * tr * 0.22;
                pL += s; pR += s; wet += s * 0.6;
            }
            else if (touchAge < 3) touchAge += Dt;

            ExtraSample(ref wL, ref wR, ref pL, ref pR, ref wet);
            // ---- Hall ----
            double y0 = dBuf[0][dPos[0]], y1 = dBuf[1][dPos[1]], y2 = dBuf[2][dPos[2]], y3 = dBuf[3][dPos[3]];
            dLp[0] += 0.45 * (y0 - dLp[0]); dLp[1] += 0.45 * (y1 - dLp[1]); dLp[2] += 0.45 * (y2 - dLp[2]); dLp[3] += 0.45 * (y3 - dLp[3]);
            double hs = 0.5 * (dLp[0] + dLp[1] + dLp[2] + dLp[3]);
            dBuf[0][dPos[0]] = (float)((dLp[0] - hs) * fbk + wet); dBuf[1][dPos[1]] = (float)((dLp[1] - hs) * fbk + wet);
            dBuf[2][dPos[2]] = (float)((dLp[2] - hs) * fbk + wet); dBuf[3][dPos[3]] = (float)((dLp[3] - hs) * fbk + wet);
            for (int i = 0; i < 4; i++) if (++dPos[i] >= dLen[i]) dPos[i] = 0;
            double rvL = (y0 + y2) * 0.5, rvR = (y1 + y3) * 0.5;

            double oL = ((wL + rvL) * duck + pL) * 1.5 * Vol, oR = ((wR + rvR) * duck + pR) * 1.5 * Vol;
            Buf[n * 2] = (short)(Math.Tanh(oL) * 30000); Buf[n * 2 + 1] = (short)(Math.Tanh(oR) * 30000);
        }
        BeatEnv = (float)((Math.Exp(-hbT * 9) + (hbT > 0.24 ? 0.7 * Math.Exp(-(hbT - 0.24) * 9) : 0)) * Math.Min(1, hbGain * 2));
    }
}
