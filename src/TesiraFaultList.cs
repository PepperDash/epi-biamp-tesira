#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// One active fault inside a fault indicator, e.g. FAULT_DANTE_FLOW_INACTIVE.
    /// </summary>
    public class TesiraFault
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Instance { get; set; }
    }

    /// <summary>
    /// One fault indicator from 'DEVICE get activeFaultList', e.g. INDICATOR_MAJOR_IN_DEVICE.
    /// </summary>
    public class TesiraFaultIndicator
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<TesiraFault> Faults { get; } = new List<TesiraFault>();

        /// <summary>
        /// 0 none, 1 minor, 2 major, 3 critical, from the indicator id (verbose replies) or its name
        /// (terse replies only carry a number). An unrecognised indicator with faults counts as
        /// minor, so a fault is never reported as no fault.
        /// </summary>
        public int Severity
        {
            get
            {
                var severity = SeverityOf(Id);
                if (severity < 0) severity = SeverityOf(Name);
                if (severity < 0) severity = Faults.Count > 0 ? 1 : 0;
                return severity;
            }
        }

        private static int SeverityOf(string text)
        {
            if (string.IsNullOrEmpty(text)) return -1;
            if (text.IndexOf("CRITICAL", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
            if (text.IndexOf("MAJOR", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
            if (text.IndexOf("MINOR", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
            if (text.IndexOf("NONE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("NO FAULT", StringComparison.OrdinalIgnoreCase) >= 0) return 0;
            return -1;
        }

        /// <summary>
        /// Builds the verbose-style id (INDICATOR_MAJOR_IN_DEVICE) from a terse reply's indicator name
        /// ("Major Fault in Device"), so FaultId reads the same in either session mode. Null if the
        /// name doesn't say both the severity and the scope.
        /// </summary>
        internal static string IdFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string level;
            switch (SeverityOf(name))
            {
                case 3: level = "CRITICAL"; break;
                case 2: level = "MAJOR"; break;
                case 1: level = "MINOR"; break;
                case 0: level = "NONE"; break;
                default: return null;
            }
            string scope;
            if (name.IndexOf("SYSTEM", StringComparison.OrdinalIgnoreCase) >= 0) scope = "SYSTEM";
            else if (name.IndexOf("DEVICE", StringComparison.OrdinalIgnoreCase) >= 0) scope = "DEVICE";
            else return null;
            return "INDICATOR_" + level + "_IN_" + scope;
        }
    }

    /// <summary>
    /// The parsed reply to 'DEVICE get activeFaultList' and the summary values the plugin reports.
    /// Pure logic (no Crestron types) so it can be unit tested.
    /// </summary>
    public class TesiraFaultList
    {
        public List<TesiraFaultIndicator> Indicators { get; } = new List<TesiraFaultIndicator>();

        /// <summary>Total active faults across all indicators.</summary>
        public int ActiveFaultCount => Indicators.Sum(i => i.Faults.Count);

        /// <summary>The most severe indicator; the first one on a tie (device before system).</summary>
        public TesiraFaultIndicator WorstIndicator
        {
            get
            {
                TesiraFaultIndicator worst = null;
                foreach (var indicator in Indicators)
                {
                    if (worst == null || indicator.Severity > worst.Severity) worst = indicator;
                }
                return worst;
            }
        }

        public int Severity => WorstIndicator?.Severity ?? 0;

        public bool HasFault => ActiveFaultCount > 0 || Severity > 0;

        public string FaultId => WorstIndicator?.Id ?? string.Empty;

        public string FaultName => WorstIndicator?.Name ?? string.Empty;

        /// <summary>The active faults' names, de-duplicated, in reply order, comma-separated.</summary>
        public string FaultListText => string.Join(", ",
            Indicators.SelectMany(i => i.Faults).Select(f => string.IsNullOrEmpty(f.Name) ? f.Id : f.Name)
                .Where(n => !string.IsNullOrEmpty(n)).Distinct());

        /// <summary>A stable signature of the active state, used to log only on change.</summary>
        public string Signature => FaultId + "|" + string.Join(",",
            Indicators.SelectMany(i => i.Faults).Select(f => f.Id + "/" + f.Instance));

        /// <summary>
        /// Parses the reply to 'DEVICE get activeFaultList' in either TTP session mode:
        /// verbose  +OK "value":[{"id":INDICATOR_MAJOR_IN_DEVICE "name":"..." "faults":[{"id":... "name":"..." "instance":"..."}] "serialNumber":"..."} ...]
        /// terse    +OK [[1 "Major Fault in Device" [[86 "one or more Dante flows inactive" "DEFAULT"]] "05398476"] ...]
        /// Returns null if the reply isn't a +OK with a value list.
        /// </summary>
        public static TesiraFaultList Parse(string reply)
        {
            if (string.IsNullOrEmpty(reply)) return null;
            var ok = reply.IndexOf("+OK", StringComparison.Ordinal);
            if (ok < 0) return null;

            var start = ok + 3;
            var valueKey = reply.IndexOf("\"value\":", start, StringComparison.Ordinal);
            if (valueKey >= 0) start = valueKey + "\"value\":".Length;

            var reader = new TtpValueReader(reply, start);
            var list = reader.ReadValue() as List<object>;
            if (list == null) return null;

            var result = new TesiraFaultList();
            foreach (var item in list)
            {
                var indicator = ReadIndicator(item);
                if (indicator != null) result.Indicators.Add(indicator);
            }
            return result;
        }

        private static TesiraFaultIndicator ReadIndicator(object item)
        {
            var indicator = new TesiraFaultIndicator();
            object faults;

            if (item is Dictionary<string, object> d)
            {
                indicator.Id = Str(d, "id");
                indicator.Name = Str(d, "name");
                d.TryGetValue("faults", out faults);
            }
            else if (item is List<object> l)
            {
                // terse: [id name [faults] serialNumber]; the id is a number, so name the indicator from its text
                indicator.Name = At(l, 1);
                indicator.Id = TesiraFaultIndicator.IdFromName(indicator.Name) ?? At(l, 0);
                faults = l.Count > 2 ? l[2] : null;
            }
            else
            {
                return null;
            }

            if (faults is List<object> faultItems)
            {
                foreach (var f in faultItems)
                {
                    if (f is Dictionary<string, object> fd)
                        indicator.Faults.Add(new TesiraFault { Id = Str(fd, "id"), Name = Str(fd, "name"), Instance = Str(fd, "instance") });
                    else if (f is List<object> fl)
                        indicator.Faults.Add(new TesiraFault { Id = At(fl, 0), Name = At(fl, 1), Instance = At(fl, 2) });
                }
            }
            return indicator;
        }

        private static string Str(Dictionary<string, object> d, string key)
        {
            return d.TryGetValue(key, out var v) && v != null ? v.ToString() : string.Empty;
        }

        private static string At(List<object> l, int index)
        {
            return index < l.Count && l[index] != null && !(l[index] is List<object>) ? l[index].ToString() : string.Empty;
        }

        /// <summary>
        /// Reads a TTP value: objects {"key":value ...}, lists [v v ...], quoted strings and bare
        /// tokens (enums, numbers, true/false). Separators are spaces; commas are tolerated.
        /// </summary>
        private sealed class TtpValueReader
        {
            private readonly string s;
            private int pos;

            public TtpValueReader(string text, int start)
            {
                s = text;
                pos = start;
            }

            public object ReadValue()
            {
                SkipSeparators();
                if (pos >= s.Length) return null;
                switch (s[pos])
                {
                    case '{': return ReadObject();
                    case '[': return ReadList();
                    case '"': return ReadString();
                    default: return ReadToken();
                }
            }

            private Dictionary<string, object> ReadObject()
            {
                var d = new Dictionary<string, object>(StringComparer.Ordinal);
                pos++; // {
                while (true)
                {
                    SkipSeparators();
                    if (pos >= s.Length) return d;
                    if (s[pos] == '}') { pos++; return d; }
                    if (s[pos] != '"') { ReadToken(); continue; } // malformed: skip
                    var key = ReadString();
                    SkipSeparators();
                    if (pos < s.Length && s[pos] == ':') pos++;
                    d[key] = ReadValue();
                }
            }

            private List<object> ReadList()
            {
                var l = new List<object>();
                pos++; // [
                while (true)
                {
                    SkipSeparators();
                    if (pos >= s.Length) return l;
                    if (s[pos] == ']') { pos++; return l; }
                    l.Add(ReadValue());
                }
            }

            private string ReadString()
            {
                var sb = new StringBuilder();
                pos++; // opening quote
                while (pos < s.Length)
                {
                    var c = s[pos++];
                    if (c == '\\' && pos < s.Length) { sb.Append(s[pos++]); continue; }
                    if (c == '"') break;
                    sb.Append(c);
                }
                return sb.ToString();
            }

            private string ReadToken()
            {
                var start = pos;
                while (pos < s.Length && !char.IsWhiteSpace(s[pos]) && s[pos] != ',' &&
                       s[pos] != '}' && s[pos] != ']' && s[pos] != ':')
                {
                    pos++;
                }
                if (pos == start) pos++; // never stall on an unexpected character
                return s.Substring(start, pos - start);
            }

            private void SkipSeparators()
            {
                while (pos < s.Length && (char.IsWhiteSpace(s[pos]) || s[pos] == ',')) pos++;
            }
        }
    }
}
