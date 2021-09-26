using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using System;
using System.Linq;

namespace CustomCommunityCentre
{
	public class ModEntry : Mod
	{
		internal static ModEntry Instance;
		internal static Config Config;
		internal ITranslationHelper i18n => Helper.Translation;

		internal const int DummyId = 6830 * 10000;
		private const string CommandPrefix = "bb.ccc.";

		internal static bool IsNewGame => Game1.dayOfMonth == 1 && Game1.currentSeason == "spring" && Game1.year == 1;

		internal bool _isSaveLoaded = false;
		internal readonly PerScreen<int> _lastJunimoNoteMenuArea = new PerScreen<int>(createNewState: () => 0);


		public override void Entry(IModHelper helper)
		{
			ModEntry.Instance = this;
			ModEntry.Config = helper.ReadConfig<Config>();

			this.RegisterEvents();
			this.AddConsoleCommands();

			string id = this.ModManifest.UniqueID;
			HarmonyPatches.ApplyHarmonyPatches(id: id);

			AssetManager assetManager = new AssetManager();
			helper.Content.AssetLoaders.Add(assetManager);
			helper.Content.AssetEditors.Add(assetManager);
		}

		private void AddConsoleCommands()
		{
			if (ModEntry.Config.DebugMode)
			{
				this.Helper.ConsoleCommands.Add(ModEntry.CommandPrefix + "debug1", "...", (s, args) =>
				{
					int asdf = ((StardewValley.Locations.CommunityCenter)
						Game1.getLocationFromName(nameof(StardewValley.Locations.CommunityCenter)))
						.bundleMutexes.Count;
					Log.D($"BundleMutexes: {asdf}");
				});
			}

			Bundles.AddConsoleCommands(cmd: ModEntry.CommandPrefix);
		}

		private void RegisterEvents()
		{
			this.Helper.Events.GameLoop.ReturnedToTitle += this.GameLoop_ReturnedToTitle;
			this.Helper.Events.GameLoop.SaveLoaded += this.GameLoop_SaveLoaded;
			this.Helper.Events.GameLoop.DayStarted += this.GameLoop_DayStarted;

			Bundles.RegisterEvents();
			Kitchen.RegisterEvents();
		}

		private void GameLoop_ReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
		{
			this._isSaveLoaded = false;
		}

		private void GameLoop_DayStarted(object sender, DayStartedEventArgs e)
		{
			// Perform OnSaveLoaded behaviours when starting a new game
			if (IsNewGame && !this._isSaveLoaded)
			{
				this.SaveLoadedBehaviours();
			}
		}

		private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
		{
			this.SaveLoadedBehaviours();
		}

		private void SaveLoadedBehaviours()
		{
			this._isSaveLoaded = true;

			AssetManager.ReloadAssets(helper: this.Helper);

			Bundles.SaveLoadedBehaviours(Bundles.CC);
			Kitchen.SaveLoadedBehaviours(Bundles.CC);
		}
	}
}
