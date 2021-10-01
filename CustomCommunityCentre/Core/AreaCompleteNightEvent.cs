using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewValley;
using StardewValley.Events;
using System;

namespace CustomCommunityCentre
{
	public class AreaCompleteNightEvent : FarmEvent
	{
		public readonly NetInt whichEvent = new NetInt();
		public int cutsceneLengthTimer;
		public int timerSinceFade;
		public int soundTimer;
		public int soundInterval = 99999;
		public GameLocation location;
		public GameLocation preEventLocation;
		public string sound;
		public bool kill;
		public bool wasRaining;

		public NetFields NetFields { get; } = new NetFields();


		public AreaCompleteNightEvent()
		{
			NetFields.AddField(whichEvent);
		}

		public AreaCompleteNightEvent(int which)
			: this()
		{
			whichEvent.Value = which;
		}

		public void draw(SpriteBatch b)
		{
		}

		public void drawAboveEverything(SpriteBatch b)
		{
		}

		public void makeChangesToLocation()
		{
		}

		public bool setUp()
		{
			CustomCommunityCentre.Data.BundleMetadata bundleMetadata = Bundles.GetCustomBundleMetadataFromAreaNumber(whichEvent.Value);

			if (!Bundles.IsCustomArea(whichEvent.Value) || bundleMetadata == null
				|| string.IsNullOrEmpty(bundleMetadata.JunimoCutsceneLocation)
				|| bundleMetadata.JunimoCutsceneTileLocation == Point.Zero)
				return true;

			// Add dialogue event
			string eventId = string.Format(Bundles.ActiveDialogueEventAreaCompleted, bundleMetadata.AreaName);
			Game1.player.activeDialogueEvents[eventId] = Bundles.ActiveDialogueEventAreaCompletedDuration;

			// Set up generic junimo area complete event

			// Set game
			Game1.currentLightSources.Clear();
			location = null;
			cutsceneLengthTimer = 8000;
			wasRaining = Game1.isRaining;
			Game1.isRaining = false;
			Game1.changeMusicTrack("nightTime");
			preEventLocation = Game1.currentLocation;

			// Set location
			location = Game1.getLocationFromName(bundleMetadata.JunimoCutsceneLocation);
			location.resetForPlayerEntry();
			Point targetTile = bundleMetadata.JunimoCutsceneTileLocation;
			Vector2 targetPixel = Utility.PointToVector2(targetTile) * Game1.tileSize;

			// Add sprites
			for (int i = 0; i < 2; ++i)
			{
				Vector2 offset = Game1.tileSize * new Vector2(i == 0 ? 4f : -4f, 1.5f);
				location.temporarySprites.Add(
					new TemporaryAnimatedSprite(
						textureName: @"LooseSprites/Cursors",
						sourceRect: new Rectangle(294, 1432, 16, 16),
						animationInterval: 300f,
						animationLength: 4,
						numberOfLoops: 999,
						position: targetPixel + offset,
						flicker: false,
						flipped: false)
					{
						scale = Game1.pixelZoom,
						layerDepth = 1f,
						xPeriodic = true,
						xPeriodicLoopTime = 2000f + (i * 300f),
						xPeriodicRange = 16f,
						light = true,
						lightcolor = Color.DarkGoldenrod,
						lightRadius = 1f
					});
			}

			// Set effects
			LightSource lightSource = new LightSource(
				textureIndex: 4,
				position: Utility.PointToVector2(targetTile) * Game1.tileSize,
				radius: 4f,
				color: Color.DarkGoldenrod);
			Game1.currentLightSources.Add(lightSource);
			Utility.addSprinklesToLocation(location, targetTile.X, targetTile.Y, 7, 4, 15000, 150, Color.LightCyan);
			Utility.addStarsAndSpirals(location, targetTile.X + 1, targetTile.Y, 7, 4, 15000, 350, Color.White);
			soundTimer = soundInterval = 800;
			sound = "junimoMeep1";

			// Set viewport
			Game1.currentLocation = location;
			Game1.fadeClear();
			Game1.nonWarpFade = true;
			Game1.timeOfDay = 2400;
			Game1.displayHUD = false;
			Game1.viewportFreeze = true;
			Game1.player.position.X = -999999f;
			Game1.viewport.X = Math.Max(0, Math.Min(location.map.DisplayWidth - Game1.viewport.Width, (targetTile.X * Game1.tileSize) - (Game1.viewport.Width / 2)));
			Game1.viewport.Y = Math.Max(0, Math.Min(location.map.DisplayHeight - Game1.viewport.Height, (targetTile.Y * Game1.tileSize) - (Game1.viewport.Height / 2)));
			if (!location.IsOutdoors)
			{
				Game1.viewport.X = (int)targetPixel.X - (Game1.viewport.Width / 2);
				Game1.viewport.Y = (int)targetPixel.Y - (Game1.viewport.Height / 2);
			}
			Game1.previousViewportPosition = new Vector2(Game1.viewport.X, Game1.viewport.Y);
			if (Game1.debrisWeather != null && Game1.debrisWeather.Count > 0)
			{
				Game1.randomizeDebrisWeatherPositions(Game1.debrisWeather);
			}
			Game1.randomizeRainPositions();
			return false;
		}

		public bool tickUpdate(GameTime time)
		{
			Game1.UpdateGameClock(time);
			location.updateWater(time);
			cutsceneLengthTimer -= time.ElapsedGameTime.Milliseconds;
			if (timerSinceFade > 0)
			{
				timerSinceFade -= time.ElapsedGameTime.Milliseconds;
				Game1.globalFade = true;
				Game1.fadeToBlackAlpha = 1f;
				return timerSinceFade <= 0;
			}
			if (cutsceneLengthTimer <= 0 && !Game1.globalFade)
			{
				Game1.globalFadeToBlack(endEvent, 0.01f);
			}
			soundTimer -= time.ElapsedGameTime.Milliseconds;
			if (soundTimer <= 0 && sound != null)
			{
				Game1.playSound(sound);
				soundTimer = soundInterval;
			}
			return false;
		}

		public void endEvent()
		{
			if (preEventLocation != null)
			{
				Game1.currentLocation = preEventLocation;
				Game1.currentLocation.resetForPlayerEntry();
				preEventLocation = null;
			}
			Game1.changeMusicTrack("none");
			timerSinceFade = 1500;
			Game1.isRaining = wasRaining;
			Game1.getFarm().temporarySprites.Clear();
		}
	}
}
