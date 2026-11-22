using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PeakDangoMusic;

internal sealed class PlayerPerspective : IDisposable
{
    internal static readonly string[] Names = { "第一人称", "第二人称", "第三人称" };
    internal static readonly int TerrainMask = LayerMask.GetMask("Map", "Terrain");

    private MainCameraMovement? movement;
    private int gameplayFrame = -1;
    private Camera? renderedCamera;
    private Pose original;
    private Pose alternate;
    private bool alternateReady;

    internal int Mode { get; set; }

    internal PlayerPerspective()
    {
        RenderPipelineManager.beginCameraRendering += BeginRendering;
        RenderPipelineManager.endCameraRendering += EndRendering;
        SceneManager.activeSceneChanged += ResetView;
    }

    internal void MarkGameplayCamera(MainCameraMovement cameraMovement)
    {
        movement = cameraMovement;
        gameplayFrame = Time.frameCount;
    }

    internal bool HasGameplayCamera(Camera? camera) =>
        camera != null && movement != null && camera.transform == movement.transform &&
        Time.frameCount - gameplayFrame <= 1;

    internal Pose UpdateView(Camera camera, Character player)
    {
        original = new Pose(camera.transform.position, camera.transform.rotation);
        alternateReady = false;
        if (Mode == 0) return original;

        Vector3 target = player.Center + Vector3.up * 0.45f;
        Vector3 forward = player.data.lookDirection_Flat;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f) return original;

        Vector3 facing = forward.normalized;
        Vector3 offset = facing * (Mode == 1 ? 2.8f : -3.25f) + Vector3.up * 0.7f;
        Vector3 direction = offset.normalized;
        float distance = offset.magnitude;
        if (Physics.SphereCast(target, 0.2f, direction, out RaycastHit hit, distance, TerrainMask, QueryTriggerInteraction.Ignore))
            distance = hit.distance - 0.12f;
        if (distance < 0.65f) return original;

        Vector3 position = target + direction * distance;
        if (Physics.CheckSphere(position, 0.15f, TerrainMask, QueryTriggerInteraction.Ignore))
            return original;

        alternateReady = true;
        alternate = new Pose(position, Quaternion.LookRotation(target - position, Vector3.up));
        return alternate;
    }

    internal void ClearView() => alternateReady = false;

    private void ResetView(Scene previous, Scene current)
    {
        Mode = 0;
        ClearView();
    }

    private void BeginRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!alternateReady || gameplayFrame != Time.frameCount || !HasGameplayCamera(camera) || renderedCamera != null)
            return;
        original = new Pose(camera.transform.position, camera.transform.rotation);
        renderedCamera = camera;
        camera.transform.SetPositionAndRotation(alternate.position, alternate.rotation);
    }

    private void EndRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == renderedCamera) RestoreCamera();
    }

    internal void RestoreCamera()
    {
        if (renderedCamera != null)
            renderedCamera.transform.SetPositionAndRotation(original.position, original.rotation);
        renderedCamera = null;
    }

    public void Dispose()
    {
        RenderPipelineManager.beginCameraRendering -= BeginRendering;
        RenderPipelineManager.endCameraRendering -= EndRendering;
        SceneManager.activeSceneChanged -= ResetView;
        RestoreCamera();
    }
}

[HarmonyPatch(typeof(MainCameraMovement), "CharacterCam")]
internal static class GameplayCameraPatch
{
    private static void Postfix(MainCameraMovement __instance) =>
        Plugin.Instance?.Perspective?.MarkGameplayCamera(__instance);
}
