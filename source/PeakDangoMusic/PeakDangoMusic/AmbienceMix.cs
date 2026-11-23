using System;
using System.Reflection;
using UnityEngine;

namespace PeakDangoMusic;

/// <summary>
/// 特殊环境处理：水下、洞内、雨雪大风。
/// 平时跑图完全不碰音乐，只有这几种情况才动手。
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

	private static Type? waterZoneType;
	private static Type? characterType;
	private static FieldInfo? localCharacterField;
	private static FieldInfo? inWaterField;
	private static FieldInfo? zoneBoundsField;
	private static FieldInfo? waterPlaneField;
	private static UnityEngine.Object[]? waterZones;
	private static float nextWaterScan;

	private static float diveDepth;
	private static float playerWater = -1f;
	private static float wetFor;
	private static float caveFor;
	private static float lastStormVolume;
	private static float lastWindVolume;
	private static float lastRainVolume;
	private static int seenParticles;
	private static float nextDiag;
	private static string kind = "";
	private static float mix;
	private static float windPhase;
	private static float indoorBlend;
	private static float indoorTarget;
	private static float nextIndoorProbe;
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
		"Cave" => "洞内",
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
		if (!audioEnabled && (!settings.LyricWeather.Value || !settings.Enabled.Value))
		{
			ResetAudio(deltaTime);
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

		TrackIndoor(now, deltaTime);

		// 风暴音量是环境基线（常年 0.25/0.35/0.25），只拿来给强度做参考，绝不拿来当开关。
		float seenStrength = ReadParticles(out string seenKind);
		Vector3 direction = windZone != null && windDirectionField?.GetValue(windZone) is Vector3 windVector
			? windVector : Vector3.zero;
		LyricWeather.Tick(settings.LyricWeather.Value && settings.Enabled.Value, seenKind, seenStrength, indoorBlend, direction, deltaTime);
		if (!audioEnabled)
		{
			ResetAudio(deltaTime);
			return;
		}
		float strength = ReadStormAudio(out string heardKind);
		if (seenKind.Length > 0)
		{
			heardKind = seenKind;
			strength = Mathf.Max(seenStrength, Mathf.Min(0.95f, 0.5f + strength));
		}
		else
		{
			heardKind = "";
			strength = 0f;
		}

		// 角色自己的入水数值最准：沼泽、浅滩里相机还在水面以上，用相机高度根本测不到。
		playerWater = ReadPlayerWater();
		diveDepth = ((playerWater >= 0f) ? playerWater : ReadDiveDepth(now));

		// 水里待住才算，趟一下就走开不闷。
		wetFor = ((diveDepth > 0.2f) ? wetFor + deltaTime : 0f);
		caveFor = ((indoorBlend > 0.35f) ? caveFor + deltaTime : 0f);

		if (wetFor > 0.5f)
		{
			heardKind = "Water";
			strength = Mathf.Clamp01(0.35f + 0.65f * diveDepth);
		}
		else if (caveFor > 0.9f)
		{
			heardKind = "Cave";
			strength = 0.85f + 0.15f * indoorBlend;
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
		mix = Mathf.Lerp(mix, strength, 1f - Mathf.Exp(0f - rise * Mathf.Max(0.0001f, deltaTime)));
		windPhase += deltaTime * (0.6f + 1.4f * mix);

		float gust = 0.5f + 0.5f * Mathf.Sin(windPhase);
		float depth = Mathf.Clamp(settings.AmbienceFloor.Value, 0.3f, 1.6f);
		float loud = Mathf.Clamp01(mix * depth);
		float coverage = ((kind == "Cave") ? loud : Mathf.Lerp(loud, loud * 0.28f, indoorBlend));

		float floor = FloorFor(kind);
		float keep = Mathf.Clamp(1f - coverage * (1f - floor), 0.12f, 1f);

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
		MusicScale = Mathf.Clamp(keep * (1f - Mathf.Clamp(wobble, -0.45f, 0.6f)), 0.1f, 1f);

		float open = Mathf.Clamp01(coverage);
		Cutoff = settings.AmbienceLowPass.Value ? Mathf.Exp(Mathf.Lerp(Mathf.Log(22000f), Mathf.Log(CutoffFor(kind)), open)) : 22000f;

		float speedWobble = (kind == "Rain") ? 3.1f : 2.2f;
		float amountWobble = (kind == "Rain") ? 0.012f : 0.02f;
		float drifting = ((kind == "Rain" || kind == "Wind") ? (Mathf.Sin(windPhase * speedWobble) * amountWobble) : 0f);
		Pitch = Mathf.Clamp(1f - (1f - PitchFor(kind)) * Mathf.Clamp01(loud * 0.9f) + drifting * coverage, 0.6f, 1.1f);

		EchoWet = EchoFor(kind) * Mathf.Clamp01(loud) * ((kind == "Cave") ? 1f : (1f - 0.85f * indoorBlend));

		float sway = kind switch
		{
			"Wind" => 0.34f,
			"Snow" => 0.06f,
			"Rain" => 0.09f,
			_ => 0.05f,
		};
		Pan = Mathf.Clamp(Mathf.Sin(windPhase * 0.82f) * sway * (1f - 0.6f * indoorBlend) * Mathf.Clamp01(mix * 1.2f), -1f, 1f);
		Indoor = indoorBlend;

		if (Time.unscaledTime >= nextDiag)
		{
			nextDiag = Time.unscaledTime + 1f;
			DiagnosticLog.Write($"[环境音] storm={lastStormVolume:F2} wind={lastWindVolume:F2} rain={lastRainVolume:F2} 粒子={seenKind}:{seenStrength:F2}/{seenParticles}颗 判定={kind}:{strength:F2} 混合={mix:F3} 保留={(MusicScale * 100f):F0}% 低通={Cutoff:F0} 洞={indoorBlend:F2} 角色水={playerWater:F2} 水深={diveDepth:F2}");
		}
	}

	private static void ResetAudio(float dt)
	{
		mix = Mathf.MoveTowards(mix, 0f, dt * 2f);
		MusicScale = Pitch = 1f;
		Cutoff = 22000f;
		Pan = EchoWet = Indoor = 0f;
		windPhase = 0f;
		kind = "";
		status = "已关闭";
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

	private static float FloorFor(string kindName)
	{
		return kindName switch
		{
			"Rain" => 0.6f,
			"Snow" => 0.74f,
			"Wind" => 0.55f,
			"Water" => 0.5f,
			"Cave" => 0.9f,
			_ => 0.85f,
		};
	}

	private static float CutoffFor(string kindName)
	{
		return kindName switch
		{
			"Rain" => 380f,
			"Snow" => 480f,
			"Wind" => 800f,
			"Water" => 500f,
			"Cave" => 6000f,
			_ => 22000f,
		};
	}

	private static float PitchFor(string kindName)
	{
		return kindName switch
		{
			"Rain" => 0.97f,
			"Snow" => 0.95f,
			"Wind" => 0.95f,
			"Water" => 0.95f,
			"Cave" => 0.99f,
			_ => 1f,
		};
	}

	private static float EchoFor(string kindName)
	{
		return kindName switch
		{
			"Rain" => 0.05f,
			"Snow" => 0.08f,
			"Wind" => 0.1f,
			"Water" => 0.05f,
			"Cave" => 0.3f,
			_ => 0f,
		};
	}

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

	// 直接问角色“你现在多深在水里”，游戏自己算好了，比用相机高度猜靠谱。
	// 返回 -1 表示读不到，退回用相机高度估。
	private static float ReadPlayerWater()
	{
		if (characterType == null)
		{
			characterType = LookupType("Character");
			if (characterType != null)
			{
				localCharacterField = FieldOf(characterType, "localCharacter");
				inWaterField = FieldOf(characterType, "inWater");
			}
		}
		if (localCharacterField == null || inWaterField == null)
		{
			return -1f;
		}

		object? local;
		try
		{
			local = localCharacterField.GetValue(null);
		}
		catch
		{
			return -1f;
		}
		if (local == null)
		{
			return -1f;
		}

		float wetValue = FieldFloat(inWaterField, local);
		return Mathf.Clamp01(wetValue);
	}

	private static float ReadDiveDepth(float now)
	{
		if (waterZoneType == null)
		{
			waterZoneType = LookupType("WaterZone");
			if (waterZoneType == null)
			{
				return 0f;
			}
			zoneBoundsField = FieldOf(waterZoneType, "zoneBounds");
			waterPlaneField = FieldOf(waterZoneType, "waterPlane");
		}

		if ((waterZones == null || waterZones.Length == 0) && now >= nextWaterScan)
		{
			nextWaterScan = now + 4f;
			try
			{
				waterZones = UnityEngine.Object.FindObjectsOfType(waterZoneType);
			}
			catch
			{
				waterZones = null;
			}
		}

		Camera? camera = Camera.main;
		if (camera == null || waterZones == null)
		{
			return 0f;
		}

		Vector3 position = camera.transform.position;
		float deepest = 0f;
		for (int i = 0; i < waterZones.Length; i++)
		{
			Component? zone = waterZones[i] as Component;
			if (zone == null)
			{
				continue;
			}
			if (!Bounds(zone, out float surface, out float minX, out float maxX, out float minZ, out float maxZ))
			{
				continue;
			}
			if (position.x < minX - 1f || position.x > maxX + 1f || position.z < minZ - 1f || position.z > maxZ + 1f)
			{
				continue;
			}
			float depth = surface - position.y;
			if (depth > 0.15f)
			{
				deepest = Mathf.Max(deepest, Mathf.Clamp01(depth / 1.6f));
			}
		}
		return deepest;
	}

	private static bool Bounds(Component zone, out float surface, out float minX, out float maxX, out float minZ, out float maxZ)
	{
		surface = 0f;
		minX = 0f;
		maxX = 0f;
		minZ = 0f;
		maxZ = 0f;
		try
		{
			if (waterPlaneField != null)
			{
				Renderer? plane = waterPlaneField.GetValue(zone) as Renderer;
				if (plane != null)
				{
					Bounds bounds = plane.bounds;
					surface = bounds.max.y;
					minX = bounds.min.x;
					maxX = bounds.max.x;
					minZ = bounds.min.z;
					maxZ = bounds.max.z;
					return true;
				}
			}
			if (zoneBoundsField != null)
			{
				object? value = zoneBoundsField.GetValue(zone);
				if (value is Bounds bounds)
				{
					surface = bounds.max.y;
					minX = bounds.min.x;
					maxX = bounds.max.x;
					minZ = bounds.min.z;
					maxZ = bounds.max.z;
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private static void TrackIndoor(float now, float deltaTime)
	{
		if (now >= nextIndoorProbe)
		{
			nextIndoorProbe = now + 0.35f;
			Camera? camera = Camera.main;
			if (camera != null)
			{
				bool covered;
				try
				{
					covered = Physics.Raycast(camera.transform.position + Vector3.up * 0.2f, Vector3.up, 12f, ~0, QueryTriggerInteraction.Collide);
				}
				catch
				{
					covered = false;
				}
				indoorTarget = covered ? 1f : 0f;
			}
		}
		indoorBlend = Mathf.Lerp(indoorBlend, indoorTarget, 1f - Mathf.Exp(0f - 3f * Mathf.Max(0.0001f, deltaTime)));
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
