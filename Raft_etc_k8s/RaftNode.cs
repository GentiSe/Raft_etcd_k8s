using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raft_etc_k8s
{
    public class RaftNode(int id)
    {
        public int Id { get; set; } = id;
        public NodeRole Role { get; set; }

        public int CurrentTerm { get; private set; } = 0;
        public int? VotedFor { get; set; } = null;
        public List<LogEntry> Log { get; private set; } = [];

        public void StartElection(List<RaftNode> cluster)
        {
            CurrentTerm++;

            int votes = 1;

            foreach(var peer in cluster)
            {
                if (peer.Id == Id) continue;
                if (peer.RequestVote(CurrentTerm, Id))
                {
                    votes++;
                }
            }

            if(votes > cluster.Count / 2)
            {
                Role = NodeRole.Leader;
                Console.WriteLine($"Node {Id} became Leader for term {CurrentTerm}");
            }

            else
            {
                Role = NodeRole.Follower;
            }

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
