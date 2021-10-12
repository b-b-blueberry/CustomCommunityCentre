using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace CommunityKitchen
{
    public class ModEntry : Mod
	{
		internal static ModEntry Instance;
		internal static ITranslationHelper i18n => Instance.Helper.Translation;

		private const string CommandPrefix = "bb.cck.";


		public override void Entry(IModHelper helper)
		{
			ModEntry.Instance = this;

			this.RegisterEvents();
			Kitchen.AddConsoleCommands(cmd: ModEntry.CommandPrefix);
			GusDeliveryService.AddConsoleCommands(cmd: ModEntry.CommandPrefix);

			AssetManager assetManager = new();
			helper.Content.AssetLoaders.Add(assetManager);
			helper.Content.AssetEditors.Add(assetManager);
		}

		private void RegisterEvents()
		{
			this.Helper.Events.GameLoop.SaveLoaded += this.GameLoop_SaveLoaded;
			this.Helper.Events.GameLoop.DayStarted += this.GameLoop_DayStarted;

			Kitchen.RegisterEvents();
			GusDeliveryService.RegisterEvents();
		}

		private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
		{
			this.SaveLoadedBehaviours();
		}

		private void GameLoop_DayStarted(object sender, DayStartedEventArgs e)
		{
			if (CustomCommunityCentre.ModEntry.IsNewGame && !CustomCommunityCentre.ModEntry.Instance.IsSaveLoaded)
			{
				// Perform OnSaveLoaded behaviours when starting a new game
				this.SaveLoadedBehaviours();
			}

			this.DayStartedBehaviours();
		}

		private void SaveLoadedBehaviours()
		{
			Kitchen.SaveLoadedBehaviours();
			GusDeliveryService.SaveLoadedBehaviours();
		}

		private void DayStartedBehaviours()
		{
			Kitchen.DayStartedBehaviours();
			GusDeliveryService.DayStartedBehaviours();
		}
	}
}
