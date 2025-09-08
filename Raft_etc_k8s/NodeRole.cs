using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raft_etc_k8s
{
    public enum NodeRole
    {
        Follower,
        Candidate,
        Leader
    }
}
