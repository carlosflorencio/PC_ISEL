/**
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Reference code for SE#3, winter 2016/17 
 *
 *  Pedro Félix, December 2016
 *
 **/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;

namespace Serie3
{

    class ConnectionState
    {

        // Connection Info
        public readonly ManualResetEventSlim acceptDone;
        public readonly TcpListener listener;
        public TcpClient client;
        public NetworkStream stream;

        // Throttled Region Info
        public readonly ConcurrentDictionary<string, SemaphoreSlim> map;
        public readonly int maxRequestsInRegion;

        // Client Info
        public readonly int clientId;
        public readonly ISet<string> acquiredKeys;

        // Temporary Buffer for reads
        public byte[] buffer = new byte[4 * 1204];

        public ConnectionState(int cid, ManualResetEventSlim e, TcpListener l,
            ConcurrentDictionary<string, SemaphoreSlim> m, int maxRequests)
        {
            this.clientId = cid;
            this.acceptDone = e;
            this.listener = l;
            this.map = m;
            this.maxRequestsInRegion = maxRequests;
            this.acquiredKeys = new HashSet<string>();
        }
    }

    class Server
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> map =
            new ConcurrentDictionary<string, SemaphoreSlim>();

        private static readonly ManualResetEventSlim acceptDone = new ManualResetEventSlim(false);

        private const int MaxRequestsInRegion = 1;
        private const string LocalIp = "0.0.0.0";
        private const int LocalPort = 8888;


        /*
        |--------------------------------------------------------------------------
        | Run: Accept Connections Async
        |--------------------------------------------------------------------------
        */
        private static ThreadLocal<int> rcounter = new ThreadLocal<int>();
        public static void Run()
        {
            Log(
                $"MAIN THREAD: ThreadPool: {Thread.CurrentThread.IsThreadPoolThread} ThreadId: {Thread.CurrentThread.ManagedThreadId}");
            var cid = 0;
            var listener = new TcpListener(IPAddress.Parse(LocalIp), LocalPort);
            listener.Start();
            Log($"Server is listening on {LocalIp}:{LocalPort}");


            AsyncCallback onAcceptEntryPoint = delegate(IAsyncResult ar) {
                if (!ar.CompletedSynchronously) {
                    AcceptCallback(ar);
                } else {
                    rcounter.Value += 1;
                    if (rcounter.Value > 10) {
                        ThreadPool.QueueUserWorkItem(_ => { AcceptCallback(ar); });
                    } else {
                        AcceptCallback(ar);
                    }
                    rcounter.Value -= 1;
                }
            };


                while (true)
            {
                // Set the event to nonsignaled state.  
                acceptDone.Reset();

                // Create some state for this connection
                var state = new ConnectionState(cid++, acceptDone, listener, map, MaxRequestsInRegion);

                Log("Waiting for a connection..");
                listener.BeginAcceptTcpClient(onAcceptEntryPoint, state);

                // Wait until a connection is made before continuing.  
                acceptDone.Wait();
            }
        }

        /*
        |--------------------------------------------------------------------------
        | Accept Connection Callback
        |--------------------------------------------------------------------------
        */

        private static void AcceptCallback(IAsyncResult ar)
        {
            // Get the socket that handles the client request.
            var state = (ConnectionState)ar.AsyncState;
            var client = state.listener.EndAcceptTcpClient(ar);
            Log($"Client accepted with id {state.clientId}");

            // Signal the main thread to continue.
            state.acceptDone.Set();

            Log(
                $"ThreadPool: {Thread.CurrentThread.IsThreadPoolThread} ThreadId: {Thread.CurrentThread.ManagedThreadId}");

            // Update the state
            state.client = client;
            state.stream = client.GetStream();

            // Read the socket async
            ClientHandlerAsync.StartReadAsync(state);
        }

        public static volatile int counter = 0;



        /*
        |--------------------------------------------------------------------------
        | Log: Add messages to the logger thread
        |--------------------------------------------------------------------------
        */

        public static void Log(string fmt, params object[] prms)
        {
            Console.WriteLine(string.Format(fmt, prms));
        }

    }


    class ServerProgram
    {

        public static void Main(string[] args)
        {
            Server.Run();
            // server.Run never returns 
        }

    }

}