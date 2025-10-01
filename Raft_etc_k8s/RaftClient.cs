using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Raft_etc_k8s
{
    public class RaftClient
    {
        public static async Task SendAsync(string host, int port, RaftMessage msg)
        {
            try
            {
                var rnd = new Random();
                await Task.Delay(rnd.Next(50, 200));
                if (rnd.NextDouble() < 0.1) return; // drop 10% of messages

                using var client = new TcpClient();
                await client.ConnectAsync(host, port);
                using var stream = client.GetStream();
                using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                string json = JsonSerializer.Serialize(msg);
                await writer.WriteLineAsync(json);
            }
            catch { /* ignore network errors */ }
        }
    }
    public enum RaftMessageType
    {
        RequestVote,
        VoteResponse,
        Heartbeat
    }
    public class RaftMessage
    {
        public RaftMessageType Type { get; set; }
        public int Term { get; set; }
        public int SenderId { get; set; }
        public bool? VoteGranted { get; set; }
    }
}
