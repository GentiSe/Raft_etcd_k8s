using Raft_etc_k8s;

public class Program
{
    public static void Main()
    {
        var node1 = new RaftNode(1);
        var node2 = new RaftNode(2);
        var node3 = new RaftNode(3);

        node1.Peers = new[] { ("localhost", 5002), ("localhost", 5003) };
        node2.Peers = new[] { ("localhost", 5001), ("localhost", 5003) };
        node3.Peers = new[] { ("localhost", 5001), ("localhost", 5002) };

        _ = node1.RunAsync(5001);
        _ = node2.RunAsync(5002);
        _ = node3.RunAsync(5003);

        Console.WriteLine("Raft cluster started with 3 nodes.");

        Console.WriteLine("Press 'k' + Enter to kill leader, 'r' + Enter to revive node 1, 'q' to quit.");

        while (true)
        {
            var key = Console.ReadLine();
            if (key == "q") break;

            if (key == "k")
            {                
                var leader = new List<RaftNode> { node1, node2, node3 }.Find(n => n.Role == NodeRole.Leader);
                if (leader != null)
                {
                    leader.IsAlive = false;
                    Console.WriteLine($"Node {leader.Id} (leader) killed!");
                }
            }

            if (key == "r")
            {
                node1.IsAlive = true;
                Console.WriteLine("Node 1 revived!");
            }
        }

        //var cluster = new List<RaftNode>
        //{
        //    new(1),
        //    new(2),
        //    new(3)
        //};

        //// Elect a leader
        //cluster[0].StartElection(cluster);

        //// Leader appends a command
        //var leader = cluster.Find(n => n.Role == NodeRole.Leader);
        //leader?.AppendEntry(new LogEntry { Term = leader.CurrentTerm, Command = "set x=10" }, cluster);

        //// Print logs
        //foreach (var node in cluster)
        //{
        //    Console.WriteLine($"Node {node.Id} log: [{string.Join(", ", node.Log.ConvertAll(e => e.Command))}]");
        //}
    }
}
