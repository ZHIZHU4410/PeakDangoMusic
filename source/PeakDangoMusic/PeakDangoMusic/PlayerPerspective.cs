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
    private Camera? gameplayCamera;
    private int gameplayFrame = -1;
    private Camera? renderedCamera;
    private Pose original;
    private Pose alternate;
    private bool alternateReady;
    private readonly IdleCameraTimer idle = new IdleCameraTimer();
    private readonly IdleCameraShots shots = new IdleCameraShots();

    internal int Mode { get; set; }
    internal bool IdleCameraActive { get; private set; }
    internal int IdleShotId { get; private set; }
    internal string IdleStatus
    {
        get
        {
            string reason = idle.LastReset switch
            {
                IdleCameraBlock.Disabled => "已关闭",
                IdleCameraBlock.Camera => "等待本地角色镜头",
                IdleCameraBlock.Menu => "游戏菜单正在接收操作",
                IdleCameraBlock.Background => "游戏失去焦点",
                IdleCameraBlock.Input => "收到游戏操作",
                _ => "已开始计时"
            };
            if (idle.WasActive) return "正在运镜";
            return idle.Remaining > 0f ? $"剩余 {Mathf.CeilToInt(idle.Remaining)} 秒 · 上次重置：{reason}" : reason;
        }
    }

    internal PlayerPerspective()
    {
        RenderPipelineManager.beginCameraRendering += BeginRendering;
        RenderPipelineManager.endCameraRendering += EndRendering;
        SceneManager.activeSceneChanged += ResetView;
    }

    internal void MarkGameplayCamera(MainCameraMovement cameraMovement)
    {
        if (movement != cameraMovement || gameplayCamera == null)
            gameplayCamera = cameraMovement.GetComponent<Camera>();
        movement = cameraMovement;
        gameplayFrame = Time.frameCount;
    }

    internal Camera? GameplayCamera => gameplayCamera != null && gameplayCamera.isActiveAndEnabled &&
        Time.frameCount - gameplayFrame <= 1 ? gameplayCamera : null;

    internal Pose UpdateView(Camera camera, Character player, IdleCameraBlock blocked, int idleSeconds)
    {
        original = new Pose(camera.transform.position, camera.transform.rotation);
        alternate = original;
        alternateReady = false;
        IdleCameraActive = false;
        float? idleTime = idle.Update(Time.unscaledTime, blocked, idleSeconds);
        if (!idleTime.HasValue) shots.Reset();
        if (Mode == 0 && !idleTime.HasValue) return original;

        Vector3 target = player.Center + Vector3.up * 0.45f;
        Vector3 forward = player.data.lookDirection_Flat;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        Vector3 facing = forward.normalized;
        Vector3 side = Mode == 1 ? facing : -facing;
        if (Mode != 0 && TryPlace(target, side * (Mode == 1 ? 2.8f : 3.25f) + Vector3.up * 0.7f, out Vector3 manual))
        {
            alternate = new Pose(manual, Quaternion.LookRotation(target - manual, Vector3.up));
            alternateReady = true;
        }
        if (idleTime is not float elapsed) return alternate;
        if (shots.TryView(elapsed, target, facing, TryPlaceShot, out Pose shot))
        {
            alternate = shot;
            IdleShotId = shots.Revision * 2;
        }
        else
        {
            // A small pan gives tight spaces a shot without moving the camera into the terrain.
            float yaw = Mathf.Sin(elapsed * 0.46f) * 13f;
            float pitch = Mathf.Sin(elapsed * 0.34f) * 3.5f;
            alternate = new Pose(alternate.position, Quaternion.AngleAxis(yaw, Vector3.up) *
                alternate.rotation * Quaternion.Euler(pitch, 0f, 0f));
            IdleShotId = shots.Revision * 2 + 1;
        }
        IdleCameraActive = true;
        alternateReady = true;
        return alternate;
    }

    private static bool TryPlaceShot(Vector3 subject, IdleCameraShots.Frame frame, out Pose pose)
    {
        pose = default;
        Vector3 offset = frame.Position - subject;
        if (!TryPlace(subject, offset, out Vector3 position)) return false;
        float retained = (position - subject).sqrMagnitude / offset.sqrMagnitude;
        if (retained < 0.48f) return false;

        Vector3 aim = frame.LookAt - position;
        float length = aim.magnitude;
        if (length < 0.8f) return false;
        if (frame.ShotScale != IdleCameraShots.Scale.Scenery && Vector3.Angle(aim, subject - position) > 28f)
            return false;
        float clearance = frame.ShotScale == IdleCameraShots.Scale.Scenery ? 4.5f : 1.25f;
        if (Physics.SphereCast(position, 0.12f, aim / length, out _, Mathf.Min(length, clearance),
                TerrainMask, QueryTriggerInteraction.Ignore))
            return false;
        pose = new Pose(position, Quaternion.LookRotation(aim, Vector3.up));
        return true;
    }

    private static bool TryPlace(Vector3 target, Vector3 offset, out Vector3 position)
    {
        position = target;
        Vector3 direction = offset.normalized;
        float distance = offset.magnitude;
        if (distance < 0.65f) return false;
        if (Physics.SphereCast(target, 0.2f, direction, out RaycastHit hit, distance, TerrainMask, QueryTriggerInteraction.Ignore))
            distance = hit.distance - 0.12f;
        if (distance < 0.65f) return false;
        position = target + direction * distance;
        return !Physics.CheckSphere(position, 0.15f, TerrainMask, QueryTriggerInteraction.Ignore);
    }

    internal void ClearView(IdleCameraBlock reason = IdleCameraBlock.Camera)
    {
        alternateReady = false;
        IdleCameraActive = false;
        idle.Reset(reason);
        shots.Reset();
    }

    private void ResetView(Scene previous, Scene current)
    {
        Mode = 0;
        ClearView();
    }

    private void BeginRendering(ScriptableRenderContext context, Camera camera) => ApplyView(camera);

    internal void ApplyView(Camera camera)
    {
        if (!alternateReady || gameplayFrame != Time.frameCount || camera != GameplayCamera || renderedCamera != null)
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
