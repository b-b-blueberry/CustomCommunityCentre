using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Locations;
using System.Collections.Generic;

namespace CustomCommunityCentre
{
	public class ModEntry : Mod
	{
		public static ModEntry Instance;
		internal static Config Config;
		public static AssetManager AssetManager;

		// Constant values
		public const int DummyId = 6830 * 10000;
		private const string CommandPrefix = "bb.ccc.";

		// Player states
		public readonly PerScreen<PlayerState> State = new PerScreen<PlayerState>(createNewState: () => new PlayerState());
		public class PlayerState
		{
			public int LastJunimoNoteMenuArea = 0;
		}

		// Game states
		public bool IsSaveLoaded = false;
		public static bool IsNewGame => Game1.dayOfMonth == 1 && Game1.currentSeason == "spring" && Game1.year == 1;


		public override void Entry(IModHelper helper)
		{
			ModEntry.Instance = this;
			ModEntry.Config = helper.ReadConfig<Config>();
			ModEntry.AssetManager = new AssetManager();

			this.RegisterEvents();
			this.AddConsoleCommands();

			string id = this.ModManifest.UniqueID;
			HarmonyPatches.ApplyHarmonyPatches(id: id);

			helper.Content.AssetLoaders.Add(ModEntry.AssetManager);
			helper.Content.AssetEditors.Add(ModEntry.AssetManager);
		}

		public override object GetApi()
		{
			return new CustomCommunityCentre.API.CustomCommunityCentreAPI(reflection: Helper.Reflection);
		}

		private void AddConsoleCommands()
		{
			if (ModEntry.Config.DebugMode)
			{
				this.Helper.ConsoleCommands.Add(ModEntry.CommandPrefix + "debug1", "...", (s, args) =>
				{
				});
			}

			//BundleManager.AddConsoleCommands(cmd: ModEntry.CommandPrefix);
			Bundles.AddConsoleCommands(cmd: ModEntry.CommandPrefix);
		}

		private void RegisterEvents()
		{
			this.Helper.Events.GameLoop.ReturnedToTitle += this.GameLoop_ReturnedToTitle;
			this.Helper.Events.GameLoop.SaveLoaded += this.GameLoop_SaveLoaded;
			this.Helper.Events.GameLoop.DayStarted += this.GameLoop_DayStarted;

			BundleManager.RegisterEvents();
			Bundles.RegisterEvents();
		}

		private void GameLoop_ReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
		{
			this.IsSaveLoaded = false;
		}

		private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
		{
			this.SaveLoadedBehaviours();
		}

		private void GameLoop_DayStarted(object sender, DayStartedEventArgs e)
		{
			if (ModEntry.IsNewGame && !this.IsSaveLoaded)
			{
				// Perform OnSaveLoaded behaviours when starting a new game
				this.SaveLoadedBehaviours();
			}

			this.DayStartedBehaviours(Bundles.CC);
		}

		private void SaveLoadedBehaviours()
		{
			this.IsSaveLoaded = true;

			AssetManager.ReloadAssets(helper: this.Helper);

			BundleManager.SaveLoadedBehaviours(Bundles.CC);
			Bundles.SaveLoadedBehaviours(Bundles.CC);
		}

		private void DayStartedBehaviours(CommunityCenter cc)
		{
			BundleManager.DayStartedBehaviours(cc);
			Bundles.DayStartedBehaviours(cc);
		}

		public Multiplayer GetMultiplayer()
		{
			Multiplayer multiplayer = Helper.Reflection.GetField
				<Multiplayer>
				(type: typeof(Game1), name: "multiplayer")
				.GetValue();
			return multiplayer;
		}

		public static Vector2 FindFirstPlaceableTileAroundPosition(GameLocation location, StardewValley.Object o, Vector2 tilePosition, int maxIterations)
		{
			// Recursive search logic taken from StardewValley.Utility.RecursiveFindOpenTiles()
			int iterations = 0;
			Queue<Vector2> positionsToCheck = new Queue<Vector2>();
			positionsToCheck.Enqueue(tilePosition);
			List<Vector2> closedList = new List<Vector2>();
			for (; iterations < maxIterations; ++iterations)
			{
				if (positionsToCheck.Count <= 0)
				{
					break;
				}
				Vector2 currentPoint = positionsToCheck.Dequeue();
				closedList.Add(currentPoint);
				if (o.canBePlacedHere(location, currentPoint))
				{
					return currentPoint;
				}
				Vector2[] directionsTileVectors = Utility.DirectionsTileVectors;
				foreach (Vector2 v in directionsTileVectors)
				{
					if (!closedList.Contains(currentPoint + v))
					{
						positionsToCheck.Enqueue(currentPoint + v);
					}
				}
			}
			return Vector2.Zero;
		}
	}
}
