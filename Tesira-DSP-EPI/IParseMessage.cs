using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace Tesira_DSP_EPI
{
    public interface IParseMessage
    {
        void ParseGetMessage(string attribute, string data);
    }
}