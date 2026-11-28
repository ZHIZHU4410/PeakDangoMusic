using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Peak;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeakDangoMusic;

internal static class AmbienceRegions
{
    internal readonly record struct Sample(float Enclosure, float AntiGravity, float Reverb, float ReflectionDelay, float Feedback, float Water, bool GloomFog);

    private delegate bool GravityQuery(Character player, out float fall, out float jump);
    private delegate bool PlanetQuery(Vector3 position, out Vector3 up, out float strength);

    private static readonly FieldInfo? SphereCharacters = AccessTools.Field(typeof(AntiSphere), "ACTIVE_CHARACTERS");
    private static readonly Type? ForgeZones = AccessTools.TypeByName("PeakMapForge.Build.ForgeEffectZone");
    private static readonly GravityQuery? ForgeGravity = Bind<GravityQuery>("GravityFor",
        typeof(Character), typeof(float).MakeByRefType(), typeof(float).MakeByRefType());
    private static readonly PlanetQuery? ForgePlanet = Bind<PlanetQuery>("PlanetUp",
        typeof(Vector3), typeof(Vector3).MakeByRefType(), typeof(float).MakeByRefType());
    private static AntiSphere[] spheres = Array.Empty<AntiSphere>();
    private static AudioReverbZone[] reverbs = Array.Empty<AudioReverbZone>();
    private static WaterZone[] waterZones = Array.Empty<WaterZone>();
    private static StatusFieldGloom[] gloomFields = Array.Empty<StatusFieldGloom>();
    private static float nextSphereScan, nextRegionScan;
    private static int sceneHandle = -1;

    internal static Sample Read(Character? player, float now)
    {
        if (player == null || player.data == null || player.data.dead ||
            player.refs?.ragdoll is not { } ragdoll || !ragdoll.partDict.TryGetValue(BodypartType.Head, out var head) || head == null)
            return default;

        int scene = SceneManager.GetActiveScene().handle;
        if (scene != sceneHandle)
        {
            sceneHandle = scene;
            nextSphereScan = nextRegionScan = 0f;
            spheres = Array.Empty<AntiSphere>();
            reverbs = Array.Empty<AudioReverbZone>();
            waterZones = Array.Empty<WaterZone>();
            gloomFields = Array.Empty<StatusFieldGloom>();
        }

        Vector3 position = head.transform.position;
        Vector3 up = Physics.gravity.sqrMagnitude > 0.01f ? -Physics.gravity.normalized : Vector3.up;
        float anti = 0f;
        if (ForgePlanet != null && ForgePlanet(position, out var planetUp, out float pull) && pull > 0f &&
            planetUp.sqrMagnitude > 0.5f)
        {
            up = planetUp.normalized;
            anti = Mathf.InverseLerp(0.55f, -0.5f, Vector3.Dot(up, Vector3.up));
        }
        if (ForgeGravity != null && ForgeGravity(player, out float fall, out _) && fall < 0.85f)
            anti = Mathf.Max(anti, Mathf.InverseLerp(0.85f, 0.05f, fall));

        // The game's timeInAntigrav can survive leaving a sphere. Membership is the live signal.
        if (player.data.timeInAntigrav > 0f)
        {
            if (now >= nextSphereScan)
            {
                nextSphereScan = now + 0.75f;
                spheres = UnityEngine.Object.FindObjectsByType<AntiSphere>(FindObjectsSortMode.None);
            }
            if (InsideAntiSphere(player, spheres)) anti = 1f;
        }

        if (now >= nextRegionScan)
        {
            nextRegionScan = now + 1.25f;
            reverbs = UnityEngine.Object.FindObjectsByType<AudioReverbZone>(FindObjectsSortMode.None);
            waterZones = UnityEngine.Object.FindObjectsByType<WaterZone>(FindObjectsSortMode.None);
            gloomFields = UnityEngine.Object.FindObjectsByType<StatusFieldGloom>(FindObjectsSortMode.None);
        }
        float reverb = ReverbAt(position, reverbs, out var zone);
        float delay = zone == null ? 0.1f : Mathf.Clamp(zone.reflectionsDelay + zone.reverbDelay, 0.035f, 0.19f);
        float feedback = zone == null ? 0.42f :
            Mathf.Clamp(Mathf.Pow(0.001f, delay / Mathf.Max(0.12f, zone.decayTime)), 0.24f, 0.65f);
        Vector3 torso = ragdoll.partDict.TryGetValue(BodypartType.Torso, out var body) && body != null
            ? body.transform.position : position;
        return new Sample(EnclosureAt(position, up), anti, reverb, delay, feedback,
            WaterAt(position, torso, waterZones), InGloomFog(position, gloomFields));
    }

    private static bool InGloomFog(Vector3 head, StatusFieldGloom[] fields)
    {
        foreach (var field in fields)
        {
            // The native query checks the fog bounds and active safe zones, including lit belltowers.
            if (field != null && field.isActiveAndEnabled && field.PointInField(head)) return true;
        }
        return false;
    }

    private static T? Bind<T>(string name, params Type[] parameters) where T : Delegate
    {
        var method = ForgeZones?.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null, parameters, null);
        return method == null ? null : Delegate.CreateDelegate(typeof(T), method, throwOnBindFailure: false) as T;
    }

    private static bool InsideAntiSphere(Character player, AntiSphere[] activeSpheres)
    {
        foreach (var sphere in activeSpheres)
        {
            if (sphere != null && sphere.isActiveAndEnabled &&
                SphereCharacters?.GetValue(sphere) is List<Character> players && players.Contains(player))
                return true;
        }
        return false;
    }

    private static float ReverbAt(Vector3 position, AudioReverbZone[] zones, out AudioReverbZone? selected)
    {
        selected = null;
        float strongest = 0f;
        foreach (var zone in zones)
        {
            if (zone == null || !zone.isActiveAndEnabled || zone.reverbPreset == AudioReverbPreset.Off) continue;
            float distance = Vector3.Distance(position, zone.transform.position);
            if (distance >= zone.maxDistance) continue;
            float weight = distance <= zone.minDistance ? 1f :
                Mathf.InverseLerp(zone.maxDistance, zone.minDistance, distance);
            if (weight <= strongest) continue;
            strongest = weight;
            selected = zone;
        }
        return strongest;
    }

    private static float EnclosureAt(Vector3 position, Vector3 up)
    {
        int mask = PlayerPerspective.TerrainMask;
        if (mask == 0) return 0f;
        var across = Vector3.Cross(up, Mathf.Abs(up.x) < 0.8f ? Vector3.right : Vector3.forward).normalized;
        var along = Vector3.Cross(up, across);
        bool nearRoof = Physics.Raycast(position, up, 4.5f, mask, QueryTriggerInteraction.Ignore);
        int overhead = 0;
        if (nearRoof || Physics.Raycast(position, up, 12f, mask, QueryTriggerInteraction.Ignore)) overhead++;
        if (Physics.Raycast(position + across * 0.65f, up, 12f, mask, QueryTriggerInteraction.Ignore)) overhead++;
        if (Physics.Raycast(position - across * 0.65f, up, 12f, mask, QueryTriggerInteraction.Ignore)) overhead++;

        float narrow = 0f;
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            Vector3 axis = along * Mathf.Cos(angle) + across * Mathf.Sin(angle);
            if (Physics.Raycast(position, axis, out var left, 3.1f, mask, QueryTriggerInteraction.Ignore) &&
                Physics.Raycast(position, -axis, out var right, 3.1f, mask, QueryTriggerInteraction.Ignore))
                narrow = Mathf.Max(narrow, 0.48f + 0.52f * Mathf.InverseLerp(6.2f, 2f, left.distance + right.distance));
        }
        return Mathf.Max(narrow, overhead >= 2 ? 0.82f + narrow * 0.18f : nearRoof ? 0.72f : 0f);
    }

    private static float WaterAt(Vector3 head, Vector3 torso, WaterZone[] zones)
    {
        float strongest = 0f;
        foreach (var zone in zones)
        {
            if (zone == null || !zone.isActiveAndEnabled) continue;
            Bounds bounds = zone.zoneBounds;
            float surface = zone.waterPlane != null ? zone.waterPlane.bounds.center.y : bounds.max.y;
            float bottom = Mathf.Min(bounds.min.y, surface - zone.waterDepth);
            // Use the actual surface where the character is, even when the camera is outside the water.
            float headDepth = Immersion(head, bounds, surface, bottom);
            float torsoDepth = Immersion(torso, bounds, surface, bottom);
            if (headDepth > 0.045f)
                strongest = Mathf.Max(strongest, Mathf.Lerp(0.88f, 1f, Mathf.InverseLerp(0.045f, 0.45f, headDepth)));
            else if (torsoDepth > 0.12f)
                strongest = Mathf.Max(strongest, Mathf.Lerp(0.28f, 0.52f, Mathf.InverseLerp(0.12f, 0.65f, torsoDepth)));
        }
        return strongest;
    }

    private static float Immersion(Vector3 position, Bounds bounds, float surface, float bottom)
    {
        if (position.x < bounds.min.x || position.x > bounds.max.x ||
            position.z < bounds.min.z || position.z > bounds.max.z || position.y < bottom - 0.1f)
            return 0f;
        return Mathf.Max(0f, surface - position.y);
    }
}
