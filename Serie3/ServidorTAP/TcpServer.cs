using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ServidorTAP {

    class ConnectionState {

        public readonly TcpClient client;
        public readonly int id;
        public readonly ISet<string> acquiredKeys = new HashSet<string>();
        public readonly NetworkStream stream;

        // Temporary Buffer for reads
        public byte[] buffer = new byte[4 * 1204];
        public LoggerThread log;

        public ConnectionState(int cid, TcpClient c, LoggerThread logger) {
            this.id = cid;
            this.log = logger;
            this.client = c;
            this.stream = this.client.GetStream();
        }

    }

    class Server {

        // Constants
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
            server.Start();
            logger.Add($"Server is listening on {LocalIp}:{LocalPort}");

            ListenAsync(server);

            logger.Add("Press <enter> to shutdown the server...");
            Console.ReadLine();

            server.Stop();
        }

        private async void ListenAsync(TcpListener server) {
            try {
                var client = await server.AcceptTcpClientAsync();
                logger.Add($"Client accepted with id {currentClientId}");

                int c = Interlocked.Increment(ref activeConnections);
                if (c < MaxActiveConnections) {
                    ListenAsync(server);
                }

                var state = new ConnectionState(currentClientId, client, logger);
                Interlocked.Increment(ref currentClientId);
                await ClientHandlerAsync.ProcessConnectionAsync(state);

                c = Interlocked.Decrement(ref activeConnections);

                if (c == MaxActiveConnections - 1) {
                    ListenAsync(server);
                }
            } catch (ObjectDisposedException e) {
                // benign exception that occurs when the server shuts down
                // and stops listening the server socket
            } catch (Exception e) {
                logger.Add($"Exception {e}");
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