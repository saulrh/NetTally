﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetTally.Output
{
    /// <summary>
    /// Meta-vote class to allow storing the primary vote, along with any
    /// 'child' votes that have the same first line of text as the main vote.
    /// </summary>
    public class VoteNode
    {
        /// <summary>
        /// Allow access to the TallyOutput object that created this node.
        /// </summary>
        readonly TallyOutput owner;

        public string Text { get; set; }
        public string Line { get; set; }
        public HashSet<string> Voters { get; } = new HashSet<string>();
        public List<VoteNode> Children { get; } = new List<VoteNode>();

        public int VoterCount => Voters.Count(v => !v.StartsWith(Utility.Text.PlanNameMarker, StringComparison.Ordinal));

        public VoteNode(TallyOutput owner, string text, HashSet<string> voters)
        {
            this.owner = owner;
            Text = text;

            AddVoters(voters);
            SetLine();
        }

        /// <summary>
        /// Add new voters to this node's voter list.
        /// </summary>
        /// <param name="voters">Voters to add.</param>
        public void AddVoters(HashSet<string> voters)
        {
            if (voters != null)
                Voters.UnionWith(voters);
        }

        /// <summary>
        /// Add a child node to the current node.
        /// Update this node's voter list to include any new voters from the child.
        /// </summary>
        /// <param name="node">New child vote node.</param>
        public void AddChild(VoteNode node)
        {
            Voters.UnionWith(node.Voters);
            Children.Add(node);
            SetLine();
        }

        /// <summary>
        /// Update the Line property when the voter list changes, so that it
        /// always points at the first voter (for those votes that will use
        /// the compact reference format).
        /// </summary>
        private void SetLine()
        {
            var lines = Utility.Text.GetStringLines(Text);

            if (lines.Count == 1)
                Line = lines.First();
            else if (lines.Count == 2 && (VoteString.GetVotePrefix(lines[1]) != string.Empty))
                Line = lines.First();
            else
                Line = GetCondensedLine();
        }

        /// <summary>
        /// Create the text to be used in the Line property, as a condensed
        /// link format to the original voter.
        /// </summary>
        /// <returns>Returns the condensed line.</returns>
        private string GetCondensedLine()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("[X]");

            string task = VoteString.GetVoteTask(Text);
            if (task != string.Empty)
                sb.Append($"[{task}]");


            string firstVoter = owner.GetFirstVoter(Voters);
            string link;

            VoteType voteType = Utility.Text.IsPlanName(firstVoter) ? VoteType.Plan : VoteType.Vote;
            link = owner.GetVoterUrl(firstVoter, voteType);

            sb.Append($" Plan: {firstVoter} — {link}");

            return sb.ToString();
        }
    }
}
