#nullable disable

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Decides which fault poll replies to apply, per request. Each poll is queued with the
    /// attribute code <see cref="Tag"/>, which carries a generation number, and the queue hands that
    /// code back with the reply. Turning polling off starts a new generation, so a reply is applied
    /// only if polling hasn't been turned off since its request was sent. That holds for periodic
    /// polls and explicit one-shots alike: a reply to a poll sent before the turn-off can't restore
    /// the values that turning it off cleared, nor take the place of a later one-shot's reply.
    /// Not thread-safe: the caller holds its own lock.
    /// </summary>
    public class FaultReplyGate
    {
        private readonly string attributeCode;
        private int generation;

        public FaultReplyGate(string attributeCode)
        {
            this.attributeCode = attributeCode;
        }

        /// <summary>The attribute code to queue a poll with now.</summary>
        public string Tag => attributeCode + "#" + generation;

        /// <summary>Call when polling is turned off: replies to every earlier poll are no longer wanted.</summary>
        public void PollingTurnedOff()
        {
            generation++;
        }

        /// <summary>True if the queued attribute code belongs to a fault poll (of any generation).</summary>
        public bool IsFaultReply(string queuedAttributeCode) =>
            queuedAttributeCode != null && queuedAttributeCode.StartsWith(attributeCode + "#", System.StringComparison.Ordinal);

        /// <summary>True if the reply to a poll queued with this attribute code should be applied.</summary>
        public bool ShouldApply(string queuedAttributeCode) => queuedAttributeCode == Tag;
    }
}
