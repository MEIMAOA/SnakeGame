using System;
using System.Collections.Generic;
using System.IO;
using ArknightsTD.Core;
using ArknightsTD.Core.Geometry;
using ArknightsTD.Snake;
using ArknightsTD.View;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsTD.SnakeView
{

public sealed class SnakeWorldView
{
	public const float OverlayZ = -0.02f;
	/// <summary>场地层 z：比实体层更远（相机朝 +Z），保证实体永远盖在地面之上。</summary>
	public const float FloorZ = 0.02f;
	/// <summary>底层 z：介于场地与实体之间，用于占领圈等"贴地但要在实体之下"的效果。</summary>
	public const float UnderZ = 0.01f;

	private readonly Transform _root;

	private readonly Material _opaqueMat;

	private readonly Material _blendMat;

	private readonly SnakeWorld _world;

	private MeshFilter _opaqueMf;

	private MeshFilter _blendMf;
	private MeshFilter _floorMf;
	private MeshFilter _underMf;

	private readonly List<Vec2> _spawnPos = new List<Vec2>();

	private readonly List<double> _spawnProgress = new List<double>();

	public Transform Root => _root;

	public SnakeWorldView(Transform parent, SnakeWorld world)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		_world = world;
		_root = new GameObject("SnakeWorld2D").transform;
		if ((UnityEngine.Object)(object)parent != (UnityEngine.Object)null)
		{
			_root.SetParent(parent, false);
		}
		_opaqueMat = TdMaterials.Solid();
		_blendMat = TdMaterials.Transparent(Rgb.FromHex(16777215u));
		_opaqueMf = MakeNode("Opaque", _opaqueMat);
		_blendMf = MakeNode("Overlay", _blendMat);
		_floorMf = MakeNode("Floor", _opaqueMat);
	}

	private MeshFilter MakeNode(string name, Material mat)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject(name);
		val.transform.SetParent(_root, false);
		MeshFilter result = val.AddComponent<MeshFilter>();
		MeshRenderer val2 = val.AddComponent<MeshRenderer>();
		((Renderer)val2).sharedMaterial = mat;
		((Renderer)val2).shadowCastingMode = (ShadowCastingMode)0;
		((Renderer)val2).receiveShadows = false;
		((Renderer)val2).lightProbeUsage = (LightProbeUsage)0;
		((Renderer)val2).reflectionProbeUsage = (ReflectionProbeUsage)0;
		return result;
	}

	public void Sync(SnakeWorld w)
	{
		if (w != null)
		{
			MeshBuilder meshBuilder = new MeshBuilder();
			MeshBuilder meshBuilder2 = new MeshBuilder();
			var floorMb = new MeshBuilder();
			var underMb = new MeshBuilder();
			BuildArena(floorMb, w);
			if (w.Mode == SnakeMode.Timed)
			{
				BuildRoads(meshBuilder, w);
			}
			if (w.Mode == SnakeMode.Team4v4)
			{
				BuildCaptureZone(meshBuilder, meshBuilder2, w);
			}
			if (w.Mode == SnakeMode.Timed)
			{
				BuildPoison(meshBuilder, w);
			}
			if (w.Mode == SnakeMode.Extraction)
			{
				BuildExtractionPoint(meshBuilder, meshBuilder2, w);
				BuildContainers(meshBuilder, meshBuilder2, w);
				BuildFirebombs(meshBuilder, w);
			}
			if (w.Mode == SnakeMode.Classic || w.Mode == SnakeMode.Extreme)
			{
				BuildSpawnHints(meshBuilder2, w);
			}
			BuildFoods(meshBuilder, w);
			BuildPowerUps(meshBuilder, meshBuilder2, w);
			BuildPlayer(meshBuilder, meshBuilder2, w);
			BuildEnemies(meshBuilder, w);
			if (w.Mode == SnakeMode.Timed)
			{
				BuildRace(meshBuilder, meshBuilder2, w);
			}
			BuildParticles(meshBuilder2, w);
			SetMesh(_floorMf, floorMb, "SnakeFloor", FloorZ);
			SetMesh(_underMf, underMb, "SnakeUnderlay", UnderZ);
			SetMesh(_opaqueMf, meshBuilder, "SnakeOpaque", 0f);
			SetMesh(_blendMf, meshBuilder2, "SnakeOverlay", -0.02f);
		}
	}

	public void SetVisible(bool visible)
	{
		if ((UnityEngine.Object)(object)_root != (UnityEngine.Object)null)
		{
			((Component)_root).gameObject.SetActive(visible);
		}
	}

	private void BuildArena(MeshBuilder solid, SnakeWorld w)
	{
		double worldHalfW = w.WorldHalfW;
		double worldHalfH = w.WorldHalfH;
		double scale = w.Scale;
			switch (SnakeSkin.Background)
			{
			case SnakeBackground.SolidBlack:
			case SnakeBackground.Starfield:
				solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(0u));
				break;
			case SnakeBackground.SolidWhite:
			case SnakeBackground.Grid:
				solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(16777215u));
				break;
			default:
				solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(0u));
				break;
			}
			Primitives2D.Rect(solid, 0.0 - worldHalfW, 0.0 - worldHalfH, worldHalfW, worldHalfH);
			if (SnakeSkin.Background == SnakeBackground.Grid)
			{
				solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(16119285u));
				double num = 40.0 * scale;
				double halfWidth = 0.5 * scale;
				for (double num2 = 0.0 - worldHalfW; num2 <= worldHalfW + 1E-09; num2 += num)
				{
					Primitives2D.Segment(solid, num2, 0.0 - worldHalfH, num2, worldHalfH, halfWidth);
				}
				for (double num3 = 0.0 - worldHalfH; num3 <= worldHalfH + 1E-09; num3 += num)
				{
					Primitives2D.Segment(solid, 0.0 - worldHalfW, num3, worldHalfW, num3, halfWidth);
				}
			}
			else if (SnakeSkin.Background == SnakeBackground.Starfield)
			{
				System.Random random = new System.Random(20240);
				for (int i = 0; i < 100; i++)
				{
					byte r = (byte)random.Next(100, 255);
					byte g = (byte)random.Next(100, 255);
					byte b = (byte)random.Next(100, 255);
					double num4 = 0.0 - worldHalfW + random.NextDouble() * worldHalfW * 2.0;
					double num5 = 0.0 - worldHalfH + random.NextDouble() * worldHalfH * 2.0;
					solid.FaceTint = ArkPalette.FaceShader(new Rgb(r, g, b));
					Primitives2D.Rect(solid, num4, num5, num4 + 1.0, num5 + 1.0);
				}
			}
		solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(16711680u));
		double num6 = 3.0 * scale;
		Primitives2D.Rect(solid, 0.0 - worldHalfW, 0.0 - worldHalfH, worldHalfW, 0.0 - worldHalfH + num6);
		Primitives2D.Rect(solid, 0.0 - worldHalfW, worldHalfH - num6, worldHalfW, worldHalfH);
		Primitives2D.Rect(solid, 0.0 - worldHalfW, 0.0 - worldHalfH, 0.0 - worldHalfW + num6, worldHalfH);
		Primitives2D.Rect(solid, worldHalfW - num6, 0.0 - worldHalfH, worldHalfW, worldHalfH);
	}

	private void BuildRoads(MeshBuilder solid, SnakeWorld w)
	{
		solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(13882323u));
		Primitives2D.Rect(solid, 0.0 - w.WorldHalfW, -50.0, w.WorldHalfW, 50.0);
		Primitives2D.Rect(solid, -50.0, 0.0 - w.WorldHalfH, 50.0, w.WorldHalfH);
	}

	/// <summary>
	/// 占领模式指引：从玩家蛇头到占领点画一串短虚线，并在占领点画脉冲圆环，
	/// 让玩家一眼看出该往哪走（明日方舟式的引导表现）。
	/// </summary>
	private void BuildCaptureGuide(MeshBuilder blend, SnakeWorld w)
	{
		double px = w.Player.Head.X, py = w.Player.Head.Y;
		double zx = w.CaptureZone.X, zy = w.CaptureZone.Y;
		double dx = zx - px, dy = zy - py;
		double len = System.Math.Sqrt(dx * dx + dy * dy);
		if (len < 1e-6) return;
		dx /= len; dy /= len;

		double s = w.Scale;
		blend.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(0x3FD8E8));
		for (double d = 30 * s; d < len - 25 * s; d += 34 * s)
		{
			double sx = px + dx * d, sy = py + dy * d;
			double hw = 5 * s;
			Primitives2D.Segment(blend, sx - dy * hw, sy + dx * hw, sx + dy * hw, sy - dx * hw, 2.2 * s);
		}

		// 目标处脉冲圆环
		double pulse = 150 * s * (1.0 + 0.10 * System.Math.Sin(w.Time * 4.0));
		for (int i = 0; i < 3; i++)
		{
			double rr = pulse - i * 9 * s;
			if (rr > 6 * s) Primitives2D.Ring(blend, zx, zy, rr - 2.5 * s, rr, 56);
		}
	}

	private void BuildCaptureZone(MeshBuilder solid, MeshBuilder under, SnakeWorld w)
	{
		float num = (float)w.CaptureZone.X;
		float num2 = (float)w.CaptureZone.Y;
		float num3 = (float)(150.0 * w.Scale);
		Rgb baseColor;
		if (w.CaptureProgress > 5.0)
		{
			baseColor = Rgb.FromHex(255u);
		}
		else
		{
			baseColor = ((!(w.CaptureProgress < -5.0)) ? Rgb.FromHex(8421504u) : Rgb.FromHex(16711680u));
		}
		under.FaceTint = ArkPalette.FaceShader(baseColor);
		Primitives2D.Disc(under, num, num2, num3, 64);
		solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(0u));
		double num4 = Math.Max(0.5, 1.0 * w.Scale);
		Primitives2D.Ring(solid, num, num2, (double)num3 - num4, num3, 64);
	}

	private void BuildPoison(MeshBuilder solid, SnakeWorld w)
	{
		double num = w.PoisonRadius * w.Scale;
		if (!(num <= 0.0))
		{
			solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(16711680u));
			double num2 = 3.0 * w.Scale;
			Primitives2D.Ring(solid, (float)w.PoisonCenter.X, (float)w.PoisonCenter.Y, (float)(num - num2), (float)num, 128);
		}
	}

	private void BuildSpawnHints(MeshBuilder blend, SnakeWorld w)
	{
		_world.GetPendingSpawns(_spawnPos, _spawnProgress);
		for (int i = 0; i < _spawnPos.Count; i++)
		{
			double num = _spawnProgress[i];
			double num2 = 30.0 * w.Scale * num;
			if (!(num2 <= 0.0))
			{
				blend.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(16711680u));
				Primitives2D.Disc(blend, _spawnPos[i].X, _spawnPos[i].Y, num2, 20);
			}
		}
	}

	private void BuildRace(MeshBuilder solid, MeshBuilder blend, SnakeWorld w)
	{
		if (w.IsRacing)
		{
			float num = (float)w.RaceDestination.X;
			float num2 = (float)w.RaceDestination.Y;
			double num3 = 30.0 * w.Scale;
			solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(16766720u));
			Primitives2D.Disc(solid, num, num2, num3, 24);
			solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(16747520u));
			double num4 = 3.0 * w.Scale;
			Primitives2D.Ring(solid, num, num2, num3 - num4, num3);
			double halfWidth = 1.0 * w.Scale;
			DrawDashedLine(blend, w.Player.Head, w.RaceDestination, Rgb.FromHex(16766720u), halfWidth, 0.55);
			int raceOpponentIndex = w.RaceOpponentIndex;
			if (raceOpponentIndex >= 0 && raceOpponentIndex < w.Enemies.Count && w.Enemies[raceOpponentIndex].Points.Count > 0)
			{
				DrawDashedLine(blend, w.Enemies[raceOpponentIndex].Head, w.RaceDestination, Rgb.FromHex(16711680u), halfWidth, 0.55);
			}
		}
	}

	private void BuildExtractionPoint(MeshBuilder solid, MeshBuilder blend, SnakeWorld w)
	{
		float num = (float)w.ExtractionPoint.X;
		float num2 = (float)w.ExtractionPoint.Y;
		float num3 = (float)(w.ExtractionRadius * w.Scale);
		blend.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(65280u));
		Primitives2D.Disc(blend, num, num2, num3, 64);
		solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(32768u));
		double num4 = 2.0 * w.Scale;
		Primitives2D.Ring(solid, num, num2, (double)num3 - num4, num3, 64);
		if (w.ExtractionTimer > 0.0 && w.ExtractionTimer < 9.0)
		{
			double num5 = w.ExtractionTimer / 9.0;
			double num6 = 200.0 * w.Scale;
			double num7 = 20.0 * w.Scale;
			double num8 = (double)num - num6 / 2.0;
			double num9 = (double)(num2 + num3) + 30.0 * w.Scale;
			solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(8421504u));
			Primitives2D.Rect(solid, num8, num9, num8 + num6, num9 + num7);
			solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(65280u));
			Primitives2D.Rect(solid, num8, num9, num8 + num6 * num5, num9 + num7);
			solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(16777215u));
			double num10 = 1.0 * w.Scale;
			Primitives2D.Rect(solid, num8 - num10, num9 - num10, num8 + num6 + num10, num9 + num10);
			Primitives2D.Rect(solid, num8 - num10, num9 + num7, num8 + num6 + num10, num9 + num7 + num10);
		}
	}

	private void BuildContainers(MeshBuilder solid, MeshBuilder blend, SnakeWorld w)
	{
		IReadOnlyList<SnakeContainer> containers = w.Containers;
		if (containers != null)
		{
			double num = 10.0 * w.Scale;
			for (int i = 0; i < containers.Count; i++)
			{
				SnakeContainer snakeContainer = containers[i];
				float num2 = (float)snakeContainer.Position.X;
				float num3 = (float)snakeContainer.Position.Y;
				solid.FaceTint = ArkPalette.FaceShader(snakeContainer.IsSearched ? Rgb.FromHex(8421504u) : Rgb.FromHex(16776960u));
				Primitives2D.Rect(solid, (double)num2 - num, (double)num3 - num, (double)num2 + num, (double)num3 + num);
			}
			SnakeContainer snakeContainer2 = ((w.Extraction != null) ? w.Extraction.CurrentNearContainer : null);
			if (snakeContainer2 != null && !snakeContainer2.IsSearched && !w.IsPlayerSearching)
			{
				blend.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(15922938u));
				Primitives2D.Ring(blend, (float)snakeContainer2.Position.X, (float)snakeContainer2.Position.Y, (float)VisualRadiusExtra(16.0, w), (float)VisualRadiusExtra(22.0, w));
			}
		}
	}

	private static double VisualRadiusExtra(double px, SnakeWorld w)
	{
		return px * w.Scale;
	}

	private void BuildFirebombs(MeshBuilder solid, SnakeWorld w)
	{
		IReadOnlyList<SnakeFirebomb> firebombs = w.Firebombs;
		if (firebombs != null)
		{
			for (int i = 0; i < firebombs.Count; i++)
			{
				SnakeFirebomb snakeFirebomb = firebombs[i];
				float num = (float)snakeFirebomb.Position.X;
				float num2 = (float)snakeFirebomb.Position.Y;
				float num3 = (float)(snakeFirebomb.Radius * w.Scale);
				solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(16711680u));
				double num4 = 3.0 * w.Scale;
				Primitives2D.Ring(solid, num, num2, (double)num3 - num4, num3, 40);
			}
		}
	}

	private void BuildFoods(MeshBuilder solid, SnakeWorld w)
	{
		for (int i = 0; i < w.Foods.Count; i++)
		{
			FoodItem foodItem = w.Foods[i];
			float num = (float)foodItem.Position.X;
			float num2 = (float)foodItem.Position.Y;
			float num3 = (float)(foodItem.Radius * w.Scale);
			solid.FaceTint = ArkPalette.FaceShader(foodItem.Color);
			Primitives2D.Disc(solid, num, num2, num3, 10);
		}
	}

	private void BuildPowerUps(MeshBuilder solid, MeshBuilder blend, SnakeWorld w)
	{
		for (int i = 0; i < w.PowerUps.Count; i++)
		{
			PowerUpItem powerUpItem = w.PowerUps[i];
			float num = (float)powerUpItem.Position.X;
			float num2 = (float)powerUpItem.Position.Y;
			Rgb baseColor = PowerUpItem.ColorOf(powerUpItem.Kind);
			double num3 = 1.0 + Math.Sin(powerUpItem.PulsePhase) * 0.15;
			double num4 = 16.0 * w.Scale * num3;
			blend.FaceTint = ArkPalette.FaceShader(baseColor);
			Primitives2D.Disc(blend, num, num2, num4 * 1.9, 24);
			solid.FaceTint = ArkPalette.FaceShader(baseColor);
			Primitives2D.Disc(solid, num, num2, num4, 24);
			solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(16777215u));
			double num5 = 1.5 * w.Scale;
			Primitives2D.Ring(solid, num, num2, num4 - num5, num4);
			BuildPowerUpIcon(solid, num, num2, num4 * 0.55, powerUpItem.Kind, w);
		}
	}

	private void BuildPowerUpIcon(MeshBuilder solid, float cx, float cy, double r, PowerUpKind kind, SnakeWorld w)
	{
		solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(1644825u));
		switch (kind)
		{
		case PowerUpKind.SpeedBoost:
		{
			Vec3[] pts3 = new Vec3[6]
			{
				new Vec3((double)cx - r * 0.15, (double)cy + r, 0.0),
				new Vec3((double)cx + r * 0.45, (double)cy + r * 0.1, 0.0),
				new Vec3((double)cx + r * 0.05, (double)cy + r * 0.1, 0.0),
				new Vec3((double)cx + r * 0.3, (double)cy - r, 0.0),
				new Vec3((double)cx - r * 0.55, (double)cy - r * 0.1, 0.0),
				new Vec3((double)cx - r * 0.1, (double)cy - r * 0.1, 0.0)
			};
			Primitives2D.Polygon(solid, pts3);
			break;
		}
		case PowerUpKind.Shield:
		{
			Vec3[] pts2 = new Vec3[6]
			{
				new Vec3(cx, (double)cy + r, 0.0),
				new Vec3((double)cx + r * 0.85, (double)cy + r * 0.45, 0.0),
				new Vec3((double)cx + r * 0.85, (double)cy - r * 0.3, 0.0),
				new Vec3(cx, (double)cy - r, 0.0),
				new Vec3((double)cx - r * 0.85, (double)cy - r * 0.3, 0.0),
				new Vec3((double)cx - r * 0.85, (double)cy + r * 0.45, 0.0)
			};
			Primitives2D.Polygon(solid, pts2);
			break;
		}
		case PowerUpKind.ScoreGem:
		{
			Vec3[] pts = new Vec3[4]
			{
				new Vec3(cx, (double)cy + r, 0.0),
				new Vec3((double)cx + r * 0.85, cy, 0.0),
				new Vec3(cx, (double)cy - r, 0.0),
				new Vec3((double)cx - r * 0.85, cy, 0.0)
			};
			Primitives2D.Polygon(solid, pts);
			break;
		}
		default:
		{
			double num = r * 0.7;
			double num2 = r * 0.3;
			double num3 = r * 0.7;
			Primitives2D.Rect(solid, (double)cx - num, (double)cy - num3, (double)cx - num + num2, (double)cy + r * 0.3);
			Primitives2D.Rect(solid, (double)cx + num - num2, (double)cy - num3, (double)cx + num, (double)cy + r * 0.3);
			Primitives2D.Rect(solid, (double)cx - num, (double)cy + r * 0.3 - num2, (double)cx + num, (double)cy + r * 0.3);
			break;
		}
		}
	}

	private void BuildPlayer(MeshBuilder solid, MeshBuilder blend, SnakeWorld w)
	{
		if (w.Player.Points.Count != 0)
		{
			Rgb headColor = SnakeSkin.HeadColor;
			Rgb bodyColor = SnakeSkin.BodyColor;
			double num = 8.0 * w.Scale;
			Vec2 head = w.Player.Head;
			if (w.ShieldRemaining > 0.0)
			{
				double num2 = 1.0 + Math.Sin((double)Time.time * 10.0) * 0.12;
				double num3 = num * 2.2 * num2;
				blend.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(5289215u));
				Primitives2D.Disc(blend, head.X, head.Y, num3, 32);
				solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(5289215u));
				double num4 = 3.0 * w.Scale;
				Primitives2D.Ring(solid, head.X, head.Y, num3 - num4, num3, 32);
			}
			if (w.InvincibleTimer > 0.0 || w.DevInvincible)
			{
				double num5 = 1.0 + Math.Sin((double)Time.time * 14.0) * 0.15;
				double num6 = num * 2.6 * num5;
				blend.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(16768100u));
				Primitives2D.Disc(blend, head.X, head.Y, num6, 32);
				solid.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(16768100u));
				double num7 = 2.0 * w.Scale;
				Primitives2D.Ring(solid, head.X, head.Y, num6 - num7, num6, 32);
			}
			DrawSnake2D(solid, w.Player, headColor, bodyColor, num);
		}
	}

	private void BuildEnemies(MeshBuilder solid, SnakeWorld w)
	{
		double radius = 8.0 * w.Scale;
		for (int i = 0; i < w.Enemies.Count; i++)
		{
			SnakeBody snakeBody = w.Enemies[i];
			if (snakeBody.Points.Count != 0)
			{
				Rgb rgb = ((snakeBody.Team == "blue") ? Rgb.FromHex(255u) : Rgb.FromHex(16711680u));
				DrawSnake2D(solid, snakeBody, rgb, rgb, radius);
			}
		}
	}

	private static void DrawSnake2D(MeshBuilder solid, SnakeBody s, Rgb headColor, Rgb bodyColor, double radius)
	{
		List<Vec2> points = s.Points;
		if (points.Count != 0)
		{
			for (int i = 0; i < points.Count; i++)
			{
				solid.FaceTint = ArkPalette.FaceShader((i == 0) ? headColor : bodyColor);
				Primitives2D.Disc(solid, points[i].X, points[i].Y, radius, 12);
			}
		}
	}

	private void BuildParticles(MeshBuilder blend, SnakeWorld w)
	{
		for (int i = 0; i < w.Particles.Count; i++)
		{
			Particle particle = w.Particles[i];
			double num = ((particle.MaxLife > 0.0) ? (particle.Life / particle.MaxLife) : 0.0);
			if (!(num <= 0.02))
			{
				double radius = particle.Radius * (0.4 + 0.6 * num);
				blend.FaceTint = ArkPalette.FaceShader(particle.Color);
				Primitives2D.Disc(blend, particle.Position.X, particle.Position.Y, radius, 6);
			}
		}
	}

	private static void DrawDashedLine(MeshBuilder mb, Vec2 a, Vec2 b, Rgb color, double halfWidth, double dashRatio)
	{
		double num = b.X - a.X;
		double num2 = b.Y - a.Y;
		double num3 = Math.Sqrt(num * num + num2 * num2);
		if (num3 < 1E-06)
		{
			return;
		}
		int num4 = Math.Max(3, (int)(num3 / 30.0));
		double num5 = num3 / (double)num4;
		mb.FaceTint = ArkPalette.FaceShader(color);
		for (int i = 0; i < num4; i++)
		{
			if (i % 2 != 1)
			{
				double num6 = (double)i * num5 / num3;
				double num7 = ((double)i * num5 + num5 * dashRatio) / num3;
				Primitives2D.Segment(mb, a.X + num * num6, a.Y + num2 * num6, a.X + num * num7, a.Y + num2 * num7, halfWidth);
			}
		}
	}

	private static void SetMesh(MeshFilter mf, MeshBuilder mb, string name, float z)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected Obj, but got Unknown
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		if ((UnityEngine.Object)(object)mf == (UnityEngine.Object)null)
		{
			return;
		}
		Mesh val = mf.sharedMesh;
		if ((UnityEngine.Object)(object)val == (UnityEngine.Object)null)
		{
			val = new Mesh
			{
				name = name
			};
			val.MarkDynamic();
			mf.sharedMesh = val;
		}
		val.Clear();
		if (!mb.IsEmpty)
		{
			int vertexCount = mb.VertexCount;
			Vector3[] array = new Vector3[vertexCount];
			Vector3[] array2 = new Vector3[vertexCount];
			Vector2[] array3 = new Vector2[vertexCount];
			Color32[] array4 = new Color32[vertexCount];
			for (int i = 0; i < vertexCount; i++)
			{
				Vec3 vec = mb.Positions[i];
				Vec3 vec2 = mb.Normals[i];
				(double, double) tuple = mb.Uvs[i];
				array[i] = new Vector3((float)vec.X, (float)vec.Y, z);
				array2[i] = new Vector3((float)vec2.X, (float)vec2.Y, (float)vec2.Z);
				array3[i] = new Vector2((float)tuple.Item1, (float)tuple.Item2);
				Rgb rgb = mb.TriangleColor(i / 3);
				array4[i] = new Color32(rgb.R, rgb.G, rgb.B, byte.MaxValue);
			}
			int[] array5 = new int[mb.Triangles.Count];
			for (int j = 0; j < array5.Length; j++)
			{
				array5[j] = mb.Triangles[j];
			}
		// ★ 顶点数超过 65535 时必须用 32 位索引：
		//   Unity 网格默认 indexFormat=UInt16，食物一多顶点超限，
		//   超出部分直接不渲染（实测"食物达到一定数量部分不显示"）。
		if (vertexCount > 65535)
			val.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
		else
			val.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
			val.vertices = array;
			val.normals = array2;
			val.uv = array3;
			val.colors32 = array4;
			val.triangles = array5;
			val.RecalculateBounds();
		}
	}
}
}
