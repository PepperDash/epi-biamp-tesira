using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using PepperDash.Core.Logging;
using PepperDash.Essentials.AppServer;
using PepperDash.Essentials.AppServer.Messengers;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Dialer;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Messengers
{
    /// <summary>
    /// Messenger that exposes Tesira-specific dialer service codes that are not covered by the standard
    /// dialer messenger: redial (all lines) and hook-flash (POTS lines only). The redial action accepts
    /// an optional 1-based call appearance number in the content; when omitted the line picks a default.
    /// Flash is a line-level operation and takes no appearance.
    /// </summary>
    public class DialerControlMessenger : MessengerBase
    {
        private readonly TesiraDspDialer _line;

        /// <summary>
        /// Constructor for the dialer control messenger.
        /// </summary>
        /// <param name="key">Messenger key.</param>
        /// <param name="messagePath">Message path.</param>
        /// <param name="line">Dialer line instance.</param>
        public DialerControlMessenger(string key, string messagePath, TesiraDspDialer line)
            : base(key, messagePath, line)
        {
            _line = line ?? throw new ArgumentNullException(nameof(line));
        }

        /// <inheritdoc />
        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/redial", (id, content) => _line.Redial(GetAppearanceNumber(content)));
            AddAction("/flash", (id, content) => _line.Flash());
        }

        private int GetAppearanceNumber(JToken content)
        {
            if (content == null) return 0;
            try
            {
                var value = content.ToObject<MobileControlSimpleContent<int>>();
                return value != null ? value.Value : 0;
            }
            catch (Exception ex)
            {
                this.LogError(ex, "Error parsing dialer control appearance content");
                return 0;
            }
        }
    }
}
