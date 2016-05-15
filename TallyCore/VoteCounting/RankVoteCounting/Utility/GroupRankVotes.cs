﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetTally.VoteCounting
{
    // Task (string group), collection of votes (string vote, hashset of voters)
    using GroupedVotesByTask = IGrouping<string, KeyValuePair<string, HashSet<string>>>;

    public class RankGroupedVoters
    {
        public string VoteContent { get; set; }
        public IEnumerable<RankedVoters> Ranks { get; set; }
    }

    public class RankedVoters
    {
        public string Rank { get; set; }
        public IEnumerable<string> Voters { get; set; }
    }

    public static class GroupRankVotes
    {
        public static IEnumerable<RankGroupedVoters> GroupByVoteAndRank(GroupedVotesByTask task)
        {
            var res2 = from vote in task
                       let content = VoteString.GetVoteContent(vote.Key)
                       let rank = VoteString.GetVoteMarker(vote.Key)
                       group vote by content into votes
                       select new RankGroupedVoters
                       {
                           VoteContent = votes.Key,
                           Ranks = from v in votes
                                   group v by VoteString.GetVoteMarker(v.Key) into vr
                                   select new RankedVoters { Rank = vr.Key, Voters = vr.SelectMany(a => a.Value) }
                       };

            return res2;
        }
    }
}