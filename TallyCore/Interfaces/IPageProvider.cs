﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using HtmlAgilityPack;

namespace NetTally
{
    public enum Caching
    {
        UseCache,
        BypassCache
    }


    public interface IPageProvider
    {
        /// <summary>
        /// Asynchronously load pages for the specified quest.
        /// </summary>
        /// <param name="quest">The quest object describing which pages to load.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Returns a list of HTML documents defined by the requested quest.</returns>
        Task<List<HtmlDocument>> LoadPages(IQuest quest, CancellationToken token);

        /// <summary>
        /// Asynchronously load a specific page.
        /// </summary>
        /// <param name="url">The URL of the page to load.</param>
        /// <param name="shortDescrip">A short description that can be used in status updates.</param>
        /// <param name="bypassCache">Flag for whether to bypass the cache when trying to load the page.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Returns an HTML document.</returns>
        Task<HtmlDocument> GetPage(string url, string shortDescription, Caching caching, CancellationToken token);

        Task<HtmlDocument> GetPageDocument(string url, string shortDescription, Caching caching, CancellationToken token);
        Task<string> GetPageContents(string url, string shortDescription, CancellationToken token);

        /// <summary>
        /// Clear the cache of any previously loaded pages.
        /// </summary>
        void ClearPageCache();

        /// <summary>
        /// Have an event that can be watched for status messages.
        /// </summary>
        event EventHandler<MessageEventArgs> StatusChanged;
    }
}
