using PepperDash.Essentials.Core;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps
{
  /// <summary>
  /// Device Joinmap for Advanced Bridge - Meant for holistic DSP Object
  /// </summary>
  public class TesiraDspDeviceJoinMapAdvanced : JoinMapBaseAdvanced
  {

    [JoinName("IsOnline")]
    public JoinDataComplete IsOnline =
        new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
              Description = "Device Online",
              JoinCapabilities = eJoinCapabilities.ToSIMPL,
              JoinType = eJoinType.Digital
            });

    [JoinName("Resubscribe")]
    public JoinDataComplete Resubscribe =
        new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
              Description = "Trigger control resubscription",
              JoinCapabilities = eJoinCapabilities.FromSIMPL,
              JoinType = eJoinType.Digital
            });

    [JoinName("Name")]
    public JoinDataComplete Name =
        new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
              Description = "Device Name",
              JoinCapabilities = eJoinCapabilities.ToSIMPL,
              JoinType = eJoinType.Serial
            });

    [JoinName("CommandPassThru")]
    public JoinDataComplete CommandPassThru =
        new JoinDataComplete(new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata
            {
              Description = "Pass discrete commands directly to/from the device",
              JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
              JoinType = eJoinType.Serial
            });

    [JoinName("SerialNumber")]
    public JoinDataComplete SerialNumber =
        new JoinDataComplete(new JoinData { JoinNumber = 3, JoinSpan = 1 },
        new JoinMetadata
        {
          Description = "Serial Number of the Device",
          JoinCapabilities = eJoinCapabilities.ToSIMPL,
          JoinType = eJoinType.Serial
        });

    [JoinName("Firmware")]
    public JoinDataComplete Firmware =
        new JoinDataComplete(new JoinData { JoinNumber = 4, JoinSpan = 1 },
        new JoinMetadata
        {
          Description = "Firmware of the Device",
          JoinCapabilities = eJoinCapabilities.ToSIMPL,
          JoinType = eJoinType.Serial
        });

    [JoinName("Hostname")]
    public JoinDataComplete Hostname =
        new JoinDataComplete(new JoinData { JoinNumber = 5, JoinSpan = 1 },
        new JoinMetadata
        {
          Description = "Hostname of the Device",
          JoinCapabilities = eJoinCapabilities.ToSIMPL,
          JoinType = eJoinType.Serial
        });

    [JoinName("IpAddress")]
    public JoinDataComplete IpAddress =
        new JoinDataComplete(new JoinData { JoinNumber = 6, JoinSpan = 1 },
        new JoinMetadata
        {
          Description = "IP Address of the Device",
          JoinCapabilities = eJoinCapabilities.ToSIMPL,
          JoinType = eJoinType.Serial
        });

    [JoinName("MacAddress")]
    public JoinDataComplete MacAddress =
        new JoinDataComplete(new JoinData { JoinNumber = 7, JoinSpan = 1 },
        new JoinMetadata
        {
          Description = "Mac Address of the Device",
          JoinCapabilities = eJoinCapabilities.ToSIMPL,
          JoinType = eJoinType.Serial
        });
    [JoinName("FaultPollingEnable")]
    public JoinDataComplete FaultPollingEnable =
        new JoinDataComplete(new JoinData { JoinNumber = 11, JoinSpan = 1 },
        new JoinMetadata
        {
            Description = "Fault monitoring: rising edge turns polling on (config interval, or 60 s), falling edge turns it off. Runtime only, never written to config. Feedback: polling active",
            JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
            JoinType = eJoinType.Digital
        });

    [JoinName("HasFault")]
    public JoinDataComplete HasFault =
        new JoinDataComplete(new JoinData { JoinNumber = 12, JoinSpan = 1 },
        new JoinMetadata
        {
            Description = "Fault monitoring: high while the DSP reports any active fault",
            JoinCapabilities = eJoinCapabilities.ToSIMPL,
            JoinType = eJoinType.Digital
        });

    [JoinName("FaultPollNow")]
    public JoinDataComplete FaultPollNow =
        new JoinDataComplete(new JoinData { JoinNumber = 13, JoinSpan = 1 },
        new JoinMetadata
        {
            Description = "Fault monitoring: poll the active fault list once now",
            JoinCapabilities = eJoinCapabilities.FromSIMPL,
            JoinType = eJoinType.Digital
        });

    [JoinName("ActiveFaultCount")]
    public JoinDataComplete ActiveFaultCount =
        new JoinDataComplete(new JoinData { JoinNumber = 11, JoinSpan = 1 },
        new JoinMetadata
        {
            Description = "Fault monitoring: number of active faults",
            JoinCapabilities = eJoinCapabilities.ToSIMPL,
            JoinType = eJoinType.Analog
        });

    [JoinName("FaultSeverity")]
    public JoinDataComplete FaultSeverity =
        new JoinDataComplete(new JoinData { JoinNumber = 12, JoinSpan = 1 },
        new JoinMetadata
        {
            Description = "Fault monitoring: worst indicator severity (0 none, 1 minor, 2 major, 3 critical)",
            JoinCapabilities = eJoinCapabilities.ToSIMPL,
            JoinType = eJoinType.Analog
        });

    [JoinName("FaultId")]
    public JoinDataComplete FaultId =
        new JoinDataComplete(new JoinData { JoinNumber = 11, JoinSpan = 1 },
        new JoinMetadata
        {
            Description = "Fault monitoring: worst indicator id, e.g. INDICATOR_MAJOR_IN_DEVICE",
            JoinCapabilities = eJoinCapabilities.ToSIMPL,
            JoinType = eJoinType.Serial
        });

    [JoinName("FaultName")]
    public JoinDataComplete FaultName =
        new JoinDataComplete(new JoinData { JoinNumber = 12, JoinSpan = 1 },
        new JoinMetadata
        {
            Description = "Fault monitoring: worst indicator name, e.g. Major Fault in Device",
            JoinCapabilities = eJoinCapabilities.ToSIMPL,
            JoinType = eJoinType.Serial
        });

    [JoinName("FaultList")]
    public JoinDataComplete FaultList =
        new JoinDataComplete(new JoinData { JoinNumber = 13, JoinSpan = 1 },
        new JoinMetadata
        {
            Description = "Fault monitoring: active fault names, comma-separated",
            JoinCapabilities = eJoinCapabilities.ToSIMPL,
            JoinType = eJoinType.Serial
        });

    public TesiraDspDeviceJoinMapAdvanced(uint joinStart)
        : base(joinStart, typeof(TesiraDspDeviceJoinMapAdvanced))
    {
    }
  }
}