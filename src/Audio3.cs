// Dreamy Hellcore - Klaenge der Escape-Raeume: Chor des falschen Himmels, der Ton des Beobachters, das Grollen der
// nahenden Dunkelheit, Siegel-Toene und das sich oeffnende Tor.
using System;

namespace DreamyHellcore;

static partial class Audio
{
    public static float Heaven, Watch, Front;
    static double hvS, wtS, frS, frLp, goAge = 99, hvDriftT = 12, hvDriftAge = 99; static int hvDriftV;
    static readonly double[] hvPh = new double[5], goPh = new double[5], wtPh = new double[3];
    static readonly double[] hvF = { 293.66, 369.99, 440.00, 587.33, 739.99 }, goF = { 146.83, 220.00, 293.66, 369.99, 587.33 };
    static Svf hvS1, hvS2;

    public static void Tone(double f) { AddBell(f, 0.10, 0.9, 0, 0); }
    public static void GateOpen()
    {
        goAge = 0; musicT = Math.Max(musicT, 10);
        for (int i = 0; i < 5; i++) AddBell(587.33 * Math.Pow(2, new[] { 0, 4, 7, 12, 16 }[i] / 12.0), 0.09, 3.2, (i - 2) * 0.3, i * 0.13);
    }
    /// <summary>Aufsteigender Ton je Siegel/Platte (Dur-Pentatonik) - der Spieler hoert, wie weit er ist.</summary>
    public static void SealTone(int n)
    {
        double f = 587.33 * Math.Pow(2, new[] { 0, 2, 4, 7, 9, 12, 14 }[Math.Clamp(n - 1, 0, 6)] / 12.0);
        AddBell(f, 0.11, 2.4, 0, 0); AddBell(f * 2, 0.05, 1.6, 0.3, 0.05);
    }

    static void Extra3(ref double wL, ref double wR, ref double pL, ref double pR, ref double wet)
    {
        hvS += (Heaven - hvS) * 0.00006; wtS += (Watch - wtS) * 0.0008; frS += (Front - frS) * 0.0006;
        if (hvS > 0.004)
        {   // Chor des falschen Himmels: ein reiner D-Dur-Klang - in dem immer wieder eine Stimme einen Viertelton absackt
            hvDriftT -= Dt; hvDriftAge += Dt;
            if (hvDriftT <= 0) { hvDriftT = 8 + 10 * Rnd(); hvDriftAge = 0; hvDriftV = rng.Next(5); }
            double v = 0;
            for (int i = 0; i < 5; i++)
            {
                double det = i == hvDriftV && hvDriftAge < 4 ? 1 - 0.028 * Math.Sin(Math.PI * hvDriftAge / 4) : 1;
                hvPh[i] += hvF[i] * det * (1 + 0.003 * Math.Sin(Tau * (4.2 + 0.4 * i) * hvPh[i] / hvF[i])) * Dt;
                double x = hvPh[i] - Math.Floor(hvPh[i]); v += 2 * x - 1;
            }
            double s = (hvS1.Run(v, Fc(650), 0.3) + 0.7 * hvS2.Run(v, Fc(1080), 0.22)) * 0.011 * hvS;
            wL += s; wR += s; wet += s * 1.4;
        }
        if (wtS > 0.004)
        {   // der Beobachter oeffnet die Augen: kleine Sekunde, die anschwillt und flattert
            wtPh[0] += 233.08 * Dt; wtPh[1] += 246.94 * Dt; wtPh[2] += 1864.66 * Dt;
            double s = (Math.Sin(Tau * wtPh[0]) + Math.Sin(Tau * wtPh[1]) + 0.25 * Math.Sin(Tau * wtPh[2]) * (0.5 + 0.5 * Math.Sin(Tau * 9 * wtPh[0] / 233.08)))
                       * 0.075 * wtS * wtS;
            pL += s; pR += s; wet += s * 0.6;
        }
        if (frS > 0.004)
        {   // die Dunkelheit kommt naeher: tiefes Grollen
            frLp += 0.012 * (White() - frLp);
            double s = (frLp * 9 + Math.Sin(Tau * 38 * goPh[0]) * 0.0) * frS * frS * 0.55;
            pL += s; pR += s * 0.9;
        }
        if (goAge < 5)
        {   // das Tor oeffnet sich: ein weiter D-Dur-Akkord
            goAge += Dt; double env = Math.Sin(Math.PI * goAge / 5); env *= env; double s = 0;
            for (int i = 0; i < 5; i++) { goPh[i] += goF[i] * Dt; s += Math.Sin(Tau * goPh[i]); }
            s *= 0.035 * env; wL += s; wR += s; wet += s * 0.8;
        }
    }
}
