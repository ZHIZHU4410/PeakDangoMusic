using System;
using System.Reflection;
using UnityEngine;

namespace PeakDangoMusic;

/// <summary>
/// 特殊环境处理：水下、狭窄地形、反重力、混响区、迷雾和天气。
/// 天气不靠音量阈值判，直接看场景里雨雪风的粒子，飘起来就算。
/// </summary>
internal static class AmbienceMix
{
	private const BindingFlags Reflected = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

	private static Type? stormAudioType;
	private static Type? stormVisualType;
	private static Type? windZoneType;

	private static MonoBehaviour? stormAudio;
	private static MonoBehaviour? windZone;

	private static FieldInfo? stormVolumeField;
	private static FieldInfo? windVolumeField;
	private static FieldInfo? rainVolumeField;
	private static FieldInfo? stormVisualField;
	private static FieldInfo? rainVisualField;
	private static FieldInfo? windVisualField;
	private static FieldInfo? stormKindField;
	private static FieldInfo? visualWindField;
	private static FieldInfo? visualParticlesField;
	private static FieldInfo? visualQuadField;
	private static FieldInfo? windIntensityField;
	private static FieldInfo? windActiveField;
	private static FieldInfo? windDirectionField;

	private static UnityEngine.Object[]? visuals;
	private static float nextVisualScan;

	private static float diveDepth;
	private static float wetFor;
	private static float caveFor;
	private static float lastStormVolume;
	private static float lastWindVolume;
	private static float lastRainVolume;
	private static int seenParticles;
	private static float nextDiag;
	private static float nextSample;
	private static string seenKind = "";
	private static float seenStrength;
	private static Vector3 direction;
	private static float stormStrength;
	private static string kind = "";
	private static float mix;
	private static float windPhase;
	private static float indoorBlend;
	private static AmbienceRegions.Sample regions;
	private static float nextRegionProbe;
	private static float nextProbe;
	private static bool probed;
	private static int lastFrame = -1;
	private static string status = "还没探测到风暴组件";

	/// <summary>音乐保留的音量比例，1 表示原样。</summary>
	internal static float MusicScale = 1f;

	/// <summary>低通的截止频率，22000 表示不处理。</summary>
	internal static float Cutoff = 22000f;

	/// <summary>左右声道偏移，-1 到 1。</summary>
	internal static float Pan;

	/// <summary>播放速率，1 为原速。</summary>
	internal static float Pitch = 1f;

	/// <summary>回声湿度，0 是干声。</summary>
	internal static float EchoWet;
	internal static float EchoDelay = 0.1f;
	internal static float EchoFeedback = 0.42f;

	/// <summary>头顶遮挡程度，1 表示完全在洞里。</summary>
	internal static float Indoor;

	internal static float UnderWater => diveDepth;

	internal static string StatusText => status;

	internal static string KindText => kind switch
	{
		"Rain" => "下雨（耳机进水）",
		"Snow" => "下雪（发闷）",
		"Wind" => "大风（呼啸）",
		"Water" => "水下",
		"Cave" => "洞内／狭窄地形",
		"AntiGravity" => "反重力",
		"Reverb" => "地图混响区",
		"GloomFog" => "雾沼迷雾",
		_ => "正常（不处理）",
	};

	internal static void Tick(MusicSettings settings, float deltaTime)
	{
		if (Time.frameCount == lastFrame)
		{
			return;
		}
		lastFrame = Time.frameCount;

		bool audioEnabled = settings.AmbienceMix.Value;
		float audioDelta = Mathf.Min(deltaTime, 0.05f);
		if (!audioEnabled && (!settings.LyricWeather.Value || !settings.Enabled.Value))
		{
			nextSample = 0f;
			nextRegionProbe = 0f;
			regions = default;
			indoorBlend = 0f;
			ResetAudio(audioDelta);
			LyricWeather.Tick(false, "", 0f, 0f, Vector3.zero, deltaTime);
			return;
		}

		float now = Time.unscaledTime;
		if (!probed || stormAudio == null || windZone == null)
		{
			if (now >= nextProbe)
			{
				nextProbe = now + 1.5f;
				Probe();
			}
		}

		if (now >= nextRegionProbe)
		{
			nextRegionProbe = now + 0.18f;
			regions = AmbienceRegions.Read(Character.localCharacter, now);
		}
		diveDepth = regions.Water;
		indoorBlend = Mathf.Lerp(indoorBlend, regions.Enclosure, 1f - Mathf.Exp(-3f * audioDelta));

		if (now >= nextSample)
		{
			nextSample = now + 0.1f;
			seenStrength = ReadParticles(out seenKind);
			direction = windZone != null && windDirectionField?.GetValue(windZone) is Vector3 windVector
				? windVector : Vector3.zero;
			if (audioEnabled)
			{
				// Storm volume is a baseline. Visible particles gate the weather effect.
				stormStrength = ReadStormAudio(out _);
			}
		}
		LyricWeather.Tick(settings.LyricWeather.Value && settings.Enabled.Value, seenKind, seenStrength, indoorBlend, direction, deltaTime);
		if (!audioEnabled)
		{
			ResetAudio(audioDelta);
			return;
		}
		string heardKind = seenKind;
		float strength = seenKind.Length == 0 ? 0f : Mathf.Max(seenStrength, Mathf.Min(0.95f, 0.5f + stormStrength));

		// 水里待住才算，趟一下就走开不闷。
		wetFor = ((diveDepth > 0.2f) ? wetFor + audioDelta : 0f);
		caveFor = regions.Enclosure >= 0.45f ? caveFor + audioDelta : 0f;

		if (wetFor > 0.18f && (!regions.GloomFog || diveDepth >= 0.85f))
		{
			heardKind = "Water";
			strength = Mathf.Clamp01(0.35f + 0.65f * diveDepth);
		}
		else if (regions.AntiGravity > 0.01f)
		{
			heardKind = "AntiGravity";
			strength = Mathf.Lerp(0.42f, 1f, regions.AntiGravity);
		}
		else if (regions.GloomFog)
		{
			heardKind = "GloomFog";
			strength = 1f;
		}
		else if (regions.Reverb > 0.02f)
		{
			heardKind = "Reverb";
			strength = regions.Reverb;
		}
		else if (caveFor > 0.25f)
		{
			heardKind = "Cave";
			strength = 0.55f + 0.45f * regions.Enclosure;
		}
		else if (strength >= TriggerAt(heardKind))
		{
			float gate = TriggerAt(heardKind);
			strength = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01((strength - gate) / (1f - gate)));
		}
		else
		{
			strength = 0f;
			heardKind = "";
		}

		kind = heardKind;
		float rise = ((strength > mix) ? 2.6f : 1.1f);
		float blend = 1f - Mathf.Exp(-rise * audioDelta);
		mix = Mathf.Lerp(mix, strength, blend);
		windPhase += audioDelta * (0.6f + 1.4f * mix);

		float depth = Mathf.Clamp(settings.AmbienceFloor.Value, 0.3f, 1.6f);
		float loud = Mathf.Clamp01(strength * depth);
		bool weather = kind is "Rain" or "Snow" or "Wind";
		float coverage = weather ? Mathf.Lerp(loud, loud * 0.28f, indoorBlend) : loud;

		var effect = EffectFor(kind);
		float keep = Mathf.Clamp(1f - coverage * (1f - effect.Volume), 0.12f, 1f);

		float wobbleAmplitude = kind switch
		{
			"Wind" => 0.07f,
			"Rain" => 0.05f,
			"Snow" => 0.03f,
			"Water" => 0.03f,
			"Cave" => 0.02f,
			_ => 0f,
		};
		float wobbleSpeed = kind switch
		{
			"Rain" => 6.2f,
			"Wind" => 1.1f,
			"Snow" => 0.8f,
			_ => 1f,
		};
		float wobble = (kind.Length == 0) ? 0f : (Mathf.Sin(windPhase * wobbleSpeed) * wobbleAmplitude * coverage);
		float gain = Mathf.Clamp(keep * (1f - Mathf.Clamp(wobble, -0.45f, 0.6f)), 0.1f, 1f);
		MusicScale = Mathf.Lerp(MusicScale, gain, blend);

		float cutoff = Mathf.Lerp(Mathf.Log(22000f), Mathf.Log(effect.Cutoff), coverage);
		Cutoff = settings.AmbienceLowPass.Value ?
			Mathf.Exp(Mathf.Lerp(Mathf.Log(Cutoff), cutoff, blend)) : 22000f;

		float speedWobble = (kind == "Rain") ? 3.1f : 2.2f;
		float amountWobble = (kind == "Rain") ? 0.012f : 0.02f;
		float drifting = ((kind == "Rain" || kind == "Wind") ? (Mathf.Sin(windPhase * speedWobble) * amountWobble) : 0f);
		float pitch = Mathf.Clamp(1f - (1f - effect.Pitch) * (loud * 0.9f) + drifting * coverage, 0.6f, 1.1f);
		Pitch = Mathf.Lerp(Pitch, pitch, blend);

		float wet = effect.Wet * loud * (weather ? 1f - 0.85f * indoorBlend : 1f);
		EchoWet = Mathf.Lerp(EchoWet, wet, blend);
		if (strength > 0f)
		{
			EchoDelay = Mathf.Lerp(EchoDelay, kind == "Reverb" ? regions.ReflectionDelay : effect.Delay, blend);
			EchoFeedback = Mathf.Lerp(EchoFeedback, kind == "Reverb" ? regions.Feedback : effect.Feedback, blend);
		}

		float sway = kind switch
		{
			"Wind" => 0.34f,
			"Snow" => 0.06f,
			"Rain" => 0.09f,
			_ => 0.05f,
		};
		float pan = Mathf.Clamp(Mathf.Sin(windPhase * 0.82f) * sway * (1f - 0.6f * indoorBlend) * Mathf.Clamp01(strength * 1.2f), -1f, 1f);
		Pan = Mathf.Lerp(Pan, pan, blend);
		Indoor = indoorBlend;

		if (Time.unscaledTime >= nextDiag)
		{
			nextDiag = Time.unscaledTime + 15f;
			DiagnosticLog.Write($"[环境音] storm={lastStormVolume:F2} wind={lastWindVolume:F2} rain={lastRainVolume:F2} 粒子={seenKind}:{seenStrength:F2}/{seenParticles}颗 判定={kind}:{strength:F2} 混合={mix:F3} 保留={(MusicScale * 100f):F0}% 低通={Cutoff:F0} 遮挡={indoorBlend:F2} 反重={regions.AntiGravity:F2} 混响={regions.Reverb:F2} 水深={diveDepth:F2}");
		}
	}

	private static void ResetAudio(float dt)
	{
		wetFor = caveFor = diveDepth = stormStrength = 0f;
		mix = Mathf.MoveTowards(mix, 0f, dt * 2f);
		MusicScale = Pitch = 1f;
		Cutoff = 22000f;
		Pan = EchoWet = Indoor = 0f;
		EchoDelay = 0.1f;
		EchoFeedback = 0.42f;
		windPhase = 0f;
		kind = "";
	}

	private static float TriggerAt(string kindName)
	{
		return kindName switch
		{
			"Rain" => 0.3f,
			"Snow" => 0.3f,
			"Wind" => 0.3f,
			_ => 0.55f,
		};
	}

	private static (float Volume, float Cutoff, float Pitch, float Wet, float Delay, float Feedback) EffectFor(string kindName) =>
		kindName switch
		{
			"Rain" => (0.6f, 380f, 0.97f, 0.05f, 0.1f, 0.42f),
			"Snow" => (0.74f, 480f, 0.95f, 0.08f, 0.1f, 0.42f),
			"Wind" => (0.55f, 800f, 0.95f, 0.1f, 0.1f, 0.42f),
			"Water" => (0.5f, 470f, 0.95f, 0.05f, 0.1f, 0.42f),
			"GloomFog" => (0.78f, 1900f, 1f, 0.23f, 0.125f, 0.4f),
			"Cave" => (0.86f, 4200f, 0.99f, 0.39f, 0.1f, 0.42f),
			"AntiGravity" => (0.84f, 4600f, 0.96f, 0.46f, 0.165f, 0.48f),
			"Reverb" => (0.89f, 6200f, 1f, 0.31f, 0.1f, 0.42f),
			_ => (1f, 22000f, 1f, 0f, 0.1f, 0.42f),
		};

	// 只认风暴粒子真的在飘：游戏的风暴音量常年停在 0.25/0.35/0.25 的环境基线，不能当开关用。
	// 粒子数太少只可能是远处飘的一点雪沫，至少 12 颗才算天气压过来。
	private static float ReadParticles(out string seenKind)
	{
		seenKind = "";
		seenParticles = 0;
		if (stormVisualType == null)
		{
			return 0f;
		}

		float now = Time.unscaledTime;
		if (now >= nextVisualScan)
		{
			nextVisualScan = now + 2f;
			try
			{
				visuals = UnityEngine.Object.FindObjectsOfType(stormVisualType);
			}
			catch
			{
				visuals = null;
			}
		}
		if (visuals == null)
		{
			return 0f;
		}

		float best = 0f;
		float strongest = -1f;
		for (int i = 0; i < visuals.Length; i++)
		{
			Component? visual = visuals[i] as Component;
			if (visual == null || !visual.gameObject.activeInHierarchy)
			{
				continue;
			}
			int count = ParticleCount(visual);
			if (count < 12)
			{
				continue;
			}
			string name = FieldText(stormKindField, visual);
			if (name.Length == 0)
			{
				continue;
			}
			float wind = FieldFloat(visualWindField, visual);
			// 风要真的刮起来：风强度也得上去，光有零星尘粒不算。
			if (name == "Wind" && wind < 0.45f)
			{
				continue;
			}
			if (best == 0f || wind > strongest)
			{
				strongest = wind;
				seenKind = name;
				best = Mathf.Max(0.78f, wind);
			}
		}
		return best;
	}

	// 只数真正的粒子。风的风暴面片在每张图都常开，绝不能当天气证据，所以这里不看 quad。
	private static int ParticleCount(Component visual)
	{
		int total = 0;
		try
		{
			if (visualParticlesField != null)
			{
				object? value = visualParticlesField.GetValue(visual);
				if (value is ParticleSystem[] systems)
				{
					foreach (ParticleSystem system in systems)
					{
						if (system != null && system.isPlaying)
						{
							total += system.particleCount;
						}
					}
				}
			}
		}
		catch
		{
		}
		return total;
	}

	private static void Probe()
	{
		probed = true;

		windZoneType ??= LookupType("WindChillZone");
		stormAudioType ??= LookupType("StormAudio");
		stormVisualType ??= LookupType("StormVisual");

		if (stormAudioType != null)
		{
			stormVolumeField ??= FieldOf(stormAudioType, "stormVolume");
			windVolumeField ??= FieldOf(stormAudioType, "windVolume");
			rainVolumeField ??= FieldOf(stormAudioType, "rainVolume");
			stormVisualField ??= FieldOf(stormAudioType, "stormVisual");
			rainVisualField ??= FieldOf(stormAudioType, "rainVisual");
			windVisualField ??= FieldOf(stormAudioType, "windVisual");
			if (stormAudio == null) stormAudio = FirstComponent(stormAudioType);
		}

		if (stormVisualType != null)
		{
			stormKindField ??= FieldOf(stormVisualType, "stormType");
			visualWindField ??= FieldOf(stormVisualType, "windIntensity");
			visualParticlesField ??= FieldOf(stormVisualType, "part");
			visualQuadField ??= FieldOf(stormVisualType, "quadRend");
		}

		if (windZoneType != null)
		{
			windIntensityField ??= FieldOf(windZoneType, "windIntensity");
			windActiveField ??= FieldOf(windZoneType, "windActive");
			windDirectionField ??= FieldOf(windZoneType, "currentWindDirection");

			if (windZone == null)
			{
				FieldInfo? instance = FieldOf(windZoneType, "instance");
				if (instance != null)
				{
					try
					{
						windZone = instance.GetValue(null) as MonoBehaviour;
					}
					catch
					{
						windZone = null;
					}
				}
				windZone ??= FirstComponent(windZoneType);
			}
		}

		status = stormAudio != null ? "已连接风暴音源" : "未找到风暴音源（可能不在风暴地图）";
		if (stormVisualType != null)
		{
			status += " · 天气粒子可读";
		}
		if (windZone != null)
		{
			status += " · 风场已连接";
		}
	}

	private static float ReadStormAudio(out string heardKind)
	{
		heardKind = "";
		float storm = 0f;
		float wind = 0f;
		float rain = 0f;

		if (stormAudio != null)
		{
			storm = FieldFloat(stormVolumeField, stormAudio);
			wind = FieldFloat(windVolumeField, stormAudio);
			rain = FieldFloat(rainVolumeField, stormAudio);
			lastStormVolume = storm;
			lastWindVolume = wind;
			lastRainVolume = rain;

			object? visual;
			if (rain >= wind && rain >= storm)
			{
				visual = FieldValue(rainVisualField, stormAudio);
			}
			else if (wind >= storm)
			{
				visual = FieldValue(windVisualField, stormAudio);
			}
			else
			{
				visual = FieldValue(stormVisualField, stormAudio);
			}
			heardKind = FieldText(stormKindField, visual);
		}

		float loudest = Mathf.Max(storm, Mathf.Max(wind, rain));

		if (loudest < 0.04f)
		{
			heardKind = "";
		}
		return Mathf.Clamp01(loudest);
	}

	private static string FieldText(FieldInfo? field, object? target)
	{
		if (field == null || target == null)
		{
			return "";
		}
		try
		{
			return field.GetValue(target)?.ToString() ?? "";
		}
		catch
		{
			return "";
		}
	}

	private static object? FieldValue(FieldInfo? field, object target)
	{
		if (field == null)
		{
			return null;
		}
		try
		{
			return field.GetValue(target);
		}
		catch
		{
			return null;
		}
	}

	private static float FieldFloat(FieldInfo? field, object? target)
	{
		if (field == null || target == null)
		{
			return 0f;
		}
		try
		{
			object? value = field.GetValue(target);
			if (value is float single)
			{
				return single;
			}
			if (value is int whole)
			{
				return Mathf.Clamp01(whole);
			}
		}
		catch
		{
		}
		return 0f;
	}

	private static FieldInfo? FieldOf(Type type, string name)
	{
		try
		{
			return type.GetField(name, Reflected);
		}
		catch
		{
			return null;
		}
	}

	private static MonoBehaviour? FirstComponent(Type type)
	{
		try
		{
			return UnityEngine.Object.FindObjectOfType(type) as MonoBehaviour;
		}
		catch
		{
			return null;
		}
	}

	private static Type? LookupType(string name)
	{
		foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			try
			{
				Type? type = assembly.GetType(name, throwOnError: false);
				if (type != null)
				{
					return type;
				}
			}
			catch
			{
			}
		}
		return null;
	}
}
