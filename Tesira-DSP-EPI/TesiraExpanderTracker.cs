using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;
using Crestron.SimplSharp;
using Newtonsoft.Json;
using System.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using Crestron.SimplSharpPro.DeviceSupport;
using Tesira_DSP_EPI.Bridge.JoinMaps;
using PepperDash.Essentials.Core.Bridges;
using System.Text.RegularExpressions;
using System.Text;

namespace Tesira_DSP_EPI
{
    public class TesiraExpanderTracker : TesiraDspControlPoint
    {
        private CTimer _expanderTimer;

        private const string KeyFormatter = "{0}--{1}";
        public List<TesiraExpanderData> Expanders = new List<TesiraExpanderData>(); 

        public Dictionary<int, StringFeedback> Hostnames = new Dictionary<int, StringFeedback>();
        public Dictionary<int, StringFeedback> SerialNumbers = new Dictionary<int, StringFeedback>();
        public Dictionary<int, StringFeedback> Firmwares = new Dictionary<int, StringFeedback>();
        public Dictionary<int, StringFeedback> MacAddresses = new Dictionary<int, StringFeedback>();
        public Dictionary<int, BoolFeedback> OnlineStatuses = new Dictionary<int, BoolFeedback>(); 

        public TesiraExpanderTracker(TesiraDsp parent, Dictionary<string, TesiraExpanderBlockConfig> expanders )
            : base("", "", 0, 0, parent, String.Format(KeyFormatter, parent.Key, "Expanders"), "Expanders", 0)
        {
            foreach (var tesiraExpanderBlockConfig in expanders)
            {
                var key = tesiraExpanderBlockConfig.Value.Index;
                var value = tesiraExpanderBlockConfig.Value.Hostname;
                var expander = new TesiraExpanderData(value, key);
                Expanders.Add(expander);
                var h = new StringFeedback(() => expander.Hostname);
                var s = new StringFeedback(() => expander.SerialNumber);
                var f = new StringFeedback(() => expander.Firmware);
                var m = new StringFeedback(() => expander.MacAddress);
                var o = new BoolFeedback(() => expander.Online);

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
            }

            foreach (var feedback in Hostnames)
            {
                var f = feedback.Value;
                Feedbacks.Add(f);
            }
            
            foreach (var feedback in SerialNumbers)
            {
                var f = feedback.Value;
                Feedbacks.Add(f);
            } 
            
            foreach (var feedback in Firmwares)
            {
                var f = feedback.Value;
                Feedbacks.Add(f);
            } 
            
            foreach (var feedback in MacAddresses)
            {
                var f = feedback.Value;
                Feedbacks.Add(f);
            } 
            
            foreach (var feedback in OnlineStatuses)
            {
                var f = feedback.Value;
                Feedbacks.Add(f);
            }
        }

        public override void Initialize()
        {
            CheckTracker();
        }

        private void StartTimer()
        {
            if (_expanderTimer == null)
            {
                _expanderTimer = new CTimer(o => CheckTracker(), null, 90000, 90000);
                return;
            }
            _expanderTimer.Reset(90000, 90000);
        }

        private void CheckTracker()
        {
            Parent.SendLine("DEVICE get discoveredExpanders");
            StartTimer();
        }

        public void ParseResults(Regex rgx, string data)
        {
            var matches = rgx.Matches(data);
            for (int i = 0; i < matches.Count;  i++)
            {
                const string pattern = "\\\"([^\\\"\\\"]+)\\\"?";

                var matches2 = Regex.Matches(data, pattern);
                var dataMod = Regex.Replace(data, pattern, "").Trim('"').Trim('[').Trim().Replace("  ", " "); ;
                //Console.WriteLine("Data2 = {0}", data2);
                var fData = dataMod.Split(' ');
                var hostname = matches2[0].ToString().Trim('"');
                var serialNumber = matches2[1].ToString().Trim('"');
                var firmware = String.Format("{0}.{1}.{2}-build{3}", fData[0], fData[1], fData[2], fData[3]);

                var newData = data.Replace("[", "").Replace("]", "");
                var macGroup = newData.Split(' ');
                var macAddress = macGroup.Aggregate("", (current, oct) => current + int.Parse(oct).ToString("X2"));

                var expander = Expanders.FirstOrDefault(v => v.Hostname == hostname);
                if (expander == null) continue;
                expander.SetData(serialNumber, firmware, macAddress);
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

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, expanderJoinMap);
            }

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            foreach (var item in Expanders)
            {
                if (item == null) return;

                var expander = item;
                var index = expander.Index;
                var offset = (5*(index - 1));


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

    public class TesiraExpanderData
    {
        private CTimer _expanderTimer;

        public bool Online { get; private set; }
        public string Hostname { get; private set; }
        public string SerialNumber { get; private set; }
        public string Firmware { get; private set; }
        public string MacAddress { get; private set; }
        public int Index { get; private set; }

        private const string pattern = "\\\"([^\\\"\\\"]+)\\\"?";

        public TesiraExpanderData(string data, int index)
        {
            Online = false;
            Index = index;
            Hostname = data;
            SerialNumber = "";
            Firmware = "";
            MacAddress = "";
        }

        public void SetData(string serial , string firmware, string mac)
        {
            if (_expanderTimer == null)
            {
                _expanderTimer = new CTimer(o => SetOffline(), null, 120000, 120000);
                return;
            }
            _expanderTimer.Reset(120000, 120000);

        }

        private void SetOffline()
        {
            _expanderTimer = null;
            Online = false;
        }

    }
}