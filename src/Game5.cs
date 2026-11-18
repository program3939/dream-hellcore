// Dreamy Hellcore - Bildschirmanzeige: "Hellstage N" oben mittig, Hinweise in Escape-Raeumen, Pausenmenue (Esc).
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace DreamyHellcore;

static unsafe partial class Game
{
    static Font uiFont; static bool uiFontOk, menuReset, menuQuit, argMenu; static float resetArm;

    /// <summary>Systemschrift mit Umlauten laden (Windows: Segoe UI, Linux: DejaVu Sans); sonst raylib-Standardschrift.</summary>
    static void LoadUiFont()
    {
        var cps = new List<int>(); for (int c = 32; c < 127; c++) cps.Add(c); cps.AddRange(new[] { 196, 214, 220, 228, 246, 252, 223 });
        foreach (string p in new[] { "C:/Windows/Fonts/segoeui.ttf", "C:/Windows/Fonts/arial.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                                     "/usr/share/fonts/TTF/DejaVuSans.ttf", "/usr/share/fonts/dejavu/DejaVuSans.ttf" })
            if (File.Exists(p))
            {
                uiFont = LoadFontEx(p, 72, cps.ToArray(), cps.Count);
                if (uiFont.Texture.Id != 0) { uiFontOk = true; SetTextureFilter(uiFont.Texture, TextureFilter.Bilinear); return; }
            }
        uiFont = GetFontDefault();
    }
    static string Ui(string s) => uiFontOk ? s : s.Replace("ü", "ue").Replace("ä", "ae").Replace("ö", "oe").Replace("ß", "ss");
    static void UiText(string s, float cx, float y, float size, Color c)
    {
        s = Ui(s); float sp = size * 0.04f; Vector2 m = MeasureTextEx(uiFont, s, size, sp);
        DrawTextEx(uiFont, s, new Vector2(cx - m.X / 2 + size * 0.06f, y + size * 0.06f), size, sp, new Color((byte)0, (byte)0, (byte)0, (byte)(c.A * 3 / 4)));
        DrawTextEx(uiFont, s, new Vector2(cx - m.X / 2, y), size, sp, c);
    }
    static bool UiButton(string label, float cx, float y, float w, float h, float size)
    {
        var r = new Rectangle(cx - w / 2, y, w, h); bool hover = CheckCollisionPointRec(GetMousePosition(), r);
        DrawRectangleRec(r, hover ? new Color((byte)70, (byte)14, (byte)12, (byte)215) : new Color((byte)14, (byte)12, (byte)16, (byte)200));
        DrawRectangleLinesEx(r, 1.5f, hover ? new Color((byte)235, (byte)175, (byte)130, (byte)255) : new Color((byte)110, (byte)100, (byte)100, (byte)210));
        UiText(label, cx, y + (h - size) / 2 - size * 0.05f, size, new Color((byte)236, (byte)226, (byte)214, (byte)255));
        return hover && IsMouseButtonPressed(MouseButton.Left);
    }
    static Color Ca(int r, int g, int b, float a) => new Color((byte)r, (byte)g, (byte)b, (byte)Math.Clamp((int)(a * 255), 0, 255));

    /// <summary>Wird am Ende jedes Bildes zwischen BeginDrawing und EndDrawing aufgerufen.</summary>
    static void DrawUi(long st, ref bool paused, float dt, Camera3D cam)
    {
        float W = GetScreenWidth(), H = GetScreenHeight(), u = H / 720f;
        // ---- Anzeige der aktuellen Hellstage, oben mittig ----
        UiText("Hellstage " + st, W / 2, 16 * u, 30 * u, new Color((byte)232, (byte)224, (byte)214, (byte)215));
        UpdateChat(u, dt);   // versteckte Konsole (Strg+Umschalt+O), siehe Game7.cs

        // ---- Hinweise im Escape-Raum: Titel und Regel unten, Markierung der naechsten richtigen Platte ----
        Room r = cur;
        if (r != null && r.Escape > 0 && !r.Open && !paused)
        {
            roomT += dt;
            bool inCh = pos.Z > r.AltarZ - 3f, puzzle = r.Escape >= 11;
            if (inCh) chamberT += dt;
            float vis = puzzle ? (inCh ? MathF.Min(1, chamberT * 1.5f) : 0) : MathF.Max(inCh ? 1 : 0, M.C01((14f - roomT) / 2f)) * MathF.Min(1, roomT * 1.5f);
            if (vis > 0.01f)
            {
                float y0 = H - 124 * u;
                UiText(PuzTitle[r.Escape], W / 2, y0, 30 * u, Ca(240, 226, 200, 0.95f * vis));
                UiText(PuzRule[r.Escape], W / 2, y0 + 40 * u, 20 * u, Ca(225, 215, 205, 0.9f * vis));
                UiText(puzzle ? "H: show next step      Esc: menu / skip" : "Esc: menu / skip", W / 2, y0 + 72 * u, 16 * u, Ca(190, 180, 170, 0.75f * vis));
            }
            if (puzzle && inCh)
            {
                if ((!chatOpen && IsKeyPressed(KeyboardKey.H)) || chamberT > 75f || pzTries >= 4) hintOn = true;
                if (hintOn)
                {
                    if (hintDirty) { hintPlate = ComputeHint(r); hintDirty = false; }
                    if (hintPlate >= 0 && hintPlate < r.Plates.Count)
                    {
                        Vector3 p = r.Plates[hintPlate] + new Vector3(0, 0.06f, 0);
                        if (Vector3.Dot(cam.Target - cam.Position, p - cam.Position) > 0)
                        {
                            Vector2 sp = GetWorldToScreen(p, cam);
                            float pr = (24 + 5 * MathF.Sin((float)GetTime() * 5)) * u;
                            DrawRing(sp, pr, pr + 4 * u, 0, 360, 48, Ca(255, 226, 160, 0.9f));
                            DrawRing(sp, pr + 10 * u, pr + 12 * u, 0, 360, 48, Ca(255, 226, 160, 0.35f));
                            DrawCircleV(sp, 5 * u, Ca(255, 240, 210, 0.95f));
                        }
                    }
                }
            }
        }

        if (!paused) { resetArm = 0; return; }
        // ---- Pausenmenue ----
        resetArm -= dt;
        DrawRectangle(0, 0, (int)W, (int)H, new Color((byte)0, (byte)0, (byte)0, (byte)135));
        UiText("Paused", W / 2, H * 0.20f, 56 * u, new Color((byte)240, (byte)232, (byte)220, (byte)255));
        float bw = 420 * u, bh = 54 * u, fs = 26 * u, y = H * 0.36f, step = 72 * u;
        if (UiButton("Resume", W / 2, y, bw, bh, fs)) { paused = false; DisableCursor(); }
        y += step;
        if (r != null && r.Escape > 0 && !r.Open)
        {
            if (UiButton(r.Escape >= 11 ? "Skip this puzzle" : "Skip this trial", W / 2, y, bw, bh, fs)) menuSkip = true;
            y += step;
        }
        if (UiButton(resetArm > 0 ? "Are you sure? Click again to confirm" : "Reset progress", W / 2, y, bw, bh, fs))
        {
            if (resetArm > 0) { menuReset = true; resetArm = 0; } else resetArm = 3f;
        }
        y += step;
        if (UiButton("Quit game", W / 2, y, bw, bh, fs)) menuQuit = true;
        UiText(resetArm > 0 ? "Resetting starts over at Hellstage 1." : "Esc: resume", W / 2, y + 86 * u, 20 * u,
            new Color((byte)200, (byte)190, (byte)180, (byte)200));
    }
}
