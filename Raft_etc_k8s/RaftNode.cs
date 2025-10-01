using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Raft_etc_k8s
{
    public class RaftNode
    {
        public int Id { get; set; }
        private DateTime lastHeartBeat;
        public NodeRole Role { get; set; }

        public RaftNode(int id)
        {
            Id = id;
            Role = NodeRole.Follower;
            ResetElectionTimeout();
            lastHeartBeat = DateTime.UtcNow;
        }

        public int CurrentTerm { get; private set; } = 0;
        public int? VotedFor { get; set; } = null;
        public List<LogEntry> Log { get; private set; } = [];
        private static readonly Random rnd = new();

        private int electionTimeoutMs = rnd.Next(1500, 3000);

        public bool IsAlive { get; set; } = true;
        public (string host, int port)[] Peers { get; set; } = Array.Empty<(string, int)>();
        private int votesRecieved = 0;

        private void ResetElectionTimeout()
        {
            electionTimeoutMs = rnd.Next(1500, 3000); 
            lastHeartBeat = DateTime.UtcNow;
        }
        public async Task RunAsync(int port)
        {
            //_ = Task.Run(() =>  ListenAsync(port));
            _ = ListenAsync(port); 
            while (true)
            {
                if (!IsAlive)
                {
                    await Task.Delay(500);
                    continue;
                }

                if(Role == NodeRole.Leader)
                {
                    await SendHeartbeatAsync();
                    await Task.Delay(1000);

                }
                else
                {
                    if((DateTime.UtcNow - lastHeartBeat).TotalMilliseconds > electionTimeoutMs)
                    {
                        Console.WriteLine($"Node {Id} election timeout! Starting election");
                        StartElection();
                    }
                    await Task.Delay(100);
                }
            }
        }
        private bool leaderElected = false;
        private void StartElection()
        {
            if (leaderElected) return;
            CurrentTerm++;
            Role = NodeRole.Candidate;
            VotedFor = Id;
            votesRecieved = 1; // vote for itself.
            ResetElectionTimeout(); 

            foreach (var peer in Peers)
            {
                _ = RaftClient.SendAsync(peer.host, peer.port, new RaftMessage
                {
                    Type = RaftMessageType.RequestVote,
                    Term = CurrentTerm,
                    SenderId = Id
                });
            }

            Console.WriteLine($"Node {Id} became Candidate for term {CurrentTerm}");
        }

        public bool RequestVote(int term, int candidateId)
        {
            if(term > CurrentTerm)
            {
                CurrentTerm = term;
                VotedFor = null;
                Role = NodeRole.Follower;
            }

            if(VotedFor == null)
            {
                VotedFor = candidateId;
                return true;
            }

            return false;
        }

        // ------------------ HEARTBEAT -------------------
        private async Task SendHeartbeatAsync()
        {
            foreach (var (host, port) in Peers)
            {
                await RaftClient.SendAsync(host, port, new RaftMessage
                {
                    Type = RaftMessageType.Heartbeat,
                    Term = CurrentTerm,
                    SenderId = Id
                });
            }
            Console.WriteLine($"Leader {Id} sent heartbeat (term {CurrentTerm})");
        }

        public void ReceiveHeartbeat(int term, int leaderId)
        {
            if (term >= CurrentTerm)
            {
                CurrentTerm = term;
                Role = NodeRole.Follower;
                VotedFor = leaderId;
                lastHeartBeat = DateTime.UtcNow;
                ResetElectionTimeout(); // important!

                Console.WriteLine($"Node {Id} received heartbeat from Leader {leaderId} (term {term})");
            }
        }

        private void BecomeLeader()
        {
            Role = NodeRole.Leader;
            Console.WriteLine($"Node {Id} became LEADER for term {CurrentTerm}");
            leaderElected = true;
        }

        public void CheckElectionTimeout(List<RaftNode> cluster)
        {
            if (Role == NodeRole.Leader) return;

            if ((DateTime.UtcNow - lastHeartBeat).TotalMilliseconds >electionTimeoutMs)
            {
                Console.WriteLine($"Node {Id} timed out, starting election...");
                lastHeartBeat = DateTime.UtcNow;
            }
        }
        public void HandleMessage(RaftMessage msg)
        {
            if (!IsAlive) return;

            switch (msg.Type)
            {
                case RaftMessageType.Heartbeat:
                    if (msg.Term >= CurrentTerm)
                    {
                        CurrentTerm = msg.Term;
                        Role = NodeRole.Follower;
                        VotedFor = msg.SenderId;
                        lastHeartBeat = DateTime.UtcNow;
                        Console.WriteLine($"Node {Id} received heartbeat from Leader {msg.SenderId}");
                    }
                    break;

                case RaftMessageType.RequestVote:
                    // If candidate term < current term, reject vote
                    if (msg.Term < CurrentTerm)
                    {
                        Console.WriteLine($"Node {Id} rejects vote for {msg.SenderId} because term {msg.Term} < current {CurrentTerm}");
                        return; // do not grant vote
                    }

                    if (msg.Term > CurrentTerm)
                    {
                        CurrentTerm = msg.Term;
                        Role = NodeRole.Follower;
                        VotedFor = null;
                    }

                    bool grantVote = false;
                    if (VotedFor == null || VotedFor == msg.SenderId)
                    {
                        VotedFor = msg.SenderId;
                        grantVote = true;
                    }

                    //if (grant) VotedFor = msg.SenderId;
                    var peerPort = Peers.First(p => p.host == "localhost").port;

                    _ = RaftClient.SendAsync("localhost", peerPort, new RaftMessage
                    {
                        Type = RaftMessageType.VoteResponse,
                        Term = CurrentTerm,
                        SenderId = Id,
                        VoteGranted = grantVote
                    });  

                    Console.WriteLine($"Node {Id} voted {(grantVote ? "YES" : "NO")} for {msg.SenderId}");
                    break;

                case RaftMessageType.VoteResponse:
                    if(msg.Term > CurrentTerm)
                    {
                        CurrentTerm = msg.Term;
                        Role = NodeRole.Follower;
                        VotedFor = null;
                    }

                    Console.Write($"Term is: {msg.Term} and CurrentTerm: {CurrentTerm}");
                    if (Role == NodeRole.Candidate && msg.Term >= CurrentTerm && msg.VoteGranted == true)
                    {
                        votesRecieved++;

                        int majority = (Peers.Length + 1) / 2 + 1;
                        if(votesRecieved >= majority)
                        {
                            BecomeLeader();
                            break;
                        }


                        //BecomeLeader();
                    }
                    break;
            }
        }
        private async Task ListenAsync(int port)
        {
            var listener = new TcpListener(System.Net.IPAddress.Any, port);
            listener.Start();

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(async () =>
                {
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8);

                    string? json = await reader.ReadLineAsync();
                    if (json != null)
                    {
                        var msg = JsonSerializer.Deserialize<RaftMessage>(json);
                        if (msg != null) HandleMessage(msg);
                    }
                });
            }
        }

        public void AppendEntry(LogEntry entry, List<RaftNode> cluster)
        {
            if (Role != NodeRole.Leader) return;

            Log.Add(entry);
            int acks = 1;

            foreach (var peer in cluster)
            {
                if (peer.Id == Id) continue;
                if (peer.ReceiveEntry(entry, CurrentTerm))
                    acks++;
            }

            if (acks > cluster.Count / 2)
                Console.WriteLine($"Leader {Id} committed entry '{entry.Command}'");
        }

        public bool ReceiveEntry(LogEntry entry, int leaderTerm)
        {
            if (leaderTerm < CurrentTerm) return false;
            Log.Add(entry);
            return true;
        }
    }
}
