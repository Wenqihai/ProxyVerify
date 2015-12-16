namespace Proxy.Service
{
    using System;
    using System.IO;

    public class Log
    {
        public static void WriteLog(string msg)
        {
            File.AppendAllLines(@"..\log.txt", new string[] { DateTime.Now.ToShortTimeString() + ":" + msg + "\t" });
        }
    }
}

