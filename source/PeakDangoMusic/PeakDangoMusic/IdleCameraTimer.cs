using UnityEngine;

namespace PeakDangoMusic;

internal enum IdleCameraBlock
{
    None, Disabled, Camera, PlayerPanel, Menu, Background, Input
}

internal sealed class IdleCameraTimer
{
    private float started = -1f;
    private bool manual;

    internal float Remaining { get; private set; }
    internal bool WasActive { get; private set; }
    internal IdleCameraBlock LastReset { get; private set; } = IdleCameraBlock.Camera;

    internal float? Update(float now, IdleCameraBlock blocked, float wait, bool automatic, bool toggle)
    {
        // Only the press that toggles the camera bypasses the input check.
        if (toggle && blocked is IdleCameraBlock.None or IdleCameraBlock.Input)
        {
            if (started >= 0f && WasActive)
            {
                Reset(IdleCameraBlock.Input);
                return null;
            }

            started = now;
            Remaining = 0f;
            WasActive = manual = true;
            return 0f;
        }
        if (blocked != IdleCameraBlock.None)
        {
            Reset(blocked);
            return null;
        }

        if (manual) return now - started;
        if (!automatic)
        {
            Reset(IdleCameraBlock.Disabled);
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
        manual = false;
        // Opening settings should preserve the last in-game reading.
        if (reason == IdleCameraBlock.PlayerPanel) return;
        LastReset = reason;
        Remaining = 0f;
        WasActive = false;
    }
}
