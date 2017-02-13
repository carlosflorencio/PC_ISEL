/**
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Reference code for SE#3, winter 2016/17 
 *
 *  Pedro Félix, December 2016
 *
 **/

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ClientTcp
{
    class ClientProgram
    {
        private const int NOfClients = 100;
        
        static void Main(string[] args)
        {            
            var ths = Enumerable.Range(0, NOfClients).Select(i =>
            {
                var th = new Thread(() => new Client(i).Run());
                th.Start();
                return th;
            }).ToArray();
            Console.ReadKey();
            Client.Cancel();
            foreach(var th in ths)
            {
                th.Join();
            }
            Console.WriteLine("Ready to leave");
            Console.ReadKey();
        }
    }

    class Client
    {        
        private volatile static bool isCancelled = false;

        private readonly int _id;
        private readonly int _key;

        private const string RemoteIp = "127.0.0.1";
        private const int RemotePort = 8888;
        private readonly Random _rnd;

        public Client(int id)
        {
            _id = id;
            _key = id % 2;
            _rnd = new Random(id);
        }

        public void Run()
        {
            using (var client = new TcpClient())
            {
                client.Connect(IPAddress.Parse(RemoteIp), RemotePort);
                Log("Is connected");
                var stream = client.GetStream();
                using (var wr = new StreamWriter(stream))
                using (var rd = new StreamReader(stream))
                {
                    try
                    {
                        for (int counter = 0; !isCancelled; ++counter)
                        {
                            wr.Send($"enter {_key}");
                            rd.EnsureAck();
                            Log($"Entered key {_key}");
                            Thread.Sleep(_rnd.Next() % 100);
                            if (_id >= 2)
                            {
                                if(counter > 100)
                                {
                                    // end abruptly without releasing
                                    return;
                                }
                            }
                            Log($"Leaving key {_key}");
                            wr.Send($"leave {_key}");
                            rd.EnsureAck();                            
                        }
                    }catch(Exception e)
                    {
                        Log($"Exception occured: {e}");                        
                    }
                }
                Log("Ending");
            }
        }

        private void Log(string fmt, params object[] prms)
        {
            Console.WriteLine($"[{_id}] {string.Format(fmt, prms)}");
        }

        public static void Cancel()
        {
            isCancelled = true;
        }
    }

    public static class Extensions
    {
        public static TextWriter Send(this TextWriter wr, string msg)
        {
            wr.WriteLine(msg);
            wr.Flush();
            return wr;
        }
        public static void EnsureAck(this TextReader rd)
        {
            var line = rd.ReadLine();
            if( string.IsNullOrWhiteSpace(line) || line != "ack")
            {
                throw new Exception("Invalid response");
            }             
        }
    }
}
