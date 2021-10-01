using CustomCommunityCentre;
using StardewModdingAPI;

namespace CommunityCentreKitchen
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

			AssetManager assetManager = new AssetManager();
			helper.Content.AssetLoaders.Add(assetManager);
			helper.Content.AssetEditors.Add(assetManager);
		}

		private void RegisterEvents()
		{
			Kitchen.RegisterEvents();
			GusDeliveryService.RegisterEvents();
		}
	}
}
