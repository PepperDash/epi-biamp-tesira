#nullable disable
using System;
using System.Text.RegularExpressions;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Recognizes TTP error replies. Besides <c>-ERR ...</c>, the Tesira answers some commands with
    /// a bare error token: <c>CANNOT_DELIVER</c> when the block's unit is offline, rebooting or not
    /// running the design, and <c>GENERAL_FAILURE</c>. Each of these is the reply to the outstanding
    /// command, so the queue must treat it as one; otherwise it waits out the command timeout and
    /// reconnects.
    /// Firmware differs on the sign: 5.6.1.2 sends <c>CANNOT_DELIVER</c> with no leading minus
    /// (captured from a 3-unit system with one unit off the network), so both forms are accepted.
    /// Only the known bare tokens are accepted without the minus, because command echoes such as
    /// <c>SESSION get aliases</c> and <c>DEVICE get version</c> also start with an upper-case word.
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested.</remarks>
    public static class TtpReply
    {
        /// <summary>
        /// The token the Tesira replies with for a block whose unit can't be reached, without its
        /// optional leading minus (see the class remarks).
        /// </summary>
        public const string CannotDeliver = "CANNOT_DELIVER";

        // Either a minus sign and any upper-case token (-ERR, -CANNOT_DELIVER, -GENERAL_FAILURE, ...),
        // or one of the known tokens with no minus (CANNOT_DELIVER, GENERAL_FAILURE), then the end of
        // the line or a separator. The token name, without the minus, is in group "token". Command
        // echoes start with a quoted instance tag or a keyword such as DEVICE or SESSION, and
        // publishes with "! ", so none of them match.
        private static readonly Regex ErrorToken = new Regex(
            @"^(?:-(?<token>[A-Z][A-Z0-9_]*)|(?<token>CANNOT_DELIVER|GENERAL_FAILURE))(?=$|[\s:])",
            RegexOptions.Compiled);

        // The instance tag a command addresses: quoted ("Level 1" set level 1 0) or a bare first word.
        private static readonly Regex Target = new Regex(@"^\s*(?:""([^""]*)""|(\S+))", RegexOptions.Compiled);

        /// <summary>
        /// The instance tag a TTP command addresses (its block), e.g. <c>Level1</c> for
        /// <c>"Level1" get level 1</c>, or empty if there is none.
        /// </summary>
        public static string TargetOf(string command)
        {
            if (string.IsNullOrEmpty(command)) return string.Empty;
            var match = Target.Match(command);
            if (!match.Success) return string.Empty;
            return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        }

        /// <summary>
        /// The verb of a TTP command (<c>get</c>, <c>set</c>, <c>subscribe</c> ...): the word after
        /// its instance tag, or empty if there is none.
        /// </summary>
        public static string VerbOf(string command)
        {
            if (string.IsNullOrEmpty(command)) return string.Empty;
            var match = Target.Match(command);
            if (!match.Success) return string.Empty;
            var rest = command.Substring(match.Index + match.Length).TrimStart();
            var end = rest.IndexOfAny(new[] { ' ', '\t' });
            return end < 0 ? rest : rest.Substring(0, end);
        }

        /// <summary>True for a reply that confirms a subscription or read: <c>+OK ...</c> or <c>-ERR ALREADY_SUBSCRIBED</c>.</summary>
        public static bool IsSuccess(string line) =>
            line != null && (line.StartsWith("+OK", StringComparison.Ordinal)
                             || line.IndexOf("ALREADY_SUBSCRIBED", StringComparison.Ordinal) >= 0);

        /// <summary>
        /// True for a TTP error reply: <c>-ERR ...</c>, any other <c>-TOKEN</c>, or a bare
        /// <c>CANNOT_DELIVER</c> / <c>GENERAL_FAILURE</c>.
        /// </summary>
        public static bool IsError(string line) => line != null && ErrorToken.IsMatch(line);

        /// <summary>
        /// True when the block's unit is unavailable: <c>CANNOT_DELIVER</c> or <c>-CANNOT_DELIVER</c>,
        /// as a whole token.
        /// </summary>
        public static bool IsUnavailable(string line)
        {
            if (line == null) return false;
            var match = ErrorToken.Match(line);
            return match.Success && string.Equals(match.Groups["token"].Value, CannotDeliver, StringComparison.Ordinal);
        }
    }
}
