using UnityEngine;

namespace PeakDangoMusic;

internal sealed class IdleCameraShots
{
    internal enum Scale { Near, Wide, Scenery }

    internal readonly record struct Frame(Vector3 Position, Vector3 LookAt, Scale ShotScale);
    internal delegate bool Place(Vector3 player, Frame frame, out Pose pose);

    private readonly record struct Shot(Vector3 From, Vector3 To, Vector3 LookFrom, Vector3 LookTo,
        float Seconds, float Arc = 0f);

    // Coordinates are in metres relative to the player: right, up, forward.
    private static readonly Shot[][] Shots =
    {
        new Shot[]
        {
            new(new(-2f, 0.48f, 3.35f), new(0.65f, 0.48f, 3.35f), new(0.88f, 0.22f, 0f), new(1.05f, 0.22f, 0f), 5.2f),
            new(new(-1.9f, 0.12f, -3.3f), new(-0.6f, 0.14f, -2.25f), new(0.8f, 0.18f, 0.45f), new(0.6f, 0.2f, 0.4f), 5.4f),
            new(new(2.4f, 1.25f, 2.4f), new(2.4f, 0.72f, 2.4f), new(-0.75f, 0.3f, 0.45f), new(-0.9f, 0.14f, 0.5f), 4.8f)
        },
        new Shot[]
        {
            new(new(-3.7f, 0.9f, -5.8f), new(3.1f, 1.35f, -6.1f), new(2.15f, 0.65f, 1.6f), new(2.65f, 0.65f, 1.9f), 6.4f, 1.4f),
            new(new(3.4f, 1.25f, -4.75f), new(4.7f, 2.05f, -7.1f), new(-1.85f, 0.5f, 1.25f), new(-2.4f, 0.6f, 1.7f), 6f),
            new(new(-4.4f, 2.3f, 5.8f), new(-2.6f, 3.45f, 6.4f), new(1.8f, 0.6f, -1.35f), new(1.9f, 0.4f, -1.85f), 6.2f)
        },
        new Shot[]
        {
            new(new(-2.6f, 1.6f, 2.8f), new(3.2f, 1.6f, 2.8f), new(2.2f, 0.4f, 16f), new(5.4f, 0.65f, 17f), 6.8f),
            new(new(-3.2f, 1.85f, 2.3f), new(-3.2f, 1.85f, 2.3f), new(-8.4f, 2.6f, 14f), new(6.5f, 3.4f, 15f), 6.1f),
            new(new(2.8f, 0.8f, 2.7f), new(1.9f, 2.8f, 4f), new(6.9f, 4.2f, 15f), new(5.1f, 5.7f, 18f), 7.2f)
        }
    };

    private readonly System.Random random;
    private readonly int[] last = { -1, -1, -1 };
    private Scale nextScale;
    private Frame from;
    private Frame to;
    private Vector3 curve;
    private Vector3 shotAnchor;
    private float started;
    private float duration;
    private float retryAt;
    internal int Revision { get; private set; }

    internal IdleCameraShots() : this(new System.Random()) { }

    internal IdleCameraShots(System.Random random) => this.random = random;

    internal void Reset()
    {
        duration = retryAt = 0f;
        nextScale = Scale.Near;
    }

    internal bool TryView(float elapsed, Vector3 player, Vector3 facing, Place place, out Pose pose)
    {
        pose = default;
        if (elapsed < retryAt) return false;

        if (duration > 0f && elapsed - started < duration && (player - shotAnchor).sqrMagnitude < 1.5f)
        {
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - started) / duration));
            if (place(player, Sample(from, to, curve, progress), out pose)) return true;
        }

        // Each new shot can cut to a fresh camera position. Check both ends and the
        // midpoint first so a shot does not begin in a wall or immediately face one.
        Scale preferred = nextScale;
        for (int group = 0; group < Shots.Length; group++)
        {
            Scale scale = (Scale)(((int)preferred + group) % Shots.Length);
            if (!TryStart(scale, elapsed, player, facing, place, out pose)) continue;
            nextScale = (Scale)(((int)scale + 1) % Shots.Length);
            return true;
        }

        duration = 0f;
        retryAt = elapsed + 0.75f;
        return false;
    }

    private bool TryStart(Scale scale, float elapsed, Vector3 player, Vector3 facing, Place place, out Pose pose)
    {
        int group = (int)scale;
        Shot[] variants = Shots[group];
        int first = random.Next(variants.Length);
        int mirror = random.Next(2) == 0 ? -1 : 1;
        // Scan the same compositions facing other parts of the map when terrain blocks the first angle.
        int yaw = random.Next(4);
        for (int i = 0; i < 4; i++)
        {
            Vector3 forward = Quaternion.AngleAxis(((yaw + i) % 4) * 90f, Vector3.up) * facing;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            for (int j = 0; j < variants.Length; j++)
            {
                int index = (first + j) % variants.Length;
                if (index == last[group] && i < 3) continue;
                Shot shot = variants[index];
                Vector3 World(Vector3 offset) => player + right * (offset.x * mirror) + Vector3.up * offset.y + forward * offset.z;
                var start = new Frame(World(shot.From), World(shot.LookFrom), scale);
                var end = new Frame(World(shot.To), World(shot.LookTo), scale);
                Vector3 bend = -forward * shot.Arc;
                if (!place(player, start, out pose) ||
                    !place(player, Sample(start, end, bend, 0.5f), out _) ||
                    !place(player, end, out _))
                    continue;

                from = start;
                to = end;
                curve = bend;
                shotAnchor = player;
                started = elapsed;
                duration = shot.Seconds;
                last[group] = index;
                Revision++;
                return true;
            }
        }
        pose = default;
        return false;
    }

    private static Frame Sample(Frame from, Frame to, Vector3 bend, float progress) =>
        new(Vector3.Lerp(from.Position, to.Position, progress) + bend * (4f * progress * (1f - progress)),
            Vector3.Lerp(from.LookAt, to.LookAt, progress), from.ShotScale);
}
