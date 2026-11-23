using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeakDangoMusic;

internal static class LyricWeather
{
    internal enum Surface { None, Snow, Rain, Fire }

    private static Campfire[] fires = Array.Empty<Campfire>();
    private static (Lava Zone, Renderer? Surface)[] lava = Array.Empty<(Lava, Renderer?)>();
    private static int sceneHandle;
    private static float nextScan;
    private static float nextFireCheck;
    private static bool nearFire;
    private static Vector3 windVector;
    private static float wind, snow, rain, fire;

    internal static (Surface Kind, float Strength) Coating =>
        fire > 0.025f ? (Surface.Fire, fire) :
        snow > 0.025f && snow >= rain ? (Surface.Snow, snow) :
        rain > 0.025f ? (Surface.Rain, rain) : (Surface.None, 0f);

    internal static void Tick(bool enabled, string weather, float intensity, float shelter, Vector3 direction, float dt)
    {
        var player = Character.localCharacter;
        if (!enabled || player == null || player.data.dead || SceneManager.GetActiveScene().name == "Title")
        {
            wind = snow = rain = fire = 0f;
            nearFire = false;
            nextFireCheck = 0f;
            return;
        }

        float now = Time.unscaledTime;
        int scene = SceneManager.GetActiveScene().handle;
        if (scene != sceneHandle)
        {
            sceneHandle = scene;
            nextScan = nextFireCheck = 0f;
            nearFire = false;
            snow = rain = fire = wind = 0f;
        }
        if (now >= nextScan)
        {
            nextScan = now + 3f;
            fires = UnityEngine.Object.FindObjectsByType<Campfire>(FindObjectsSortMode.None);
            var zones = UnityEngine.Object.FindObjectsByType<Lava>(FindObjectsSortMode.None);
            lava = Array.ConvertAll(zones, zone => (zone, (Renderer?)zone.GetComponentInChildren<Renderer>()));
        }
        if (now >= nextFireCheck)
        {
            nextFireCheck = now + 0.25f;
            nearFire = HasFire(player.Center);
        }

        float exposure = Mathf.Clamp01(intensity) * (1f - Mathf.Clamp01(shelter) * 0.96f);
        wind = Mathf.MoveTowards(wind, weather == "Wind" ? exposure : 0f, dt * 1.5f);
        snow = Mathf.MoveTowards(snow, weather == "Snow" && !nearFire ? exposure : 0f, dt * 0.55f);
        rain = Mathf.MoveTowards(rain, weather == "Rain" ? exposure : 0f, dt * 1.15f);
        fire = Mathf.MoveTowards(fire, nearFire ? 1f : 0f, dt * 1.35f);
        if (direction.sqrMagnitude > 0.001f) windVector = direction.normalized;
        else if (windVector.sqrMagnitude < 0.001f) windVector = Vector3.right;
    }

    internal static Vector3 Drift(float scale, float phase = 0f)
    {
        if (wind <= 0f) return Vector3.zero;
        float time = Time.unscaledTime + phase;
        float gust = 0.64f + Mathf.Sin(time * 2.1f) * 0.26f + Mathf.Sin(time * 4.7f) * 0.10f;
        return (windVector * gust + Vector3.up * (Mathf.Sin(time * 2.6f) * 0.32f)) * (scale * wind);
    }

    private static bool HasFire(Vector3 player)
    {
        foreach (var campfire in fires)
        {
            if (campfire == null || !campfire.isActiveAndEnabled || !campfire.Lit) continue;
            Vector3 flames = campfire.transform.position + Vector3.up * 0.45f;
            if ((flames - player).sqrMagnitude < 16f &&
                !Physics.Linecast(player, flames, PlayerPerspective.TerrainMask, QueryTriggerInteraction.Ignore))
                return true;
        }
        foreach (var (zone, surface) in lava)
        {
            if (zone == null || surface == null || !zone.isActiveAndEnabled) continue;
            Bounds bounds = surface.bounds;
            if (Mathf.Abs(player.y - zone.transform.position.y) <= 2.4f &&
                player.x >= bounds.min.x && player.x <= bounds.max.x &&
                player.z >= bounds.min.z && player.z <= bounds.max.z)
                return true;
        }
        return false;
    }
}
