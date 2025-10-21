namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Dialer
{
  public partial class TesiraDspDialer
  {
    /// <summary>
    /// List of possible Call Status values returned from component
    /// </summary>
    public enum ECallStatus
    {
      // ReSharper disable InconsistentNaming
      INIT = 1,
      FAULT,
      IDLE,
      DIAL_TONE,
      SILENT,
      DIALING,
      RINGBACK,
      RINGING,
      ANSWERING,
      BUSY,
      REJECT,
      INVALID_NUMBER,
      ACTIVE,
      ACTIVE_MUTED,
      ON_HOLD,
      WAITING_RING,
      CONF_ACTIVE,
      CONF_HOLD,
      XFER_INIT,
      XFER_Silent,
      XFER_ReqDialing,
      XFER_Process,
      XFER_ReplacesProcess,
      XFER_Active,
      XFER_RingBack,
      XFER_OnHold,
      XFER_Decision,
      XFER_InitError,
      XFER_WAIT
      // ReSharper restore InconsistentNaming
    }

  }
}