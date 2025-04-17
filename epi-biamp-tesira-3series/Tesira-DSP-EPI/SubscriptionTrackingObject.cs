namespace Tesira_DSP_EPI
{
    /// <summary>
    /// Track a subscription in a multi-subscription control
    /// </summary>
    public class SubscriptionTrackingObject
    {
        /// <summary>
        /// Enables the subscription object
        /// </summary>
        public readonly bool Enabled;

        /// <summary>
        /// Object subscription status
        /// </summary>
        public bool Subscribed { get; set; }

        /// <summary>
        /// Constructor for subscription tracking object
        /// </summary>
        /// <param name="enabled">sets enable status at instantiation</param>
        public SubscriptionTrackingObject(bool enabled)
        {
            Enabled = enabled;
        }
    }
}