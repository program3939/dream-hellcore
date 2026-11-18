// Dreamy Hellcore - Klang, Phase "nie repetitiv": keine Dauermusik mehr (Musik nur als Ereignis), Raumklang pro Raum,
// nichtlineare Schreie (Blumstein et al. 2012: Rauschen, Verzerrung, abrupte Frequenzspruenge - sparsam, jedes Mal anders),
// Stille als Mittel, Klaenge der neuen Mechaniken.
using System;

namespace DreamyHellcore;

static partial class Audio
{
    public static float Tension, RailSpeed;
    static double musicT, musicGate, tachyT;
    // Raumklang-Schichten: 0 Wind, 1 Brummen, 2 Tropfen, 3 ferne Maschine, 4 Knistern, 5 hoher Ton
    static readonly double[] amb = { 1, 0, 0, 0, 0, 0 }, ambTarget = { 1, 0, 0, 0, 0, 0 };
    static double humF = 50, dripRate = 0.4, machRate = 0.7, whineF = 3800, humPh, machPh, machLp, whinePh;
    static double scAge = 99, scDur, scF, scBase, scJump, scPh, scZ, scNoise, scPan, scGain;
    static double bgAge = 99, bgPan, bgLp, chAge = 99, chDur, drAge = 99, drPh, boAge = 99, boPh, crAge = 99, bzAge = 99, brAge = 99, railPh, railClack = 9;
    static readonly double[] chPh = new double[5], chFr = new double[5]; static Svf chS1, chS2, railF, brF;
    static int stepsLeft; static double stepsTimer, stepsPan;

    public static void Music(float sec) { musicT = Math.Max(musicT, sec); noteTimer = Math.Min(noteTimer, 0.6); }
    public static void Whisper() { if (whT <= 0) { whDur = whT = 1.2 + 1.6 * Rnd(); whPan = Rnd() < 0.5 ? -0.85 : 0.85; } }
    public static void Silence(float sec) { silenceT = Math.Max(silenceT, sec); }
    public static void Tachy(float sec) { tachyT = sec; }
    /// <summary>Nichtlinearer Schrei: chaotische Modulation, harte Verzerrung, Rauschen, Frequenzspruenge. Jedes Mal andere Parameter.</summary>
    public static void Scream(float len)
    {
        scAge = 0; scDur = (0.35 + 0.9 * Rnd()) * len; scBase = 500 + 900 * Rnd(); scF = scBase; scJump = 0;
        scNoise = 0.3 + 0.6 * Rnd(); scPan = (Rnd() - 0.5) * 1.4; scGain = 0.28 + 0.14 * Rnd();
    }
    public static void Bang(float pan) { bgAge = 0; bgPan = pan; }
    public static void Choir(float sec)
    {
        chAge = 0; chDur = sec; double b = 180 + 140 * Rnd();
        for (int i = 0; i < 5; i++) chFr[i] = b * Math.Pow(2, (i * (Rnd() < 0.5 ? 1 : 6) + (Rnd() - 0.5) * 0.6) / 12.0);   // Sekund- und Tritonus-Cluster
    }
    public static void Drop() { drAge = 0; }
    public static void Bounce() { boAge = 0; }
    public static void Crack() { crAge = 0; }
    public static void Buzz() { bzAge = 0; }
    public static void BreathNow() { brAge = 0; }
    public static void Steps(int n, float pan) { stepsLeft = n; stepsTimer = 0; stepsPan = pan; }
    public static void Chime(bool fake)
    {
        if (!fake) { Chime(); return; }
        AddBell(1174.66, 0.09, 1.2, -0.2, 0, 0.12); AddBell(1108.73, 0.08, 1.6, 0.2, 0.16, 0.2); AddBell(554.37, 0.07, 2.4, 0, 0.36, 0.25);
    }
    /// <summary>Jeder Raum klingt anders: zwei von sechs Schichten, eigene Tonhoehen und Takte, aus der Stage-Nummer.</summary>
    public static void AmbientSet(long stage, int theme, float mood)
    {
        var r = new Random((int)(stage * 7919 % 2000000011));
        for (int i = 0; i < 6; i++) ambTarget[i] = 0;
        if (theme == 8) return;                                   // Raumtyp "Leere": gar kein Raumklang
        int a = r.Next(6), b = r.Next(6);
        ambTarget[a] = 0.5 + 0.5 * r.NextDouble(); ambTarget[b] = Math.Max(ambTarget[b], 0.35 + 0.4 * r.NextDouble());
        if (theme == 10) ambTarget[0] = 1.7;                      // Sturmraum
        if (mood < 0.15) { ambTarget[1] = 0; ambTarget[3] = 0; ambTarget[5] = 0; ambTarget[0] = Math.Max(ambTarget[0], 0.5); }   // Traum: nur Luft, Tropfen, Knistern
        humF = new[] { 50.0, 60.0, 100.0, 43.0 }[r.Next(4)]; dripRate = 0.15 + 0.9 * r.NextDouble();
        machRate = 0.35 + 1.1 * r.NextDouble(); whineF = 2600 + 3400 * r.NextDouble();
    }

    static void ExtraBlock(double dt)
    {
        musicT -= dt; musicGate += ((musicT > 0 ? 1.0 : 0.0) - musicGate) * 0.03;
        tachyT -= dt;
        for (int i = 0; i < 6; i++) amb[i] += (ambTarget[i] - amb[i]) * 0.02;
        if (amb[2] > 0.02 && Rnd() < dripRate * dt) AddBell(900 + 1800 * Rnd(), 0.05 * amb[2], 0.10 + 0.1 * Rnd(), (Rnd() - 0.5) * 1.6, 0, -0.3);   // Tropfen
        if (stepsLeft > 0) { stepsTimer -= dt; if (stepsTimer <= 0) { Step((float)stepsPan, 0.75f); stepsLeft--; stepsTimer = 0.19 + 0.03 * Rnd(); } }
    }

    static void ExtraSample(ref double wL, ref double wR, ref double pL, ref double pR, ref double wet)
    {
        Extra3(ref wL, ref wR, ref pL, ref pR, ref wet);
        // ---- Raumklang ----
        if (amb[1] > 0.01) { humPh += humF * Dt; double s = (Math.Sin(Tau * humPh) + 0.4 * Math.Sin(Tau * 2 * humPh) + 0.2 * Math.Sin(Tau * 3.01 * humPh)) * 0.012 * amb[1]; wL += s; wR += s; }
        if (amb[3] > 0.01)
        {
            machPh += machRate * Dt; machLp += 0.02 * (White() - machLp);
            double s = machLp * Math.Pow(0.5 + 0.5 * Math.Sin(Tau * machPh), 6) * 1.4 * amb[3]; wL += s; wR += s * 0.7; wet += s * 0.4;
        }
        if (amb[4] > 0.01 && Rnd() < 0.0009 * amb[4]) { double c = (Rnd() - 0.5) * 0.5; wL += c * Rnd(); wR += c * Rnd(); }
        if (amb[5] > 0.01) { whinePh += whineF * Dt; double s = Math.Sin(Tau * whinePh) * (0.5 + 0.5 * Math.Sin(Tau * whinePh * 0.00021)) * 0.006 * amb[5]; wL += s; wR -= s * 0.6; }
        // ---- Schrei ----
        if (scAge < scDur)
        {
            scAge += Dt; scJump -= Dt;
            if (scJump <= 0) { scJump = 0.02 + 0.06 * Rnd(); scF = scBase * (0.5 + 1.5 * Rnd()) * (Rnd() < 0.25 ? 0.5 : 1); }   // abrupte Frequenzspruenge
            scPh += scF * Dt; scZ = Math.Sin(Tau * scPh * 1.618 + 3.0 * scZ);                                                    // chaotische Rueckkopplung
            double env = Math.Min(1, scAge * 120) * Math.Min(1, (scDur - scAge) * 14);
            double s = Math.Tanh(5.0 * (Math.Sin(Tau * scPh + 2.2 * scZ) + scNoise * White() * 2)) * env * scGain;
            pL += s * (0.5 - 0.5 * scPan); pR += s * (0.5 + 0.5 * scPan); wet += s * 0.5;
        }
        if (bgAge < 1.4)
        {   // dumpfer Schlag irgendwo hinter dem Spieler
            bgAge += Dt; bgLp += 0.06 * (White() - bgLp);
            double s = (bgLp * 5 * Math.Exp(-bgAge * 9) + Math.Sin(Tau * 52 * bgAge) * Math.Exp(-bgAge * 6)) * 0.55;
            pL += s * (0.5 - 0.5 * bgPan); pR += s * (0.5 + 0.5 * bgPan); wet += s * 0.7;
        }
        if (chAge < chDur)
        {   // ferner "Chor": verstimmte Saegezaehne durch zwei Formanten, schwillt an und ab
            chAge += Dt; double env = Math.Sin(Math.PI * chAge / chDur); env *= env; double v = 0;
            for (int i = 0; i < 5; i++) { chPh[i] += chFr[i] * (1 + 0.004 * Math.Sin(Tau * (4.5 + i) * chAge)) * Dt; double x = chPh[i] - Math.Floor(chPh[i]); v += 2 * x - 1; }
            double s = (chS1.Run(v, Fc(720), 0.25) + 0.6 * chS2.Run(v, Fc(1150), 0.2)) * env * 0.018; wL += s; wR += s; wet += s * 1.2;
        }
        if (drAge < 1.3) { drAge += Dt; drPh += (180 - 110 * drAge) * Dt; double s = Math.Sin(Tau * drPh) * Math.Sin(Math.PI * drAge / 1.3) * 0.30; pL += s; pR += s; }
        if (boAge < 0.22) { boAge += Dt; boPh += (120 + 1400 * boAge) * Dt; double s = Math.Sin(Tau * boPh) * (1 - boAge / 0.22) * 0.25; pL += s; pR += s; }
        if (crAge < 0.35) { crAge += Dt; double s = White() * Math.Exp(-crAge * 40) * 0.6 + Math.Sin(Tau * 80 * crAge) * Math.Exp(-crAge * 14) * 0.3; pL += s; pR += s; wet += s * 0.3; }
        if (bzAge < 0.32) { bzAge += Dt; double s = (Math.Sin(Tau * 90 * bzAge) > 0 ? 1 : -1) * 0.10 * (1 - bzAge / 0.32); pL += s; pR += s; }
        if (brAge < 2.2) { brAge += Dt; double e = Math.Pow(Math.Sin(Math.PI * brAge / 2.2), 2); double s = brF.Run(White(), Fc(brAge < 1 ? 900 : 520), 0.5) * e * 0.9; pL += s * 0.3; pR += s; }
        if (RailSpeed > 0.5)
        {   // Schienenfahrt: Fahrtwind und das Klacken der Schwellen, beides mit dem Tempo
            double v = RailSpeed, s = railF.Run(White(), Fc(250 + 60 * v), 0.4) * 0.035 * v;
            railPh += v / 2.0 * Dt; if (railPh >= 1) { railPh -= 1; railClack = 0; }
            railClack += Dt; s += White() * Math.Exp(-railClack * 120) * 0.35;
            pL += s; pR += s; wet += s * 0.2;
        }
    }
}
