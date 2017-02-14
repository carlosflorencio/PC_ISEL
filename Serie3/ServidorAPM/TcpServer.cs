using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ServidorAPM {

    class ConnectionState {

        public readonly TcpClient client; // needed to close
        public readonly int id;
        public readonly ISet<string> acquiredKeys = new HashSet<string>();
        public readonly NetworkStream stream;
        public readonly TcpListener server; // needed to perform disconnect action

        // Temporary Buffer for reads
        public byte[] buffer = new byte[4 * 1204];
        public LoggerThread log;

        public ConnectionState(int cid, TcpListener s, TcpClient c, LoggerThread logger) {
            this.id = cid;
            this.server = s;
            this.log = logger;
            this.client = c;
            this.stream = this.client.GetStream();
        }

    }

    class Server {

        // Constants
        private const int MaxNestedIoCallbacks = 10;
        private const int MaxActiveConnections = 10;
        private const int LocalPort = 8888;
        private const string LocalIp = "0.0.0.0";

        // Count the number of nested accept callbacks on each thread
        private static ThreadLocal<int> rcounter = new ThreadLocal<int>();

        // Number of active connections
        private static int activeConnections;

        // Client id (0, 1, 2, 3..) Incremented with Interlocked
        private static int currentClientId;

        // Logger Thread
        private LoggerThread logger;


        public Server(LoggerThread logger) {
            this.logger = logger;
        }

        /*
        |--------------------------------------------------------------------------
        | Run: Accept Connections Async
        |--------------------------------------------------------------------------
        */

        public void Run() {
            var server = new TcpListener(IPAddress.Parse(LocalIp), LocalPort);

            server.Start(); // start listen for clients
            logger.Add($"Server is listening on {LocalIp}:{LocalPort}");

            // First accept
            server.BeginAcceptTcpClient(OnAcceptEntryPoint, Tuple.Create(server, logger));

            logger.Add("Press <enter> to shutdown the server...");
            Console.ReadLine();

            server.Stop();
        }


        // Fix recursive calls
        private static void OnAcceptEntryPoint(IAsyncResult ar) {
            if (!ar.CompletedSynchronously) {
                OnAcceptProcessing(ar);
            } else {
                rcounter.Value += 1;
                if (rcounter.Value > MaxNestedIoCallbacks) {
                    ThreadPool.QueueUserWorkItem(_ => { OnAcceptProcessing(ar); });
                } else {
                    OnAcceptProcessing(ar);
                }
                rcounter.Value -= 1;
            }
        }

        // Process accept
        private static void OnAcceptProcessing(IAsyncResult ar) {
            var state = (Tuple<TcpListener, LoggerThread>) ar.AsyncState;
            var server = state.Item1;
            var logger = state.Item2;

            try {
                var client = server.EndAcceptTcpClient(ar);
                logger.Add($"Client accepted with id {currentClientId}");

                int curr = Interlocked.Increment(ref activeConnections);
                if (curr < MaxActiveConnections) {
                    server.BeginAcceptTcpClient(OnAcceptEntryPoint, state);
                } // not allowed? should we refuse the client instead?

                // Handle this client
                var newState = new ConnectionState(currentClientId, server, client, logger);
                ClientHandlerAsync.BeginReadSocket(newState);

                Interlocked.Increment(ref currentClientId);
            } catch (ObjectDisposedException) {
                // benign exception that occurs when the server shuts down
                // and stops listening the server socket
            }
        }

        /*
        |--------------------------------------------------------------------------
        | Disconnect client, Accept another waiting
        |--------------------------------------------------------------------------
        */

        public static void Disconnected(ConnectionState state) {
            var c = Interlocked.Decrement(ref activeConnections);
            if (c == MaxActiveConnections - 1) {
                state.server.BeginAcceptTcpClient(OnAcceptEntryPoint,
                    Tuple.Create(state.server, state.log));
            }
        }

    }


    class ServerProgram {

        public static void Main(string[] args) {
            var logger = new LoggerThread();
            logger.Start();

            var server = new Server(logger);

            server.Run(); // returns after key enter

            // logger has a foreground thread, we have to force it to close
            logger.shutdown();
        }

    }

}