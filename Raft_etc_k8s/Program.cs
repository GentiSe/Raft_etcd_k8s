using Raft_etc_k8s;

public class Program
{
    public static void Main()
    {
        var cluster = new List<RaftNode>
        {
            new RaftNode(1),
            new RaftNode(2),
            new RaftNode(3)
        };

        // Elect a leader
        cluster[0].StartElection(cluster);

        // Leader appends a command
        var leader = cluster.Find(n => n.Role == NodeRole.Leader);
        leader?.AppendEntry(new LogEntry { Term = leader.CurrentTerm, Command = "set x=10" }, cluster);

        // Print logs
        foreach (var node in cluster)
        {
            Console.WriteLine($"Node {node.Id} log: [{string.Join(", ", node.Log.ConvertAll(e => e.Command))}]");
        }
    }
}
