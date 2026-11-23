using UnityEngine;

namespace PeakDangoMusic;

internal enum IdleCameraBlock
{
    None, Disabled, Camera, PlayerPanel, Menu, Background, Input
}

internal sealed class IdleCameraTimer
{
    private float started = -1f;

    internal float Remaining { get; private set; }
    internal bool WasActive { get; private set; }
    internal IdleCameraBlock LastReset { get; private set; } = IdleCameraBlock.Camera;

    internal float? Update(float now, IdleCameraBlock blocked, float wait)
    {
        if (blocked != IdleCameraBlock.None)
        {
            Reset(blocked);
            return null;
        }

        if (started < 0f) started = now;
        float elapsed = now - started;
        Remaining = Mathf.Max(0f, wait - elapsed);
        WasActive = elapsed >= wait;
        return WasActive ? elapsed - wait : null;
    }

    internal void Reset(IdleCameraBlock reason)
    {
        started = -1f;
        // Opening settings should preserve the last in-game reading.
        if (reason == IdleCameraBlock.PlayerPanel) return;
        LastReset = reason;
        Remaining = 0f;
        WasActive = false;
    }
}
