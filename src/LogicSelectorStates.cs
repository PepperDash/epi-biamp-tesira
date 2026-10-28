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
            var tokens = Tokens(value);
            for (var i = 0; i < tokens.Length; i++)
            {
                if (string.Equals(tokens[i], "true", StringComparison.OrdinalIgnoreCase)) return i + 1;
            }
            return 0;
        }

        /// <summary>The number of channels in the value; 0 if it can't be read.</summary>
        public static int ChannelCount(string value) => Tokens(value).Length;

        private static string[] Tokens(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new string[0];
            return value.Trim().TrimStart('[').TrimEnd(']')
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
