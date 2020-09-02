using System;


namespace Tesira_DSP_EPI.Extensions
{
    public static class ScalingExtensions
    {
        public static double Scale(this double input, double inMin, double inMax, double outMin, double outMax)
        {
            var inputRange = inMax - inMin;

            if (inputRange <= 0)
            {
                throw new ArithmeticException(string.Format("Invalid Input Range '{0}' for Scaling.  Min '{1}' Max '{2}'.", inputRange, inMin, inMax));
            }

            var outputRange = outMax - outMin;

            var output = (((input - inMin) * outputRange) / inputRange) + outMin;

            return output;
        }
    }
}
