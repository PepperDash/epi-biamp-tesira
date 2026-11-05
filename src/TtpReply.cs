#nullable disable
using System;
using System.Text.RegularExpressions;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Recognizes TTP error replies. Besides <c>-ERR ...</c>, the Tesira answers some commands with
    /// a bare error token: <c>-CANNOT_DELIVER</c> when the block's unit is offline, rebooting or not
    /// running the design, and <c>-GENERAL_FAILURE</c>. Each of these is the reply to the outstanding
    /// command, so the queue must treat it as one; otherwise it waits out the command timeout and
    /// reconnects.
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested.</remarks>
    public static class TtpReply
    {
        /// <summary>The reply to a command for a block whose Tesira unit can't be reached.</summary>
        public const string CannotDeliver = "-CANNOT_DELIVER";

        // A minus sign, then an upper-case token (-ERR, -CANNOT_DELIVER, -GENERAL_FAILURE, ...), then
        // the end of the line or a separator. Command echoes start with a quoted instance tag or
        // DEVICE, and publishes with "! ", so neither matches.
        private static readonly Regex ErrorToken = new Regex(@"^(-[A-Z][A-Z0-9_]*)(?=$|[\s:])", RegexOptions.Compiled);

        /// <summary>True for a TTP error reply: <c>-ERR ...</c> or any other <c>-TOKEN</c>.</summary>
        public static bool IsError(string line) => line != null && ErrorToken.IsMatch(line);

        /// <summary>True when the block's unit is unavailable (<c>-CANNOT_DELIVER</c>, as a whole token).</summary>
        public static bool IsUnavailable(string line)
        {
            if (line == null) return false;
            var match = ErrorToken.Match(line);
            return match.Success && string.Equals(match.Groups[1].Value, CannotDeliver, StringComparison.Ordinal);
        }
    }
}
