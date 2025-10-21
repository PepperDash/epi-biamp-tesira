using PepperDash.Core;


namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Extensions
{
    public static class ScalingExtensions
    {
        public static double Scale(this double input, double inMin, double inMax, double outMin, double outMax, IKeyed parent)
        {
            var inputRange = inMax - inMin;

            if (inputRange <= 0)
            {
                //throw new ArithmeticException(string.Format("Invalid Input Range '{0}' for Scaling.  Min '{1}' Max '{2}'.", inputRange, inMin, inMax));
                Debug.LogError("Invalid Input Range {range} for Scaling.  Min '{min}' Max '{max}'", inputRange, inMin, inMax);
                return input;

            }

            var outputRange = outMax - outMin;

            var output = ((input - inMin) * outputRange / inputRange) + outMin;

            return output;
        }
    }

}
