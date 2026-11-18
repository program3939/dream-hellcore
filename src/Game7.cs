// Dreamy Hellcore - versteckte Konsole: Strg+Umschalt+O oeffnet oben links eine Eingabezeile.
// Befehl "lvlchoice" fragt nach einer Hellstage (1 bis 999 999 999 999) und springt dorthin;
// "lvlchoice 100000" geht auch in einer Zeile. Esc schliesst die Zeile. Solange sie offen ist, steht das Spiel.
// Absichtlich nicht im README erwaehnt (versteckt fuer Spieler).
using System;
using System.Globalization;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace DreamyHellcore;

static unsafe partial class Game
{
    static bool chatOpen; static string chatText = "", chatMsg = ""; static int chatMode; static float chatMsgT, chatBlink; static long chatLoad;
    const long MaxStage = 999_999_999_999;

    static bool TryStage(string s, out long n)
    {
        s = s.Replace(".", "").Replace(",", "").Replace(" ", "").Replace("_", "");
        return long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out n) && n >= 1 && n <= MaxStage;
    }
    static void ChatMsg(string m) { chatMsg = m; chatMsgT = 3f; }
    static void ChatLeft(string s, float x, float y, float size, Color c)
    {
        s = Ui(s); float sp = size * 0.04f;
        DrawTextEx(uiFont, s, new Vector2(x + size * 0.06f, y + size * 0.06f), size, sp, new Color((byte)0, (byte)0, (byte)0, (byte)(c.A * 3 / 4)));
        DrawTextEx(uiFont, s, new Vector2(x, y), size, sp, c);
    }

    static void ChatSubmit()
    {
        string t = chatText.Trim(); chatText = "";
        if (chatMode == 0)
        {
            string low = t.ToLowerInvariant();
            if (low == "lvlchoice") { chatMode = 1; return; }
            if (low.StartsWith("lvlchoice ") && TryStage(low.Substring(10), out long n1)) { chatLoad = n1; chatOpen = false; ChatMsg("Hellstage " + n1.ToString("N0", CultureInfo.InvariantCulture)); return; }
            ChatMsg(t.Length == 0 ? "" : "Unknown command.");
            return;
        }
        if (TryStage(t, out long n)) { chatLoad = n; chatOpen = false; ChatMsg("Hellstage " + n.ToString("N0", CultureInfo.InvariantCulture)); }
        else ChatMsg("Enter a number from 1 to 999,999,999,999.");
    }

    /// <summary>Wird in DrawUi jedes Bild aufgerufen: oeffnen, tippen, absenden, zeichnen.</summary>
    static void UpdateChat(float u, float dt)
    {
        bool ctrl = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl);
        bool shift = IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift);
        chatMsgT -= dt; chatBlink += dt;
        float x = 24 * u, y = 70 * u, w = 560 * u, h = 40 * u, fs = 22 * u;
        if (!chatOpen)
        {
            if (ctrl && shift && IsKeyPressed(KeyboardKey.O)) { chatOpen = true; chatText = ""; chatMode = 0; chatMsgT = 0; while (GetCharPressed() != 0) { } }
            else
            {
                if (chatMsgT > 0 && chatMsg.Length > 0) ChatLeft(chatMsg, x, y, fs, Ca(240, 226, 200, MathF.Min(1, chatMsgT)));
                return;
            }
        }
        int ch;
        while ((ch = GetCharPressed()) != 0) if (ch >= 32 && ch < 127 && chatText.Length < 40) chatText += (char)ch;
        if (IsKeyPressed(KeyboardKey.Backspace) && chatText.Length > 0) chatText = chatText.Substring(0, chatText.Length - 1);
        if (IsKeyPressed(KeyboardKey.Escape)) { chatOpen = false; return; }
        if (IsKeyPressed(KeyboardKey.Enter) || IsKeyPressed(KeyboardKey.KpEnter)) ChatSubmit();
        if (!chatOpen) return;

        ChatLeft(chatMode == 0 ? "Command:" : "Enter Hellstage (1 - 999,999,999,999):", x, y - 30 * u, 18 * u, Ca(215, 205, 195, 0.9f));
        DrawRectangleRec(new Rectangle(x, y, w, h), Ca(10, 8, 12, 0.82f));
        DrawRectangleLinesEx(new Rectangle(x, y, w, h), 1.5f, Ca(200, 150, 110, 0.9f));
        string caret = chatBlink % 1f < 0.55f ? "_" : " ";
        ChatLeft("> " + chatText + caret, x + 10 * u, y + (h - fs) / 2 - 2 * u, fs, Ca(240, 232, 220, 1f));
        if (chatMsgT > 0 && chatMsg.Length > 0) ChatLeft(chatMsg, x, y + h + 8 * u, 18 * u, Ca(240, 190, 160, MathF.Min(1, chatMsgT)));
    }
}
