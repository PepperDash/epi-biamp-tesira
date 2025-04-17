using PepperDash.Core;


namespace Tesira_DSP_EPI.Extensions
{
    public static class ScalingExtensions
    {
        public static double Scale(this double input, double inMin, double inMax, double outMin, double outMax, IKeyed parent)
        {
            var inputRange = inMax - inMin;

            if (inputRange <= 0)
            {
                //throw new ArithmeticException(string.Format("Invalid Input Range '{0}' for Scaling.  Min '{1}' Max '{2}'.", inputRange, inMin, inMax));
                Debug.Console(0, parent, Debug.ErrorLogLevel.Notice, "Invalid Input Range '{0}' for Scaling.  Min '{1}' Max '{2}'.", inputRange, inMin, inMax);
                return input;

            }

            var outputRange = outMax - outMin;

            var output = (((input - inMin) * outputRange) / inputRange) + outMin;

            return output;
        }
    }

}
