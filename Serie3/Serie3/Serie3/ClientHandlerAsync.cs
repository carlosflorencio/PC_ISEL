using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serie3 {

    class ClientHandlerAsync {

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> map =
            new ConcurrentDictionary<string, SemaphoreSlim>();

        private const int MaxRequestsInRegion = 1;

        /*
        |--------------------------------------------------------------------------
        | Read Async
        |--------------------------------------------------------------------------
        */

        public static void BeginReadSocket(ConnectionState state) {
            state.stream.BeginRead(state.buffer, 0, state.buffer.Length, EndReadSocket, state);
        }

        private static void EndReadSocket(IAsyncResult ar) {
            var state = (ConnectionState) ar.AsyncState;

            try {
                // The BeginRead method reads as much data as is available, 
                // up to the number of bytes specified by the size parameter.
                // Since our buffer is larger than the longest client message,
                // we dont need to read again
                var len = state.stream.EndRead(ar);

                if (len <= 0) { // client disconnected
                    Log(state, $"Client is disconnected");
                    ClientDisconnect(state);
                    return;
                }

                var line = Encoding.ASCII.GetString(state.buffer, 0, len).Trim();

                Log(state, $"Received {line}");
                var parts = line.Split(' ');
                if (parts.Length != 2) {
                    StartWriteAsync("nack: invalid command", state);
                    return;
                }

                try {
                    HandleCommand(parts[0], parts[1], state); // May block this thread
                    StartWriteAsync("ack", state);
                } catch (CommandException e) {
                    StartWriteAsync($"nack: {e.Message}", state);
                }
            } catch (IOException e) {
                // The socket was closed!
                Log(state, "Exception: {0}", e);
                ClientDisconnect(state);
            }
        }

        /*
        |--------------------------------------------------------------------------
        | Write Async
        |--------------------------------------------------------------------------
        */
        private static void StartWriteAsync(string msg, ConnectionState state) {
            var bytes = Encoding.ASCII.GetBytes(msg + Environment.NewLine);

            state.stream.BeginWrite(bytes, 0, bytes.Length, EndWriteAsync, state);
        }

        private static void EndWriteAsync(IAsyncResult ar) {
            var state = (ConnectionState) ar.AsyncState;

            try {
                state.stream.EndWrite(ar);

                BeginReadSocket(state);

            } catch (IOException e) {
                // The socket was closed!
                Log(state, "Exception: {0}", e);
                ClientDisconnect(state);
            }
        }

        /*
        |--------------------------------------------------------------------------
        | Client Disconnect - Free acquired keys
        |--------------------------------------------------------------------------
        */

        private static void ClientDisconnect(ConnectionState state) {
            // Releasing all the keys acquired by this connection
            // This will ensure liveness if a client dies inside a region
            // On a network partition the number of clients inside a region may be
            // greater than the one allowed
            var freeze = state.acquiredKeys.ToArray();
            foreach (var s in freeze) {
                HandleCommand("leave", s, state);
            }

            state.stream.Close();
            state.client.Close();
            Log(state, "client ended");
            // state object will eventually be garbage collected
        }

        /*
        |--------------------------------------------------------------------------
        | Parse commands
        |--------------------------------------------------------------------------
        */

        private static void HandleCommand(string command, string key, ConnectionState state) {
            var semaphore = map.GetOrAdd(key, _ => new SemaphoreSlim(MaxRequestsInRegion));
            if (command == "enter") {
                // This will block until the client can enter the region
                semaphore.Wait();
                state.acquiredKeys.Add(key);
                Log(state, $"Acquired key {key}");
            } else if (command == "leave") {
                semaphore.Release();
                state.acquiredKeys.Remove(key);
                Log(state, $"Released key {key}");
            } else {
                throw new CommandException("unknown command");
            }
        }

        private static void Log(ConnectionState state, string fmt, params object[] prms) {
           state.log.Add($"[{state.id}]" + string.Format(fmt, prms));
        }

    }

}