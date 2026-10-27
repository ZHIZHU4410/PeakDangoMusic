using UnityEngine;

namespace PeakDangoMusic;

internal static class LyricPlacement
{
    internal static readonly Color TextColor=new(247/255f,225/255f,215/255f,1);
    internal static IEnumerable<Vector3> Candidates(Camera camera,MusicSettings settings,int serial)
    {
        float horizontal=Mathf.Atan(Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)*camera.aspect)*Mathf.Rad2Deg;
        float spread=Mathf.Min(settings.Spread.Value,Mathf.Max(0,horizontal-8));
        float side=(serial%2==0)?-1:1;
        foreach(float yaw in new[]{side*spread*.8f,side*spread*.55f,-side*spread*.8f,-side*spread*.55f})
        {
            float pitch=UnityEngine.Random.Range(3f,13f);
            Vector3 point=camera.transform.position+camera.transform.rotation*Quaternion.Euler(-pitch,yaw,0)*Vector3.forward*settings.Distance.Value;
            if(settings.AboveGround.Value)point.y=Mathf.Max(point.y,camera.transform.position.y-.5f);
            yield return camera.WorldToViewportPoint(point);
        }
    }
    internal static Vector3 KeepDistance(Vector3 point,Camera camera,MusicSettings settings)
    {
        var ray=point-camera.transform.position;float length=ray.magnitude,min=settings.Distance.Value*.6f;
        if(settings.KeepAway.Value && length>.0001f && length<min)point=camera.transform.position+ray*(min/length);
        if(settings.AboveGround.Value)point.y=Mathf.Max(point.y,camera.transform.position.y-.5f);
        return point;
    }
    internal static Quaternion Face(Vector3 point,Camera camera)
    {
        var ray=point-camera.transform.position;
        return ray.sqrMagnitude>.000001f?Quaternion.LookRotation(ray,camera.transform.up):camera.transform.rotation;
    }
}
