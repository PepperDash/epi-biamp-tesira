using System;
using System.Linq;
using System.Text.RegularExpressions;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Devices.Common.AudioCodec;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Represents a DTMF Decode block on the Tesira DSP.
    /// Subscribes to DTMF tone detection and provides feedback when tones are received.
    /// </summary>
    public class TesiraDtmfDecode : TesiraDspControlPoint, IDtmfDecode, IHasFeedback
    {
        private string lastDetectedTone;
        private readonly int index;

        private const string keyFormatter = "{0}--{1}";

        /// <summary>
        /// StringFeedback for the last detected DTMF tone.
        /// </summary>
        public StringFeedback LastDetectedToneFeedback { get; set; }

        /// <summary>
        /// Event raised when a DTMF tone is detected.
        /// </summary>
        public event EventHandler<DtmfReceivedEventArgs> DtmfReceived;

        /// <summary>
        /// Constructor for TesiraDtmfDecode Component
        /// </summary>
        /// <param name="key">Unique key for the component</param>
        /// <param name="config">Config object for the component</param>
        /// <param name="parent">Component parent (TesiraDsp)</param>
        public TesiraDtmfDecode(string key, TesiraDtmfDecodeConfig config, TesiraDsp parent)
            : base(config.DtmfDecodeInstanceTag, config.DtmfDecodeInstanceTag, config.Index, 0, parent, 
                string.Format(keyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {
            index = config.Index;
            
            LastDetectedToneFeedback = new StringFeedback(Key + "-LastDetectedToneFeedback", () => lastDetectedTone ?? string.Empty);

            Feedbacks.Add(LastDetectedToneFeedback);
            Feedbacks.Add(NameFeedback);
            parent.Feedbacks.AddRange(Feedbacks);

            Initialize(config);
        }

        private void Initialize(TesiraDtmfDecodeConfig config)
        {
            this.LogVerbose("Adding DTMF Decode block {key}", Key);
            IsSubscribed = false;
            Enabled = config.Enabled;
        }

        /// <summary>
        /// Subscribe to DTMF tone detection
        /// </summary>
        public override void Subscribe()
        {
            this.LogVerbose("Subscribing to DTMF Decode block");

            // Subscribe to DTMF detected value
            var customName = string.Format("{0}__value{1}", InstanceTag1, index);
            AddCustomName(customName);
            SendSubscriptionCommand(customName, "value", 250, 1);
        }

        /// <summary>
        /// Unsubscribe from DTMF tone detection
        /// </summary>
        public override void Unsubscribe()
        {
            IsSubscribed = false;
            this.LogVerbose("Unsubscribed from DTMF Decode block");
        }

        /// <summary>
        /// Handle subscription messages from the DSP containing detected DTMF tone
        /// </summary>
        /// <param name="customName">Custom subscription name</param>
        /// <param name="value">The detected DTMF value</param>
        public override void ParseSubscriptionMessage(string customName, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            try
            {
                this.LogDebug("DTMF Tone detected: {tone}", value);
                
                lastDetectedTone = value;
                LastDetectedToneFeedback.FireUpdate();

                // Raise the DtmfReceived event
                DtmfReceived?.Invoke(this, new DtmfReceivedEventArgs(value));

                IsSubscribed = true;
            }
            catch (Exception ex)
            {
                this.LogError(ex, "Error parsing DTMF response");
            }
        }

        /// <summary>
        /// Handle get message responses from the DSP
        /// </summary>
        /// <param name="attributeCode">The attribute code being queried</param>
        /// <param name="message">The response message from the DSP</param>
        public override void ParseGetMessage(string attributeCode, string message)
        {
            // Handle initial state query response
            if (!string.IsNullOrEmpty(message) && !message.Contains("error"))
            {
                lastDetectedTone = message;
                LastDetectedToneFeedback.FireUpdate();
            }
        }
    }
}
