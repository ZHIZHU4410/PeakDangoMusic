using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakDangoMusic;

internal static class LyricPlacement
{
	internal static readonly Color TextColor = new Color(247f / 255f, 225f / 255f, 215f / 255f, 1f);

	internal static IEnumerable<Vector3> Candidates(Camera camera, MusicSettings settings, int serial)
	{
		float num = Mathf.Atan(Mathf.Tan(camera.fieldOfView * (MathF.PI / 180f) * 0.5f) * camera.aspect) * 57.29578f;
		float num2 = Mathf.Min(settings.Spread.Value, Mathf.Max(0f, num - 8f));
		float num3 = ((serial % 2 != 0) ? 1 : (-1));
		float[] array = new float[4]
		{
			num3 * num2 * 0.8f,
			num3 * num2 * 0.55f,
			(0f - num3) * num2 * 0.8f,
			(0f - num3) * num2 * 0.55f
		};
		foreach (float num4 in array)
		{
			float num5 = Random.Range(3f, 13f);
			Vector3 val = ((Component)camera).transform.position + ((Component)camera).transform.rotation * Quaternion.Euler(0f - num5, num4, 0f) * Vector3.forward * settings.Distance.Value;
			if (settings.AboveGround.Value)
			{
				val.y = Mathf.Max(val.y, ((Component)camera).transform.position.y - 0.5f);
			}
			yield return camera.WorldToViewportPoint(val);
		}
	}

	internal static Vector3 KeepDistance(Vector3 point, Camera camera, MusicSettings settings)
	{
		Vector3 val = point - ((Component)camera).transform.position;
		float magnitude = val.magnitude;
		float num = settings.Distance.Value * 0.6f;
		if (settings.KeepAway.Value && magnitude > 0.0001f && magnitude < num)
		{
			point = ((Component)camera).transform.position + val * (num / magnitude);
		}
		if (settings.AboveGround.Value)
		{
			point.y = Mathf.Max(point.y, ((Component)camera).transform.position.y - 0.5f);
		}
		return point;
	}

	internal static Quaternion Face(Vector3 point, Camera camera)
	{
		Vector3 val = point - ((Component)camera).transform.position;
		if (!(val.sqrMagnitude > 1E-06f))
		{
			return ((Component)camera).transform.rotation;
		}
		return Quaternion.LookRotation(val, ((Component)camera).transform.up);
	}

	static LyricPlacement()
	{
	}
}
