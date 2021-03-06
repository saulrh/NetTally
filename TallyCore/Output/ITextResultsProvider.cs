﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetTally
{
    public interface ITextResultsProvider
    {
        /// <summary>
        /// Public function to generate the full output for the tally.
        /// </summary>
        /// <param name="displayMode">The mode requested for how to format the output.</param>
        /// <returns>Returns the full string to be displayed.</returns>
        string BuildOutput(DisplayMode displayMode);
    }
}
