#nullable disable
using System;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Parses a Logic Selector's "states" value, one boolean per channel: "[false true false false]".
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested.</remarks>
    internal static class LogicSelectorStates
    {
        /// <summary>
        /// The 1-based channel that is true, or 0 when none is. The block interlocks, so at most one is
        /// true; if more are, the first wins.
        /// </summary>
        public static int ActiveChannel(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;

            var tokens = value.Trim().TrimStart('[').TrimEnd(']')
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < tokens.Length; i++)
            {
                if (string.Equals(tokens[i], "true", StringComparison.OrdinalIgnoreCase)) return i + 1;
            }
            return 0;
        }
    }
}
