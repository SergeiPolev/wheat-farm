using System.Collections.Generic;
using UnityEngine;

public static class Helpers
{
    public static readonly Dictionary<float, WaitForSeconds> WaitForSeconds = new();
    public static readonly Dictionary<float, WaitForSecondsRealtime> WaitForSecondsRealtime = new();
    public static readonly WaitForFixedUpdate WaitForFixedUpdate = new WaitForFixedUpdate();
    public static WaitForSeconds GetWaitForSeconds(float seconds)
    {
        if (WaitForSeconds.TryGetValue(seconds, out WaitForSeconds forSeconds))
        {
            return forSeconds;
        }
        else
        {
            WaitForSeconds waitForSeconds = new WaitForSeconds(seconds);
            WaitForSeconds.Add(seconds, waitForSeconds);
            return waitForSeconds;
        }
    }

    public static WaitForSecondsRealtime GetWaitForSecondsRealtime(float seconds)
    {
        if (WaitForSecondsRealtime.TryGetValue(seconds, out WaitForSecondsRealtime forSeconds))
        {
            return forSeconds;
        }
        else
        {
            WaitForSecondsRealtime waitForSeconds = new WaitForSecondsRealtime(seconds);
            WaitForSecondsRealtime.Add(seconds, waitForSeconds);
            return waitForSeconds;
        }
    }
}