﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NetTally.VoteCounting
{
    // List of preference results ordered by winner
    using RankResults = List<string>;
    // Task (string group), collection of votes (string vote, hashset of voters)
    using GroupedVotesByTask = IGrouping<string, KeyValuePair<string, HashSet<string>>>;

    public class SchulzeRankVoteCounter : BaseRankVoteCounter
    {
        /// <summary>
        /// Implementation to generate the ranking list for the provided set
        /// of votes for a specific task, based on the Schulze algorithm.
        /// </summary>
        /// <param name="task">The task that the votes are grouped under.</param>
        /// <returns>Returns a ranking list of winning votes.</returns>
        protected override RankResults RankTask(GroupedVotesByTask task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));


            Debug.WriteLine(">>Schulze Ranking<<");

            List<string> listOfChoices = GetAllChoices(task);

            var voterRankings = GroupRankVotes.GroupByVoterAndRank(task);

            int[,] pairwisePreferences = GetPairwisePreferences(voterRankings, listOfChoices);

            int[,] strongestPaths = GetStrongestPaths(pairwisePreferences, listOfChoices.Count);

            int[,] winningPaths = GetWinningPaths(strongestPaths, listOfChoices.Count);

            RankResults winningChoices = GetResultsInOrder(winningPaths, listOfChoices);

            return winningChoices;
        }

        #region Schulze Algorithm
        /// <summary>
        /// Fills the pairwise preferences.
        /// This goes through each voter's ranking options and updates an array indicating
        /// which options are preferred over which other options.  Each higher-ranked
        /// option gains one point in 'beating' a lower-ranked option.
        /// </summary>
        /// <param name="voterRankings">The voter rankings.</param>
        /// <param name="listOfChoices">The list of choices.</param>
        /// <returns>Returns a filled-in preferences array.</returns>
        private int[,] GetPairwisePreferences(IEnumerable<VoterRankings> voterRankings, List<string> listOfChoices)
        {
            int[,] pairwisePreferences = new int[listOfChoices.Count, listOfChoices.Count];

            var choiceIndexes = GetChoicesIndexes(listOfChoices);

            foreach (var voter in voterRankings)
            {
                var rankedChoices = voter.RankedVotes.Select(v => v.Vote);
                var unrankedChoices = listOfChoices.Except(rankedChoices);

                foreach (var choice in voter.RankedVotes)
                {
                    foreach (var otherChoice in voter.RankedVotes)
                    {
                        if ((choice.Vote != otherChoice.Vote) && (choice.Rank < otherChoice.Rank))
                        {
                            pairwisePreferences[choiceIndexes[choice.Vote], choiceIndexes[otherChoice.Vote]]++;
                        }
                    }

                    foreach (var nonChoice in unrankedChoices)
                    {
                        pairwisePreferences[choiceIndexes[choice.Vote], choiceIndexes[nonChoice]]++;
                    }
                }
            }

            return pairwisePreferences;
        }

        /// <summary>
        /// Calculate the strongest preference paths from the pairwise preferences table.
        /// </summary>
        /// <param name="pairwisePreferences">The pairwise preferences.</param>
        /// <param name="choicesCount">The choices count (size of the table).</param>
        /// <returns>Returns a table with the strongest paths between each pairwise choice.</returns>
        private int[,] GetStrongestPaths(int[,] pairwisePreferences, int choicesCount)
        {
            int[,] strongestPaths = new int[choicesCount, choicesCount];

            for (int i = 0; i < choicesCount; i++)
            {
                for (int j = 0; j < choicesCount; j++)
                {
                    if (i != j)
                    {
                        if (pairwisePreferences[i, j] >= pairwisePreferences[j, i])
                        {
                            strongestPaths[i, j] = pairwisePreferences[i, j];
                        }
                        else
                        {
                            strongestPaths[i, j] = 0;
                        }
                    }
                }
            }

            for (int i = 0; i < choicesCount; i++)
            {
                for (int j = 0; j < choicesCount; j++)
                {
                    if (i != j)
                    {
                        for (int k = 0; k < choicesCount; k++)
                        {
                            if (i != k && j != k)
                            {
                                strongestPaths[j, k] = Math.Max(strongestPaths[j, k], Math.Min(strongestPaths[j, i], strongestPaths[i, k]));
                            }
                        }
                    }
                }
            }

            return strongestPaths;
        }

        /// <summary>
        /// Gets the winning paths - The strongest of the strongest paths, for each pair option.
        /// </summary>
        /// <param name="strongestPaths">The strongest paths.</param>
        /// <param name="choicesCount">The choices count (size of table).</param>
        /// <returns>Returns a table with the winning choices of the strongest paths.</returns>
        private int[,] GetWinningPaths(int[,] strongestPaths, int choicesCount)
        {
            int[,] winningPaths = new int[choicesCount, choicesCount];

            for (int i = 0; i < choicesCount; i++)
            {
                for (int j = 0; j < choicesCount; j++)
                {
                    if (i != j)
                    {
                        if (strongestPaths[i, j] >= strongestPaths[j, i])
                        {
                            winningPaths[i, j] = strongestPaths[i, j];
                        }
                        else
                        {
                            winningPaths[i, j] = 0;
                        }
                    }
                }
            }

            return winningPaths;
        }

        /// <summary>
        /// Gets the winning options in order of preference, based on the winning paths.
        /// </summary>
        /// <param name="winningPaths">The winning paths.</param>
        /// <param name="listOfChoices">The list of choices.</param>
        /// <returns>Returns a list of </returns>
        private List<string> GetResultsInOrder(int[,] winningPaths, List<string> listOfChoices)
        {
            int count = listOfChoices.Count;

            var availableIndexes = Enumerable.Range(0, count);

            var pathCounts = from index in availableIndexes
                             select new {
                                 Index = index,
                                 Choice = listOfChoices[index],
                                 Count = GetPositivePathCount(winningPaths, index, count),
                                 Sum = GetPathSum(winningPaths, index, count)
                             };

            var orderPaths = pathCounts.OrderByDescending(p => p.Count).ThenByDescending(p => p.Sum).ThenBy(p => p.Choice);

            foreach (var path in orderPaths)
            {
                Debug.WriteLine($"- {path.Choice} [{path.Count}/{path.Sum}]");
            }

            var res = orderPaths.Select(r => listOfChoices[r.Index]).ToList();

            return res;
        }
        #endregion

        #region Small Utility        
        /// <summary>
        /// Gets all choices from all user votes.
        /// </summary>
        /// <param name="task">The collection of user votes.</param>
        /// <returns>Returns a list of all the choices in the task.</returns>
        private List<string> GetAllChoices(GroupedVotesByTask task)
        {
            HashSet<string> choices = new HashSet<string>();

            var res = from vote in task
                      group vote by VoteString.GetVoteContent(vote.Key) into votes
                      select votes.Key;

            return res.ToList();
        }

        /// <summary>
        /// Gets an indexer lookup for the list of choices, so it doesn't have to do
        /// sequential lookups each time..
        /// </summary>
        /// <param name="listOfChoices">The list of choices.</param>
        /// <returns>Returns a dictionary of choices vs list index.</returns>
        private Dictionary<string, int> GetChoicesIndexes(RankResults listOfChoices)
        {
            Dictionary<string, int> choiceIndexes = new Dictionary<string, int>();
            int index = 0;
            foreach (var choice in listOfChoices)
            {
                choiceIndexes[choice] = index++;
            }

            return choiceIndexes;
        }

        /// <summary>
        /// Gets the number of paths in the table with a value greater than 0.
        /// </summary>
        /// <param name="paths">The paths table.</param>
        /// <param name="row">The row.</param>
        /// <param name="count">The size of the table.</param>
        /// <returns>Returns a count of the number of positive path strength values.</returns>
        private int GetPositivePathCount(int[,] paths, int row, int count)
        {
            int pathCount = 0;

            for (int i = 0; i < count; i++)
            {
                if (paths[row, i] > 0)
                    pathCount++;
            }

            return pathCount;
        }

        /// <summary>
        /// Gets the sum of the path strength for a given table row.
        /// </summary>
        /// <param name="paths">The paths table.</param>
        /// <param name="row">The row.</param>
        /// <param name="count">The size of the table.</param>
        /// <returns>Returns the sum of the given path.</returns>
        private int GetPathSum(int[,] paths, int row, int count)
        {
            int pathSum = 0;

            for (int i = 0; i < count; i++)
            {
                pathSum += paths[row, i];
            }

            return pathSum;
        }
        #endregion

    }
}
