using System.Collections.Generic;
using System;
using Crestron.SimplSharp;
using Newtonsoft.Json;
using System.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Essentials.Core.DeviceInfo;
using Tesira_DSP_EPI.Bridge.JoinMaps;
using PepperDash.Essentials.Core.Bridges;
using System.Text.RegularExpressions;

namespace Tesira_DSP_EPI
{
    public class TesiraExpanderTracker : TesiraDspControlPoint, ICommunicationMonitor
    {

        private const string KeyFormatter = "{0}--{1}";
        public List<TesiraExpanderData> Expanders = new List<TesiraExpanderData>(); 

        public Dictionary<int, StringFeedback> Hostnames = new Dictionary<int, StringFeedback>();
        public Dictionary<int, StringFeedback> SerialNumbers = new Dictionary<int, StringFeedback>();
        public Dictionary<int, StringFeedback> Firmwares = new Dictionary<int, StringFeedback>();
        public Dictionary<int, StringFeedback> MacAddresses = new Dictionary<int, StringFeedback>();
        public Dictionary<int, BoolFeedback> OnlineStatuses = new Dictionary<int, BoolFeedback>();

        public StatusMonitorBase CommunicationMonitor { get; private set; }


        public TesiraExpanderTracker(TesiraDsp parent, Dictionary<string, TesiraExpanderBlockConfig> expanders )
            : base("", "", 0, 0, parent, String.Format(KeyFormatter, parent.Key, "Expanders"), "Expanders", 0)
        {
            CommunicationMonitor = new GenericCommunicationMonitor(this, Parent.Communication, 45000, 90000, 180000, CheckTracker);

            foreach (var tesiraExpanderBlockConfig in expanders)
            {
                var key = tesiraExpanderBlockConfig.Value.Index;
                var value = tesiraExpanderBlockConfig.Value.Hostname;
                var expander = new TesiraExpanderData(value, key, this, CheckTracker);
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

                DeviceManager.AddDevice(expander);

            }
            Debug.LogVerbose(this, "There are {0} configured expanders", Expanders.Count);


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

            if (Debug.Level != 2) return;
            foreach (var item in Expanders)
            {
                Debug.LogVerbose(this, "Expander Index = {0} ; Expander Hostname = {1}", item.Index, item.Hostname);
            }
        }

        public override void Initialize()
        {
            CheckTracker();
        }


        private void CheckTracker()
        {
            Debug.LogVerbose(this, "Getting DiscoveredExpanders");

            SendFullCommand("get", "discoveredExpanders", null, 999);            

            //StartTimer();
        }
        const string Pattern = @"\[([^\[\]]+)\]?";
        private readonly static Regex Regex1 = new Regex(Pattern);
        const string Pattern2 = "\\\"([^\\\"\\\"]+)\\\"?";
        private readonly static Regex Regex2 = new Regex(Pattern2);



        public override void ParseGetMessage(string attributeCode, string message)
        {
            Debug.LogVerbose(this, "!!!!!!!!EXPANDER DATA!!!!!!!!!!!");

            var matches = Regex1.Matches(message);

            Debug.LogVerbose(this, "There are {0} Matches", matches.Count);
            for (var v = 0; v < matches.Count; v++)
            {
                if (!matches[v].ToString().Contains('"')) continue;
                Debug.LogVerbose(this, "Match {0} is a device", v);

                var matchesEnclosed = Regex2.Matches(matches[v].ToString());
                var data2 = Regex2.Replace(matches[v].ToString(),  "").Trim('"').Trim('[').Trim().Replace("  ", " ");
                Console.WriteLine("Data2 = {0}", data2);
                var hostname = matchesEnclosed[0].ToString().Trim('"');

                Debug.LogVerbose(this, "Match {0} Hostname : {1}", v, hostname);

                var newData = Expanders.FirstOrDefault(o => String.Equals(o.Hostname, hostname, StringComparison.CurrentCultureIgnoreCase));

				if (newData == null) continue;
                Debug.LogVerbose(this, "Found a device Index {0} with Hostname {1}", newData.Index, newData.Hostname);
                var macData = matches[v + 1].ToString();
                var otherData = matches[v].ToString();
                newData.SetData(otherData, macData);

                //Console.WriteLine(matches[v]);
            }

            foreach (var feedback in Feedbacks)
            {
                var f = feedback;
                f.FireUpdate();
            }

            if (Debug.Level != 2) return;
            foreach (var device in Expanders)
            {
                var i = device;

                Debug.LogVerbose(this, "Index = {0}", i.Index);
                Debug.LogVerbose(this, "Hostname = {0}", i.Hostname);
                Debug.LogVerbose(this, "MacAddress = {0}", i.MacAddress);
                Debug.LogVerbose(this, "SerialNumber = {0}", i.SerialNumber);
                Debug.LogVerbose(this, "Firmware = {0}", i.Firmware);
                Debug.LogVerbose(this, "Online = {0}", i.Online);

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

            Debug.LogDebug(this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

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


        #region ICommunicationMonitor Members


        #endregion
    }

    public class TesiraExpanderData : IDeviceInfoProvider, IKeyName, IOnline
    {
        private CTimer _expanderTimer;

        public DeviceInfo DeviceInfo { get; private set; }
        public event DeviceInfoChangeHandler DeviceInfoChanged;
        private readonly Action DataPoll;

        public StringFeedback NameFeedback { get; private set; }

        public BoolFeedback IsOnline { get; private set; }
        public bool Online { get; private set; }
        public string Hostname { get; private set; }
        public string SerialNumber { get; private set; }
        public string Firmware { get; private set; }
        public string MacAddress { get; private set; }
        public int Index { get; private set; }
        public string Key { get; private set; }
        public string Name { get; private set; }
        public TesiraExpanderMonitor Monitor;

        private const string ExpanderPattern = "\\\"([^\\\"\\\"]+)\\\"?";
        private static readonly Regex ExpanderRegex = new Regex(ExpanderPattern);

        public TesiraExpanderData(string data, int index, IKeyed parent, Action dataPoll) 
        {
            Key = String.Format("{0}-{1}", parent.Key, data);
            Name = data;
            Online = false;
            Index = index;
            Hostname = data;
            SerialNumber = "";
            Firmware = "";
            MacAddress = "";
            DataPoll = dataPoll;

            IsOnline = new BoolFeedback(() => Online);
            NameFeedback = new StringFeedback(() => Name);

            Monitor = new TesiraExpanderMonitor(this, 180000, 360000);
            DeviceInfo = new DeviceInfo();
            Monitor.Start();
        }


        private void SetOffline()
        {
            _expanderTimer = null;
            Online = false;
            Monitor.IsOnline = Online;
        }

        public void SetData(string data, string macData)
        {
            var matches = ExpanderRegex.Matches(data);
            var data2 = ExpanderRegex.Replace(data, "").Trim('"').Trim('[').Trim().Replace("  ", " ");
            Console.WriteLine("Data2 = {0}", data2);
            var fData = data2.Split(' ');
            Hostname = matches[0].ToString().Trim('"');
            SerialNumber = matches[1].ToString().Trim('"');
            Firmware = String.Format("{0}.{1}.{2}-build{3}", fData[0], fData[1], fData[2], fData[3]);
            MacAddress = FormatMac(macData);

            Online = true;

            if (_expanderTimer == null)
            {
                _expanderTimer = new CTimer(o => SetOffline(), null, 120000, 120000);
                return;
            }
            _expanderTimer.Reset(120000, 120000);
            DeviceInfo.HostName = Hostname;
            DeviceInfo.SerialNumber = SerialNumber;
            DeviceInfo.FirmwareVersion = Firmware;
            DeviceInfo.MacAddress = MacAddress;

            Monitor.IsOnline = Online;
            OnDeviceInfoChanged();
        }

        private static string FormatMac(string data)
        {
            var newData = data.Replace("[", "").Replace("]", "");
            var macGroup = newData.Split(' ');
            var mac = macGroup.Aggregate("", (current, oct) => current + (int.Parse(oct).ToString("X2") + ":")).Trim(':');
            return mac;
        }


        private void OnDeviceInfoChanged()
        {
            var handler = DeviceInfoChanged;
            if (handler == null) return;
            handler(this, new DeviceInfoEventArgs(DeviceInfo));
        }


        #region IDeviceInfoProvider Members


        public void UpdateDeviceInfo()
        {
            if (DataPoll == null) return;
            DataPoll.Invoke();
        }

        #endregion
    }
}


