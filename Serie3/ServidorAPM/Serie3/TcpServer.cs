﻿/**
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
using System.Text;
using System.Threading;

namespace Serie3 {

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

            BeginListen(server);
            logger.Add($"Server is listening on {LocalIp}:{LocalPort}");

            logger.Add("Press <enter> to shutdown the server...");
            Console.ReadLine();

            server.Stop();
        }

        private void BeginListen(TcpListener server) {
            server.Start(); // start listen for clients

            AsyncCallback onAcceptProcessing = null;

            // Fix recursive calls
            AsyncCallback onAcceptEntryPoint = ar => {
                if (!ar.CompletedSynchronously) {
                    onAcceptProcessing(ar);
                } else {
                    rcounter.Value += 1;
                    if (rcounter.Value > MaxNestedIoCallbacks) {
                        ThreadPool.QueueUserWorkItem(_ => { onAcceptProcessing(ar); });
                    } else {
                        onAcceptProcessing(ar);
                    }
                    rcounter.Value -= 1;
                }
            };

            // Process accept
            onAcceptProcessing = ar => {
                try {
                    var client = server.EndAcceptTcpClient(ar);
                    logger.Add($"Client accepted with id {currentClientId}");

                    logger.Add(
                        $"ThreadPool: {Thread.CurrentThread.IsThreadPoolThread} ThreadId: {Thread.CurrentThread.ManagedThreadId}");

                    int c = Interlocked.Increment(ref activeConnections);
                    if (c < MaxActiveConnections) {
                        server.BeginAcceptTcpClient(onAcceptEntryPoint, null);
                    }

                    // Handle this client
                    var state = new ConnectionState(currentClientId, client, logger);
                    ClientHandlerAsync.BeginReadSocket(state);


                    Interlocked.Increment(ref currentClientId);

                    c = Interlocked.Decrement(ref activeConnections);
                    if (c == MaxActiveConnections - 1) {
                        server.BeginAcceptTcpClient(onAcceptEntryPoint, null);
                    }
                } catch (ObjectDisposedException) {
                    // benign exception that occurs when the server shuts down
                    // and stops listening the server socket
                }
            };

            // First accept
            server.BeginAcceptTcpClient(onAcceptEntryPoint, null);
        }

        /*
        |--------------------------------------------------------------------------
        | Accept Connection Callback
        |--------------------------------------------------------------------------
        */

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