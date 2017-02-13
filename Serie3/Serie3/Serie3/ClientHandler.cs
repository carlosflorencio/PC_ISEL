using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serie3
{
    class ClientHandler
    {

        private readonly ISet<string> _acquiredKeys;
        private readonly TcpClient _client;
        private readonly int _id;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _map;
        private readonly int _maxRequestsInRegion;

        public ClientHandler(int id, TcpClient client,
            ConcurrentDictionary<string, SemaphoreSlim> map, int maxRequestsInRegion)
        {
            _id = id;
            _map = map;
            _client = client;
            _acquiredKeys = new HashSet<string>();
            _maxRequestsInRegion = maxRequestsInRegion;
        }

        public void Handle()
        {
            using (var stream = _client.GetStream())
            using (var rd = new StreamReader(stream))
            using (var wr = new StreamWriter(stream))
            {
                try
                {
                    LoopHandlingCommands(rd, wr);
                }
                catch (IOException e)
                {
                    Log("Exception: {0}", e);
                }
                // Releasing all the keys acquired by this connection
                // This will ensure liveness if a client dies inside a region
                // On a network partition the number of clients inside a region may be
                // greater than the one allowed
                var freeze = _acquiredKeys.ToArray();
                foreach (var s in freeze)
                {
                    HandleCommand("leave", s);
                }

                Log("client ended");
            }
        }

        private void LoopHandlingCommands(StreamReader rd, StreamWriter wr)
        {
            while (true)
            {
                try
                {
                    var line = rd.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        Log($"Client is disconnected");
                        break;
                    }

                    Log($"Received {line}");
                    var parts = line.Split(' ');
                    if (parts.Length != 2)
                    {
                        wr.Send("nack: invalid command");
                        continue;
                    }

                    HandleCommand(parts[0], parts[1]);
                    wr.Send("ack");
                }
                catch (CommandException e)
                {
                    wr.Send($"nack: {e.Message}");
                    // keep processing more messages
                }
            }
        }

        private void HandleCommand(string command, string key)
        {
            var semaphore = _map.GetOrAdd(key, _ => new SemaphoreSlim(_maxRequestsInRegion));
            if (command == "enter")
            {
                // This will block until the client can enter the region
                semaphore.Wait();
                _acquiredKeys.Add(key);
                Log($"Acquired key {key}");
            }
            else if (command == "leave")
            {
                semaphore.Release();
                _acquiredKeys.Remove(key);
                Log($"Released key {key}");
            }
            else
            {
                throw new CommandException("unknown command");
            }
        }

        private void Log(string fmt, params object[] prms)
        {
            Console.WriteLine($"[{_id}]" + string.Format(fmt, prms));
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

    }
}
