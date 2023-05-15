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
                var expander = new TesiraExpanderData(value, key, this);
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
            Debug.Console(2, this, "There are {0} configured expanders", Expanders.Count);


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
                Debug.Console(2, this, "Expander Index = {0} ; Expander Hostname = {1}", item.Index, item.Hostname);
            }
        }

        public override void Initialize()
        {
            CheckTracker();
        }


        private void CheckTracker()
        {
            Debug.Console(2, this, "Getting DiscoveredExpanders");

            SendFullCommand("get", "discoveredExpanders", null, 999);            

            //StartTimer();
        }

        public override void ParseGetMessage(string attributeCode, string message)
        {
            Debug.Console(2, this, "!!!!!!!!EXPANDER DATA!!!!!!!!!!!");
            const string pattern = @"\[([^\[\]]+)\]?";
            const string pattern2 = "\\\"([^\\\"\\\"]+)\\\"?";

            var matches = Regex.Matches(message, pattern);
            /*
            var newString = Regex.Replace(myString, pattern, "");
            var matches2 = Regex.Matches(newString, pattern);
		
            var someMatches = matches2.RemoveAll(s => s.ToString.Length < 4));
            */
            Console.WriteLine("There are {0} Matches", matches.Count);
            for (var v = 0; v < matches.Count; v++)
            {
                if (!matches[v].ToString().Contains('"')) continue;
                Debug.Console(2, this, "Match {0} is a device", v);

                var matchesEnclosed = Regex.Matches(matches[v].ToString(), pattern2);
                var data2 = Regex.Replace(matches[v].ToString(), pattern2, "").Trim('"').Trim('[').Trim().Replace("  ", " ");
                Console.WriteLine("Data2 = {0}", data2);
                var hostname = matchesEnclosed[0].ToString().Trim('"');

                Debug.Console(2, this, "Match {0} Hostname : {1}", v, hostname);

                var newData = Expanders.FirstOrDefault(o => String.Equals(o.Hostname, hostname, StringComparison.CurrentCultureIgnoreCase));

				if (newData == null) continue;
                Debug.Console(2, this, "Found a device Index {0} with Hostname {1}", newData.Index, newData.Hostname);

                newData.SetData(matches[v].ToString());
                newData.SetMac(matches[v+1].ToString());

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

                Debug.Console(2, this, "Index = {0}", i.Index);
                Debug.Console(2, this, "Hostname = {0}", i.Hostname);
                Debug.Console(2, this, "MacAddress = {0}", i.MacAddress);
                Debug.Console(2, this, "SerialNumber = {0}", i.SerialNumber);
                Debug.Console(2, this, "Firmware = {0}", i.Firmware);
                Debug.Console(2, this, "Online = {0}", i.Online);

            }
        }

        /*public void ParseResults(Regex rgx, string data)
        {
            var matches = rgx.Matches(data);
            Debug.Console(2, this, "There are {0} matches", matches.Count);
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

                Debug.Console(2, this, "Device {0} - hostname : {1} - serial : {1} - firmware : {2} - mac : {3}", i, hostname, serialNumber, firmware, macAddress);

                var expander = Expanders.FirstOrDefault(v => v.Hostname == hostname);
                if (expander == null) continue;
                expander.SetData(serialNumber, firmware, macAddress);
            }
        }
         * */

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


        #region ICommunicationMonitor Members


        #endregion
    }

    public class TesiraExpanderData : IDeviceInfoProvider, IKeyName
    {
        private CTimer _expanderTimer;

        public DeviceInfo DeviceInfo { get; private set; }
        public event DeviceInfoChangeHandler DeviceInfoChanged;


        public bool Online { get; private set; }
        public string Hostname { get; private set; }
        public string SerialNumber { get; private set; }
        public string Firmware { get; private set; }
        public string MacAddress { get; private set; }
        public int Index { get; private set; }
        public string Key { get; private set; }
        public string Name { get; private set; }
        public TesiraExpanderMonitor Monitor;

        private const string Pattern = "\\\"([^\\\"\\\"]+)\\\"?";

        public TesiraExpanderData(string data, int index, IKeyed parent) 
        {
            Key = String.Format("{0}-{1}", parent.Key, data);
            Name = data;
            Online = false;
            Index = index;
            Hostname = data;
            SerialNumber = "";
            Firmware = "";
            MacAddress = "";

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

        public void SetData(string data)
        {
            var matches = Regex.Matches(data, Pattern);
            var data2 = Regex.Replace(data, Pattern, "").Trim('"').Trim('[').Trim().Replace("  ", " ");
            Console.WriteLine("Data2 = {0}", data2);
            var fData = data2.Split(' ');
            Hostname = matches[0].ToString().Trim('"');
            SerialNumber = matches[1].ToString().Trim('"');
            Firmware = String.Format("{0}.{1}.{2}-build{3}", fData[0], fData[1], fData[2], fData[3]);

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
            Monitor.IsOnline = Online;
            UpdateDeviceInfo();
        }

        public void SetMac(string data)
        {
            var newData = data.Replace("[", "").Replace("]", "");
            var macGroup = newData.Split(' ');
            var mac = macGroup.Aggregate("", (current, oct) => current + (int.Parse(oct).ToString("X2") + ":")).Trim(':');
            MacAddress = mac;
            DeviceInfo.MacAddress = MacAddress;
            UpdateDeviceInfo();

        }


        #region IDeviceInfoProvider Members


        public void UpdateDeviceInfo()
        {
            var handler = DeviceInfoChanged;
            if (handler == null) return;
            handler(this, new DeviceInfoEventArgs(DeviceInfo));

        }

        #endregion
    }
}


