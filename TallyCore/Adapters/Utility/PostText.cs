﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using NetTally.Utility;

namespace NetTally.Adapters
{
    /// <summary>
    /// Class used for extracting usable text out of the raw HTML of a web post.
    /// </summary>
    public static class PostText
    {
        // Regex for colors in a span's style
        static readonly Regex spanColorRegex = new Regex(@"\bcolor\s*:\s*(?<color>#[0-9a-f]+|\w+)", RegexOptions.IgnoreCase);
        // Regex for strike-through in a span's style
        static readonly Regex spanStrikeRegex = new Regex(@"text-decoration:\s*line-through", RegexOptions.IgnoreCase);

        #region Public Functions
        /// <summary>
        /// Clean up problematic bits of text in the extracted HTML string.
        /// </summary>
        /// <param name="text">The text pulled from an HTML page.</param>
        /// <returns>Returns a cleaned version of the post text.</returns>
        public static string CleanupWebString(string text)
        {
            if (text == null)
                return string.Empty;

            text = text.TrimStart();

            text = HtmlEntity.DeEntitize(text);

            return StringUtility.SafeString(text);
        }

        /// <summary>
        /// Extract the text contents of a post, given a starting HTML node.
        /// </summary>
        /// <param name="node">The parent node containing the entirety of a post.  Cannot be null.</param>
        /// <param name="exclude">A predicate that can be used to exclude specific
        /// sub-nodes from the end result.  A default is used if none is provided.</param>
        /// <returns>Returns a cleaned version of the text of the post.</returns>
        /// <exception cref="ArgumentNullException">If node is null.</exception>
        public static string ExtractPostText(HtmlNode node, Predicate<HtmlNode> exclude)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            // If no exclusion is provided, no nodes are removed.
            if (exclude == null)
                exclude = (n) => false;

            // Recurse into the child nodes of the main post node.
            string postText = ExtractPostTextString(node, exclude).ToString().Trim();

            // Cleanup the results of the extraction.
            return CleanupWebString(postText);
        }

        /// <summary>
        /// A helper function to create a predicate that excludes a specific class name.
        /// </summary>
        /// <param name="className">The class name to exclude.</param>
        /// <returns>Returns a predicate.</returns>
        public static Predicate<HtmlNode> GetClassExclusionPredicate(string className)
        {
            return (HtmlNode n) =>
            {
                var nodeClasses = n.GetAttributeValue("class", "").Split(' ');
                return nodeClasses.Contains(className, StringComparer.OrdinalIgnoreCase);
            };
        }

        /// <summary>
        /// A helper function to create a predicate that excludes a list of class names.
        /// </summary>
        /// <param name="classNames">The class names to exclude.</param>
        /// <returns>Returns a predicate.</returns>
        public static Predicate<HtmlNode> GetClassesExclusionPredicate(List<string> classNames)
        {
            return (HtmlNode n) =>
            {
                var nodeClasses = n.GetAttributeValue("class", "").Split(' ');
                return classNames.Any(p => nodeClasses.Contains(p, StringComparer.OrdinalIgnoreCase));
            };

        }

        #endregion

        #region Private Support Functions
        /// <summary>
        /// Extracts post text as a string from the provided HTML node.
        /// Creates a new string builder to call the full version of this function.
        /// </summary>
        /// <param name="node">The starting HTML node.</param>
        /// <param name="exclude">A predicate to exclude processing of further nodes.</param>
        /// <returns>Returns the text contents of the post.</returns>
        private static StringBuilder ExtractPostTextString(HtmlNode node, Predicate<HtmlNode> exclude) => ExtractPostTextString(node, exclude, new StringBuilder());

        /// <summary>
        /// Extracts the text (recursively) from the specified node, and converts some elements into BBCode.
        /// </summary>
        /// <param name="node">The parent node.</param>
        /// <param name="exclude">A predicate that can be used to exclude specific
        /// sub-nodes from the end result.</param>
        /// <param name="sb">The stringbuilder where all results are concatenated.</param>
        /// <returns>Returns a StringBuilder containing the results of converting the HTML to text (with possible BBCode).</returns>
        private static StringBuilder ExtractPostTextString(HtmlNode node, Predicate<HtmlNode> exclude, StringBuilder sb)
        {
            System.Diagnostics.Debug.Assert(node != null);
            System.Diagnostics.Debug.Assert(exclude != null);
            System.Diagnostics.Debug.Assert(sb != null);

            foreach (var child in node.ChildNodes)
            {
                if (exclude(child))
                {
                    continue;
                }

                switch (child.Name)
                {
                    case "#text":
                        sb.Append(child.InnerText);
                        break;
                    case "br":
                        sb.Append("\r\n");
                        break;
                    case "i":
                        sb.Append("『i』");
                        ExtractPostTextString(child, exclude, sb);
                        sb.Append("『/i』");
                        break;
                    case "b":
                        sb.Append("『b』");
                        ExtractPostTextString(child, exclude, sb);
                        sb.Append("『/b』");
                        break;
                    case "u":
                        sb.Append("『u』");
                        ExtractPostTextString(child, exclude, sb);
                        sb.Append("『/u』");
                        break;
                    case "span":
                        string spanStyle = child.GetAttributeValue("style", "");

                        // Struck-through text is entirely skipped.
                        if (spanStrikeRegex.Match(spanStyle).Success)
                        {
                            continue;
                        }
                        else
                        {
                            // Keep any COLOR styles.
                            Match m = spanColorRegex.Match(spanStyle);
                            if (m.Success)
                            {
                                sb.Append($"『color={m.Groups["color"].Value}』");
                                ExtractPostTextString(child, exclude, sb);
                                sb.Append("『/color』");
                            }
                            else
                            {
                                // Take anything else without including span style modifications.
                                ExtractPostTextString(child, exclude, sb);
                            }
                        }
                        break;
                    case "a":
                        sb.Append($"『url=\"{child.GetAttributeValue("href", "")}\"』");
                        ExtractPostTextString(child, exclude, sb);
                        sb.Append("『/url』");
                        break;
                    case "img":
                        string srcUrl = child.GetAttributeValue("data-url", "");
                        if (string.IsNullOrEmpty(srcUrl))
                            srcUrl = child.GetAttributeValue("src", "");

                        // MCE sprite smilies do not use actual images, so don't include them.
                        if (child.GetAttributeValue("class", "").Contains("mceSmilieSprite"))
                            break;

                        if (!string.IsNullOrEmpty(srcUrl))
                        {
                            sb.Append($"『url=\"{srcUrl}\"』<Image>『/url』");
                        }
                        break;
                    case "div":
                        // Recurse into divs (typically spoilers).
                        ExtractPostTextString(child, exclude, sb);
                        sb.Append("\r\n");
                        break;
                }
            }

            return sb;
        }
        #endregion
    }
}
