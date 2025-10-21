using System;
using System.Linq;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceInfo;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Expander
{
  public class TesiraExpanderData : IDeviceInfoProvider, IKeyName, IOnline
  {
    private CTimer expanderTimer;

    public DeviceInfo DeviceInfo { get; private set; }
    public event DeviceInfoChangeHandler DeviceInfoChanged;
    private readonly Action dataPoll;

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

    private const string expanderPattern = "\\\"([^\\\"\\\"]+)\\\"?";
    private static readonly Regex expanderRegex = new Regex(expanderPattern);

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
      this.dataPoll = dataPoll;

      IsOnline = new BoolFeedback("online", () => Online);
      NameFeedback = new StringFeedback("name", () => Name);

      Monitor = new TesiraExpanderMonitor(this, 180000, 360000);
      DeviceInfo = new DeviceInfo();
      Monitor.Start();
    }


    private void SetOffline()
    {
      expanderTimer = null;
      Online = false;
      Monitor.IsOnline = Online;
    }

    public void SetData(string data, string macData)
    {
      var matches = expanderRegex.Matches(data);
      var data2 = expanderRegex.Replace(data, "").Trim('"').Trim('[').Trim().Replace("  ", " ");

      var fData = data2.Split(' ');
      Hostname = matches[0].ToString().Trim('"');
      SerialNumber = matches[1].ToString().Trim('"');

      Firmware = string.Format("{0}.{1}.{2}-build{3}", fData[0], fData[1], fData[2], fData[3]);

      MacAddress = FormatMac(macData);

      Online = true;

      if (expanderTimer == null)
      {
        expanderTimer = new CTimer(o => SetOffline(), null, 120000, 120000);
        return;
      }

      expanderTimer.Reset(120000, 120000);
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
      var mac = macGroup.Aggregate("", (current, oct) => current + int.Parse(oct).ToString("X2") + ":").Trim(':');
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
      if (dataPoll == null) return;
      dataPoll.Invoke();
    }

    #endregion
  }
}


