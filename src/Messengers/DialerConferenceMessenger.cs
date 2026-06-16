using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PepperDash.Core.Logging;
using PepperDash.Essentials.AppServer;
using PepperDash.Essentials.AppServer.Messengers;
using PepperDash.Essentials.Core;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Dialer;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Messengers
{
    /// <summary>
    /// Messenger that exposes local conference state and control for a single Tesira dialer line.
    /// Conferencing is supported on VoIP lines only; on POTS lines the actions are no-ops and the
    /// reported state simply remains inactive.
    /// </summary>
    public class DialerConferenceMessenger : MessengerBase
    {
        private readonly TesiraDspDialer _line;

        /// <summary>
        /// Constructor for the dialer conference messenger.
        /// </summary>
        /// <param name="key">Messenger key.</param>
        /// <param name="messagePath">Message path.</param>
        /// <param name="line">Dialer line instance.</param>
        public DialerConferenceMessenger(string key, string messagePath, TesiraDspDialer line)
            : base(key, messagePath, line)
        {
            _line = line ?? throw new ArgumentNullException(nameof(line));
        }

        /// <inheritdoc />
        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) => SendStatus(id));
            AddAction("/conferenceStatus", (id, content) => SendStatus(id));

            AddAction("/conferenceAll", (id, content) => _line.ConferenceAllActive());
            AddAction("/leaveConference", (id, content) => _line.LeaveConference());

            AddAction("/conference", (id, content) =>
            {
                var appearance = GetAppearance(content);
                if (appearance != null)
                    appearance.Conference();
            });

            AddAction("/leaveAppearance", (id, content) =>
            {
                var appearance = GetAppearance(content);
                if (appearance != null)
                    appearance.LeaveConference();
            });

            _line.CallStatusChange += (s, a) => SendStatus();
        }

        private TesiraCallAppearance GetAppearance(Newtonsoft.Json.Linq.JToken content)
        {
            try
            {
                var value = content.ToObject<MobileControlSimpleContent<int>>();
                return _line.Appearances.FirstOrDefault(x => x.AppearanceNumber == value.Value);
            }
            catch (Exception ex)
            {
                this.LogError(ex, "Error parsing conference appearance content");
                return null;
            }
        }

        private void SendStatus(string id = null)
        {
            try
            {
                var message = new DialerConferenceStateMessage
                {
                    ConferenceActive = _line.ConferenceActive,
                    ConferenceCount = _line.ConferenceCount,
                    Appearances = _line.Appearances.Select(a => new ConferenceAppearanceState
                    {
                        Appearance = a.AppearanceNumber,
                        Label = a.Label,
                        InConference = a.InConferenceState,
                        OnHold = a.HoldCallFeedback.BoolValue,
                        CallStatus = a.CallStatusEnum.ToString()
                    }).ToList()
                };

                if (id == null)
                    PostStatusMessage(message);
                else
                    Task.Run(() => PostStatusMessage(message, id));
            }
            catch (Exception ex)
            {
                this.LogError(ex, "Error sending dialer conference status");
            }
        }
    }

    /// <summary>
    /// Message representing the local conference state of a Tesira dialer line.
    /// </summary>
    public class DialerConferenceStateMessage : DeviceStateMessageBase
    {
        /// <summary>True when at least one appearance is part of a local conference.</summary>
        [JsonProperty("conferenceActive", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ConferenceActive { get; set; }

        /// <summary>Number of appearances currently part of a local conference.</summary>
        [JsonProperty("conferenceCount", NullValueHandling = NullValueHandling.Ignore)]
        public int? ConferenceCount { get; set; }

        /// <summary>Per-appearance conference state.</summary>
        [JsonProperty("appearances", NullValueHandling = NullValueHandling.Ignore)]
        public List<ConferenceAppearanceState> Appearances { get; set; }
    }

    /// <summary>
    /// Per-appearance conference state.
    /// </summary>
    public class ConferenceAppearanceState
    {
        /// <summary>Call appearance number (1-based).</summary>
        [JsonProperty("appearance")]
        public int Appearance { get; set; }

        /// <summary>Friendly label for the appearance.</summary>
        [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)]
        public string Label { get; set; }

        /// <summary>True when this appearance is part of a local conference.</summary>
        [JsonProperty("inConference")]
        public bool InConference { get; set; }

        /// <summary>True when this appearance is on hold.</summary>
        [JsonProperty("onHold")]
        public bool OnHold { get; set; }

        /// <summary>Current call status name for the appearance.</summary>
        [JsonProperty("callStatus", NullValueHandling = NullValueHandling.Ignore)]
        public string CallStatus { get; set; }
    }
}
