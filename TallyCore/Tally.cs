﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using NetTally.Output;
using NetTally.Web;

namespace NetTally
{
    public class Tally : INotifyPropertyChanged, IDisposable
    {
        bool _disposed = false;
        IPageProvider PageProvider { get; } = new WebPageProvider2();
        public IVoteCounter VoteCounter { get; } = new VoteCounter();
        public ITextResultsProvider TextResults { get; set; } = new TallyOutput();

        bool tallyIsRunning = false;
        string results = string.Empty;
        DisplayMode displayMode = DisplayMode.Normal;

        IQuest lastTallyQuest = null;

        public HashSet<string> UserDefinedTasks { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public Tally()
            : this(null, null)
        {
        }

        public Tally(IVoteCounter altVoteCounter, IPageProvider altPageProvider)
        {
            if (altVoteCounter != null)
                VoteCounter = altVoteCounter;

            if (altPageProvider != null)
                PageProvider = altPageProvider;

            PageProvider.StatusChanged += PageProvider_StatusChanged;
        }

        ~Tally()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true); //I am calling you from Dispose, it's safe
            GC.SuppressFinalize(this); //Hey, GC: don't bother calling finalize later
        }

        protected virtual void Dispose(Boolean itIsSafeToAlsoFreeManagedObjects)
        {
            if (_disposed)
                return;

            if (itIsSafeToAlsoFreeManagedObjects)
            {
                PageProvider.Dispose();
            }

            _disposed = true;
        }


        #region Event handling
        /// <summary>
        /// Keep watch for any status messasges from the page provider, and add them
        /// to the TallyResults string so that they can be displayed in the UI.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PageProvider_StatusChanged(object sender, MessageEventArgs e)
        {
            TallyResults = TallyResults + e.Message;
        }

        /// <summary>
        /// Event for INotifyPropertyChanged.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Function to raise events when a property has been changed.
        /// </summary>
        /// <param name="propertyName">The name of the property that was modified.</param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Behavior properties
        /// <summary>
        /// The string containing the current tally progress or results.
        /// Creates a notification event if the contents change.
        /// </summary>
        public string TallyResults
        {
            get { return results; }
            set
            {
                results = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Enum of the type of display composition methodology to use for the output display.
        /// Recalculates the display if changed.
        /// </summary>
        public DisplayMode DisplayMode
        {
            get { return displayMode; }
            set
            {
                displayMode = value;
                ConstructResults(lastTallyQuest);
            }
        }

        /// <summary>
        /// Flag whether the tally is currently running.
        /// </summary>
        public bool TallyIsRunning
        {
            get { return tallyIsRunning; }
            set
            {
                tallyIsRunning = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Interface functions
        /// <summary>
        /// Run the tally for the specified quest.
        /// </summary>
        /// <param name="questTitle">The name of the quest thread to scan.</param>
        /// <param name="startPost">The starting post number.</param>
        /// <param name="endPost">The ending post number.</param>
        /// <returns></returns>
        public async Task Run(IQuest quest, CancellationToken token)
        {
            try
            {
                List<Task<HtmlDocument>> loadedPages;

                TallyIsRunning = true;
                TallyResults = string.Empty;

                if (lastTallyQuest?.DisplayName != quest.DisplayName)
                    UserDefinedTasks.Clear();
                lastTallyQuest = quest;

                await quest.InitForumAdapter(token).ConfigureAwait(false);

                // Figure out what page to start from
                var startInfo = await quest.GetStartInfo(PageProvider, token);

                // Load pages from the website
                loadedPages = await quest.LoadQuestPages(startInfo, PageProvider, token).ConfigureAwait(false);

                if (loadedPages != null && loadedPages.Count > 0)
                {
                    // Tally the votes from the loaded pages.
                    await VoteCounter.TallyVotes(quest, startInfo, loadedPages).ConfigureAwait(false);

                    // Compose the final result string from the compiled votes.
                    ConstructResults(quest);
                }

                GC.Collect();
            }
            catch (Exception)
            {
                lastTallyQuest = null;
                throw;
            }
            finally
            {
                TallyIsRunning = false;
                PageProvider.DoneLoading();
            }
        }

        /// <summary>
        /// Process the results of the tally through the vote counter, and update the output.
        /// </summary>
        /// <param name="changedQuest"></param>
        public void UpdateTally(IQuest changedQuest)
        {
            if (lastTallyQuest != null && changedQuest == lastTallyQuest)
            {
                // Tally the votes from the loaded pages.
                VoteCounter.TallyPosts(lastTallyQuest);

                // Compose the final result string from the compiled votes.
                ConstructResults(lastTallyQuest);
            }
        }

        /// <summary>
        /// Allow manual clearing of the page cache.
        /// </summary>
        public void ClearPageCache()
        {
            PageProvider.ClearPageCache();
        }

        /// <summary>
        /// Compose the tallied results into a string to put in the TallyResults property,
        /// for display in the UI.
        /// </summary>
        public void ConstructResults(IQuest quest)
        {
            if (quest == null)
                return;

            TallyResults = TextResults.BuildOutput(quest, VoteCounter, DisplayMode);
        }
        #endregion
    }
}
