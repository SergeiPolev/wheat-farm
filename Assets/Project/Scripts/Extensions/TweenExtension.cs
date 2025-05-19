using DG.Tweening;

public static class TweenExtension
{
    public static Tween KillTo0(this Tween t, bool andPlay = true)
    {
        if (t != null)
        {
            t.Goto(0, andPlay);
            t.Kill();
        }

        return null;
    }
}