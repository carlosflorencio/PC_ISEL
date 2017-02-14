using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServidorTAP {

    class ClientHandlerAsync {

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> map =
            new ConcurrentDictionary<string, SemaphoreSlim>();

        private const int MaxRequestsInRegion = 1;

        /*
        |--------------------------------------------------------------------------
        | Process Client
        |--------------------------------------------------------------------------
        */

        public static async Task ProcessConnectionAsync(ConnectionState state) {
            try {
                do {
                    int len = await state.stream.ReadAsync(state.buffer, 0, state.buffer.Length);

                    if (len <= 0) { // client disconnected
                        Log(state, $"Client is disconnected");
                        ClientDisconnect(state);
                        break;
                    }

                    var line = Encoding.ASCII.GetString(state.buffer, 0, len).Trim();

                    Log(state, $"Received {line}");

                    var parts = line.Split(' ');
                    if (parts.Length != 2) {
                        await WriteAsync("nack: invalid command", state);
                        continue;
                    }

                    try {
                        HandleCommand(parts[0], parts[1], state); // May block this thread :(
                        await WriteAsync("ack", state);
                    } catch (CommandException e) {
                        await WriteAsync($"nack: {e.Message}", state);
                    }
                } while (true);
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

        private static Task WriteAsync(string msg, ConnectionState state) {
            var bytes = Encoding.ASCII.GetBytes(msg + Environment.NewLine);

            return state.stream.WriteAsync(bytes, 0, bytes.Length);
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
            var region = map.GetOrAdd(key, _ => new SemaphoreSlim(MaxRequestsInRegion));
            if (command == "enter") {
                // This will block until the client can enter the region
                region.Wait();
                state.acquiredKeys.Add(key);
                Log(state, $"Acquired key {key}");
            } else if (command == "leave") {
                region.Release();
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