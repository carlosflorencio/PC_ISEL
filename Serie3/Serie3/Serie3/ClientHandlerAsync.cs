using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serie3 {

    class ClientHandlerAsync {

        /*
        |--------------------------------------------------------------------------
        | Read Async
        |--------------------------------------------------------------------------
        */

        public static void StartReadAsync(ConnectionState state) {
            Log(state.clientId, "Started Reading");
            state.stream.BeginRead(state.buffer, 0, state.buffer.Length, EndReadAsync, state);
        }

        public static void EndReadAsync(IAsyncResult ar) {
            var state = (ConnectionState) ar.AsyncState;

            try {
                var len = state.stream.EndRead(ar);
                Log(state.clientId, "End reading " + len + " bytes!");

                if (len == 0) { // client disconnected
                    Log(state.clientId, $"Client is disconnected");
                    ClientDisconnect(state);
                    return;
                }

                var line = Encoding.ASCII.GetString(state.buffer, 0, len).Trim();

                Log(state.clientId, $"Received {line}");
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
                Log(state.clientId, "Exception: {0}", e);
                ClientDisconnect(state);
            }
        }

        /*
        |--------------------------------------------------------------------------
        | Write Async
        |--------------------------------------------------------------------------
        */
        public static void StartWriteAsync(string msg, ConnectionState state) {
            var bytes = Encoding.ASCII.GetBytes(msg + Environment.NewLine);

            state.stream.BeginWrite(bytes, 0, bytes.Length, EndWriteAsync, state);
        }

        public static void EndWriteAsync(IAsyncResult ar) {
            var state = (ConnectionState) ar.AsyncState;

            try {
                state.stream.EndWrite(ar);

                StartReadAsync(state);

            } catch (IOException e) {
                // The socket was closed!
                Log(state.clientId, "Exception: {0}", e);
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
            Log(state.clientId, "client ended");
            // state object will eventually be garbage collected
        }

        /*
        |--------------------------------------------------------------------------
        | Parse commands
        |--------------------------------------------------------------------------
        */

        private static void HandleCommand(string command, string key, ConnectionState state) {
            var semaphore = state.map.GetOrAdd(key, _ => new SemaphoreSlim(state.maxRequestsInRegion));
            if (command == "enter") {
                // This will block until the client can enter the region
                semaphore.Wait();
                state.acquiredKeys.Add(key);
                Log(state.clientId, $"Acquired key {key}");
            } else if (command == "leave") {
                semaphore.Release();
                state.acquiredKeys.Remove(key);
                Log(state.clientId, $"Released key {key}");
            } else {
                throw new CommandException("unknown command");
            }
        }

        private static void Log(int id, string fmt, params object[] prms) {
            Server.Log($"[{id}]" + string.Format(fmt, prms));
        }

    }

}