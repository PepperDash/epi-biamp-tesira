using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Expander
{
    public class TesiraExpanderTracker : TesiraDspControlPoint, ICommunicationMonitor
    {

        private const string keyFormatter = "{0}--{1}";
        public List<TesiraExpanderData> Expanders = new List<TesiraExpanderData>();

        public Dictionary<int, StringFeedback> Hostnames = new Dictionary<int, StringFeedback>();
        public Dictionary<int, StringFeedback> SerialNumbers = new Dictionary<int, StringFeedback>();
        public Dictionary<int, StringFeedback> Firmwares = new Dictionary<int, StringFeedback>();
        public Dictionary<int, StringFeedback> MacAddresses = new Dictionary<int, StringFeedback>();
        public Dictionary<int, BoolFeedback> OnlineStatuses = new Dictionary<int, BoolFeedback>();

        public StatusMonitorBase CommunicationMonitor { get; private set; }


        public TesiraExpanderTracker(TesiraDsp parent, Dictionary<string, TesiraExpanderBlockConfig> expanders)
            : base("", "", 0, 0, parent, string.Format(keyFormatter, parent.Key, "Expanders"), "Expanders", 0)
        {
            CommunicationMonitor = new GenericCommunicationMonitor(this, Parent.Communication, 45000, 90000, 180000, CheckTracker);

            foreach (var tesiraExpanderBlockConfig in expanders)
            {
                var key = tesiraExpanderBlockConfig.Value.Index;
                var value = tesiraExpanderBlockConfig.Value.Hostname;
                var expander = new TesiraExpanderData(value, key, this, CheckTracker);
                Expanders.Add(expander);
                var h = new StringFeedback("hostName", () => expander.Hostname);
                var s = new StringFeedback("serialNumber", () => expander.SerialNumber);
                var f = new StringFeedback("firmware", () => expander.Firmware);
                var m = new StringFeedback("macAddress", () => expander.MacAddress);
                var o = new BoolFeedback("online", () => expander.Online);

                Hostnames.Add(key, h);
                SerialNumbers.Add(key, s);
                Firmwares.Add(key, f);
                MacAddresses.Add(key, m);
                OnlineStatuses.Add(key, o);

                Feedbacks.Add(h);
                Feedbacks.Add(s);
                Feedbacks.Add(f);
                Feedbacks.Add(m);
                Feedbacks.Add(o);

                DeviceManager.AddDevice(expander);

            }
            this.LogVerbose("There are {count} configured expanders", Expanders.Count);


            foreach (var f in Hostnames.Select(feedback => feedback.Value))
            {
                Feedbacks.Add(f);
            }

            foreach (var f in SerialNumbers.Select(feedback => feedback.Value))
            {
                Feedbacks.Add(f);
            }

            foreach (var f in Firmwares.Select(feedback => feedback.Value))
            {
                Feedbacks.Add(f);
            }

            foreach (var f in MacAddresses.Select(feedback => feedback.Value))
            {
                Feedbacks.Add(f);
            }

            foreach (var f in OnlineStatuses.Select(feedback => feedback.Value))
            {
                Feedbacks.Add(f);
            }

            foreach (var item in Expanders)
            {
                this.LogVerbose("Expander Index = {index} ; Expander Hostname = {hostname}", item.Index, item.Hostname);
            }
        }

        public override void Initialize()
        {
            CheckTracker();
        }


        private void CheckTracker()
        {
            this.LogVerbose("Getting DiscoveredExpanders");

            SendFullCommand("get", "discoveredExpanders", null, 999);
        }

        const string pattern = @"\[([^\[\]]+)\]?";
        private readonly static Regex regex1 = new Regex(pattern);
        const string pattern2 = "\\\"([^\\\"\\\"]+)\\\"?";
        private readonly static Regex regex2 = new Regex(pattern2);



        public override void ParseGetMessage(string attributeCode, string message)
        {
            this.LogVerbose("!!!!!!!!EXPANDER DATA!!!!!!!!!!!");

            var matches = regex1.Matches(message);

            this.LogVerbose("There are {matchCount} Matches", matches.Count);
            for (var v = 0; v < matches.Count; v++)
            {
                if (!matches[v].ToString().Contains('"')) continue;
                this.LogVerbose("Match {matchIndex} is a device", v);

                var matchesEnclosed = regex2.Matches(matches[v].ToString());
                var data2 = regex2.Replace(matches[v].ToString(), "").Trim('"').Trim('[').Trim().Replace("  ", " ");
                Console.WriteLine("Data2 = {0}", data2);
                var hostname = matchesEnclosed[0].ToString().Trim('"');

                this.LogVerbose("Match {matchIndex} Hostname : {hostname}", v, hostname);

                var newData = Expanders.FirstOrDefault(o => String.Equals(o.Hostname, hostname, StringComparison.CurrentCultureIgnoreCase));

                if (newData == null) continue;
                this.LogVerbose("Found a device Index {deviceIndex} with Hostname {hostname}", newData.Index, newData.Hostname);
                var macData = matches[v + 1].ToString();
                var otherData = matches[v].ToString();
                newData.SetData(otherData, macData);
            }

            foreach (var feedback in Feedbacks)
            {
                var f = feedback;
                f.FireUpdate();
            }

            foreach (var device in Expanders)
            {
                var i = device;

                this.LogVerbose("Index = {index}", i.Index);
                this.LogVerbose("Hostname = {hostname}", i.Hostname);
                this.LogVerbose("MacAddress = {macAddress}", i.MacAddress);
                this.LogVerbose("SerialNumber = {serialNumber}", i.SerialNumber);
                this.LogVerbose("Firmware = {firmware}", i.Firmware);
                this.LogVerbose("Online = {online}", i.Online);
            }
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var expanderJoinMap = new TesiraExpanderJoinMap(joinStart);


            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                expanderJoinMap = JsonConvert.DeserializeObject<TesiraExpanderJoinMap>(joinMapSerialized);


            var expanderJoinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(expanderJoinMapSerialized))
            {
                expanderJoinMap =
                    JsonConvert.DeserializeObject<TesiraExpanderJoinMap>(expanderJoinMapSerialized);
            }

            bridge?.AddJoinMap(Key, expanderJoinMap);

            this.LogDebug("Linking to Trilist {trilist:X}", trilist.ID);

            foreach (var item in Expanders)
            {
                if (item == null) return;

                var expander = item;
                var index = expander.Index;
                var offset = 5 * (index - 1);


                Hostnames[index].LinkInputSig(trilist.StringInput[(uint)(expanderJoinMap.Hostname.JoinNumber + offset)]);
                SerialNumbers[index].LinkInputSig(trilist.StringInput[(uint)(expanderJoinMap.SerialNumber.JoinNumber + offset)]);
                Firmwares[index].LinkInputSig(trilist.StringInput[(uint)(expanderJoinMap.Firmware.JoinNumber + offset)]);
                MacAddresses[index].LinkInputSig(trilist.StringInput[(uint)(expanderJoinMap.MacAddress.JoinNumber + offset)]);
                OnlineStatuses[index].LinkInputSig(trilist.BooleanInput[(uint)(expanderJoinMap.IsOnline.JoinNumber + offset)]);
            }

            trilist.OnlineStatusChange += (d, args) =>
            {
                if (!args.DeviceOnLine) return;

                foreach (var feedback in Feedbacks)
                {
                    feedback.FireUpdate();
                }

            };
        }
    }
}


