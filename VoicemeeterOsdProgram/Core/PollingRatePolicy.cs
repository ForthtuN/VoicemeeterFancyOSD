using System;

namespace VoicemeeterOsdProgram.Core;

internal static class PollingRatePolicy
{
    internal static double GetIntervalMilliseconds(int rateValue)
    {
        int hertz = rateValue switch
        {
            0 => 15,
            1 => 30,
            2 => 60,
            3 => 140,
            _ => 30
        };

        return 1000.0 / hertz;
    }
}
