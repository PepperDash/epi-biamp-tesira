#nullable disable
using System;
using System.Globalization;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// TTP strings and parsing for a Router block's per-output "input" attribute:
    /// <c>"Router1" set input 3 2</c> routes input 2 to output 3, and input 0 clears the output.
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested.</remarks>
    internal static class RouterProtocol
    {
        /// <summary>The attribute code tagged on a per-output get, so its "+OK n" reply names the output.</summary>
        public const string GetAttributePrefix = "input:";

        public static string SetCommand(string instanceTag, uint output, uint input) =>
            string.Format(CultureInfo.InvariantCulture, "\"{0}\" set input {1} {2}", instanceTag, output, input);

        public static string GetCommand(string instanceTag, uint output) =>
            string.Format(CultureInfo.InvariantCulture, "\"{0}\" get input {1}", instanceTag, output);

        public static string SubscribeCommand(string instanceTag, uint output, string subscriptionName, int responseRate) =>
            string.Format(CultureInfo.InvariantCulture, "\"{0}\" subscribe input {1} {2} {3}", instanceTag, output, subscriptionName, responseRate);

        public static string UnsubscribeCommand(string instanceTag, uint output, string subscriptionName) =>
            string.Format(CultureInfo.InvariantCulture, "\"{0}\" unsubscribe input {1} {2}", instanceTag, output, subscriptionName);

        /// <summary>The per-output subscription name: <c>Router1__Router-3</c> for output 3.</summary>
        public static string SubscriptionName(string baseName, uint output) =>
            string.Format(CultureInfo.InvariantCulture, "{0}-{1}", baseName, output);

        public static string GetAttributeCode(uint output) =>
            GetAttributePrefix + output.ToString(CultureInfo.InvariantCulture);

        /// <summary>Reads the output number from a per-output subscription name.</summary>
        public static bool TryParseSubscriptionOutput(string subscriptionName, string baseName, out uint output)
        {
            output = 0;
            if (string.IsNullOrEmpty(subscriptionName) || string.IsNullOrEmpty(baseName)) return false;

            var prefix = baseName + "-";
            if (!subscriptionName.StartsWith(prefix, StringComparison.Ordinal)) return false;

            return uint.TryParse(subscriptionName.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out output)
                && output > 0;
        }

        /// <summary>Reads the output number from a per-output get's attribute code.</summary>
        public static bool TryParseGetOutput(string attributeCode, out uint output)
        {
            output = 0;
            if (attributeCode == null || !attributeCode.StartsWith(GetAttributePrefix, StringComparison.Ordinal)) return false;

            return uint.TryParse(attributeCode.Substring(GetAttributePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out output)
                && output > 0;
        }

        /// <summary>
        /// Reads the routed input from a publish value ("2") or a get reply ("+OK 2"). 0 means cleared.
        /// </summary>
        public static bool TryParseInput(string value, out uint input)
        {
            input = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;

            var tokens = value.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var token = tokens[0] == "+OK" && tokens.Length > 1 ? tokens[1] : tokens[0];

            return uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out input);
        }
    }
}
