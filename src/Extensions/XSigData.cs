using System;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Extensions
{
  public class XSigData
  {

    public int Index;
    public string XString;
    public int XInt;
    public bool XBool;
    public SigType SigType;

    public XSigData()
    {
      SigType = SigType.SigNone;
    }


    #region Overrides of Object

    public override string ToString()
    {
      return $"index: {Index} type: {SigType} xString: {XString} xInt: {XInt}, xBool:{XBool}";
    }

    #endregion
  }

}