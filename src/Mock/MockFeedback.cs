using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PepperDash.Essentials.Core;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock
{
    /// <summary>Helpers shared by the mock blocks.</summary>
    internal static class MockFeedback
    {
        // Essentials keeps a feedback's last published value in a private field, and FireUpdate() only
        // publishes a change from it. Resetting the field to a value the feedback can't produce makes
        // FireUpdate() publish the current value even if it hasn't changed.
        private static readonly FieldInfo IntValueField =
            typeof(IntFeedback).GetField("_IntValue", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BoolValueField =
            typeof(BoolFeedback).GetField("_BoolValue", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo StringValueField =
            typeof(StringFeedback).GetField("_StringValue", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// Publishes a feedback's current value even if it hasn't changed (bootstrap re-publish). Returns
        /// false if Essentials' feedback internals have changed: the feedback is still fired, but an
        /// unchanged value won't publish.
        /// </summary>
        public static bool ForceFire(Feedback feedback)
        {
            var reset = true;
            switch (feedback)
            {
                case IntFeedback _ when IntValueField != null:
                    IntValueField.SetValue(feedback, int.MinValue);
                    break;
                case BoolFeedback boolFeedback when BoolValueField != null:
                    BoolValueField.SetValue(feedback, !boolFeedback.BoolValue);
                    break;
                case StringFeedback _ when StringValueField != null:
                    StringValueField.SetValue(feedback, "\u0000");
                    break;
                default:
                    reset = false;
                    break;
            }

            feedback.FireUpdate();
            return reset;
        }

        /// <summary>The port whose selector is <paramref name="selector"/>, or null.</summary>
        public static T FindPort<T>(IEnumerable<T> ports, uint selector) where T : PepperDash.Essentials.Core.RoutingPort =>
            ports.FirstOrDefault(p => p.Selector != null && Convert.ToUInt32(p.Selector) == selector);
    }
}
