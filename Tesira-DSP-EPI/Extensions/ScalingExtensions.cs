using System;
using System.Globalization;
using PepperDash.Core;


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

    public static class DoubleExtensions
    {
        public static bool CompareFullPrecision(this double data1, double data2, IKeyed device)
        {
            if(device != null)
                Debug.Console(0, device, "Attempting to Compare {0} and {1}", data1, data2);
            else
                Debug.Console(0, "Attempting to Compare {0} and {1}", data1, data2);
           
            var culture = CultureInfo.CreateSpecificCulture("en-US");
            var stringData1 = data1.ToString(culture);
            var stringData2 = data2.ToString(culture);

            return StringComparer.InvariantCultureIgnoreCase.Compare(stringData1, stringData2) == 0;

        }
    }
}
