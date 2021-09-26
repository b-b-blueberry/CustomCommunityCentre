using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using xTile.Tiles;

namespace CustomCommunityCentre
{
	public static class Kitchen
	{
		private static IModHelper Helper => ModEntry.Instance.Helper;
		private static IReflectionHelper Reflection => ModEntry.Instance.Helper.Reflection;
		private static ITranslationHelper i18n => ModEntry.Instance.Helper.Translation;
		private static Config Config => ModEntry.Config;

		// Kitchen definitions
		public const string KitchenAreaName = "Kitchen";

		// Kitchen fridge
		// We use Linus' tent interior for the dummy area, since there's surely no conceivable way it'd be in the community centre
		public static readonly Rectangle FridgeOpenedSpriteArea = new Rectangle(32, 560, 16, 32);
		public static readonly Vector2 FridgeChestPosition = new Vector2(6830);
		public static string FridgeTilesToUse = "Vanilla";
		public static readonly Dictionary<string, int[]> FridgeTileIndexes = new Dictionary<string, int[]>
		{
			{ "Vanilla", new [] { 602, 634, 1122, 1154 } },
			{ "SVE", new [] { 432, 440, 432, 442 } }
		};
		public static readonly Dictionary<string, int[]> CookingTileIndexes = new Dictionary<string, int[]>
		{
			{ "Vanilla", new [] { 498, 499, 631, 632, 633 } },
			{ "SVE", new [] { 498, 499, 631, 632, 633 } },
		};
		public static Vector2 FridgeTilePosition = Vector2.Zero;


		internal static void RegisterEvents()
		{
			Helper.Events.GameLoop.DayStarted += Kitchen.GameLoop_DayStarted;
			Helper.Events.Input.ButtonPressed += Kitchen.Input_ButtonPressed;
			Helper.Events.Display.MenuChanged += Kitchen.Display_MenuChanged;

			CustomCommunityCentre.Events.LoadedArea += Kitchen.OnLoadedArea;
		}

		internal static void SaveLoadedBehaviours(CommunityCenter cc)
		{
			// . . .
		}

		private static void GameLoop_DayStarted(object sender, DayStartedEventArgs e)
		{
			if (Bundles.IsCommunityCentreComplete(Bundles.CC))
			{
				Kitchen.SetUpKitchen();
			}
		}

		private static void Input_ButtonPressed(object sender, ButtonPressedEventArgs e)
		{
			// In-game interactions
			if (!Game1.game1.IsActive || Game1.currentLocation == null || !Context.IsWorldReady)
				return;

			// . . .

			// World interactions
			if (!Context.CanPlayerMove)
				return;

			if (e.Button.IsActionButton())
			{
				// Tile actions
				Tile tile = Game1.currentLocation.Map.GetLayer("Buildings")
					.Tiles[(int)e.Cursor.GrabTile.X, (int)e.Cursor.GrabTile.Y];

				// Open Community Centre fridge door
				if (Game1.currentLocation is CommunityCenter cc && Kitchen.IsKitchenComplete(cc)
					&& tile != null && tile.TileIndex == Kitchen.FridgeTileIndexes[Kitchen.FridgeTilesToUse][1])
				{
					// Change tile to use custom open-fridge sprite
					Point tileLocation = Utility.Vector2ToPoint(Kitchen.FridgeTilePosition);
					Game1.currentLocation.Map.GetLayer("Front")
						.Tiles[tileLocation.X, tileLocation.Y - 1]
						.TileIndex = Kitchen.FridgeTileIndexes[Kitchen.FridgeTilesToUse][2];
					Game1.currentLocation.Map.GetLayer("Buildings")
						.Tiles[tileLocation.X, tileLocation.Y]
						.TileIndex = Kitchen.FridgeTileIndexes[Kitchen.FridgeTilesToUse][3];

					// Open the fridge as a chest
					((Chest)cc.Objects[Kitchen.FridgeChestPosition]).fridge.Value = true;
					((Chest)cc.Objects[Kitchen.FridgeChestPosition]).checkForAction(Game1.player);

					Helper.Input.Suppress(e.Button);
				}
			}
		}

		private static void Display_MenuChanged(object sender, MenuChangedEventArgs e)
		{
			if (e.OldMenu is TitleMenu || e.NewMenu is TitleMenu || Game1.currentLocation == null || Game1.player == null)
				return;

			// Close Community Centre fridge door after use in the renovated kitchen
			Point tile = Utility.Vector2ToPoint(Kitchen.FridgeTilePosition);
			if (e.OldMenu is ItemGrabMenu && e.NewMenu == null
				&& Game1.currentLocation is CommunityCenter cc
				&& (Bundles.IsCommunityCentreComplete(cc) || Kitchen.IsKitchenComplete(cc))
				&& Kitchen.FridgeTilePosition != Vector2.Zero
				&& cc.Map.GetLayer("Front").Tiles[tile.X, tile.Y - 1] is Tile tileA
				&& cc.Map.GetLayer("Buildings").Tiles[tile.X, tile.Y] is Tile tileB
				&& tileA != null && tileB != null)
			{
				cc.Map.GetLayer("Front")
					.Tiles[tile.X, tile.Y - 1]
					.TileIndex = Kitchen.FridgeTileIndexes[Kitchen.FridgeTilesToUse][0];
				cc.Map.GetLayer("Buildings")
					.Tiles[tile.X, tile.Y]
					.TileIndex = Kitchen.FridgeTileIndexes[Kitchen.FridgeTilesToUse][1];
				return;
			}
		}

		private static void OnLoadedArea(object sender, EventArgs e)
		{
			if (((CustomCommunityCentre.LoadedAreaEventArgs)e).AreaName != Kitchen.KitchenAreaName)
				return;

			CommunityCenter cc = ((CustomCommunityCentre.LoadedAreaEventArgs)e).CommunityCenter;

			// Fetch tile position for opening/closing fridge visually
			Kitchen.FridgeTilesToUse = Helper.ModRegistry.IsLoaded("FlashShifter.StardewValleyExpandedCP") ? "SVE" : "Vanilla";
			Kitchen.FridgeTilePosition = Kitchen.GetKitchenFridgeTilePosition(cc);

			// Add community centre kitchen fridge container to the map for later
			if (!cc.Objects.ContainsKey(Kitchen.FridgeChestPosition))
			{
				cc.Objects.Add(Kitchen.FridgeChestPosition, new Chest(playerChest: true, tileLocation: Kitchen.FridgeChestPosition));
			}
		}

		public static bool IsKitchenLoaded(CommunityCenter cc)
		{
			if (cc == null)
				return false;

			bool bundlesExist = Game1.netWorldState.Value.Bundles.Keys.Any(key => key > Bundles.CustomBundleInitialIndex);
			bool areasCompleteEntriesExist = cc.areasComplete.Count >= Bundles.DefaultMaxArea;
			bool clientEnabled = !Game1.IsMasterGame && (bundlesExist || areasCompleteEntriesExist);
			return clientEnabled;
		}

		public static bool IsKitchenComplete(CommunityCenter cc)
		{
			if (cc == null)
				return false;

			int kitchenNumber = Bundles.GetAreaNumberFromName(Kitchen.KitchenAreaName);

			bool receivedMail = HasOrWillReceiveKitchenCompletedMail();
			bool noCustomAreas = kitchenNumber < 0 || cc.areasComplete.Count <= kitchenNumber;
			bool customAreasComplete = noCustomAreas || cc.areasComplete[kitchenNumber];
			bool ccIsComplete = Bundles.IsCommunityCentreComplete(cc);
			Log.T($"IsCommunityCentreKitchenComplete: (mail: {receivedMail}) || (entries: {noCustomAreas}) || (areas: {customAreasComplete}) || (cc: {ccIsComplete})");
			return receivedMail || noCustomAreas || customAreasComplete || ccIsComplete;
		}

		internal static void SetUpKitchen()
		{
			Helper.Content.InvalidateCache(@"LooseSprites/JunimoNote");
			Helper.Content.InvalidateCache(@"Maps/townInterior");
			Helper.Content.InvalidateCache(@"Strings/Locations");
			Helper.Content.InvalidateCache(@"Strings/UI");
		}

		public static Chest GetKitchenFridge(CommunityCenter cc)
		{
			// Add fridge chest object if missing
			if (!cc.Objects.ContainsKey(Kitchen.FridgeChestPosition))
			{
				cc.Objects[Kitchen.FridgeChestPosition] = new Chest(
					playerChest: true,
					tileLocation: Kitchen.FridgeChestPosition);
			}
			// Get fridge chest object if available
			Chest fridge = (Kitchen.IsKitchenComplete(cc)
				&& cc.Objects.TryGetValue(Kitchen.FridgeChestPosition, out StardewValley.Object o)
				&& o is Chest chest && chest != null)
				? chest
				: null;

			return fridge;
		}

		private static Vector2 GetKitchenFridgeTilePosition(CommunityCenter cc)
		{
			int w = cc.Map.GetLayer("Buildings").LayerWidth;
			int h = cc.Map.GetLayer("Buildings").LayerHeight;
			for (int x = 0; x < w; ++x)
			{
				for (int y = 0; y < h; ++y)
				{
					if (cc.Map.GetLayer("Buildings").Tiles[x, y] != null
						&& cc.Map.GetLayer("Buildings").Tiles[x, y].TileIndex == Kitchen.FridgeTileIndexes[Kitchen.FridgeTilesToUse][1])
					{
						return new Vector2(x, y);
					}
				}
			}
			return Vector2.Zero;
		}

		public static bool HasOrWillReceiveKitchenCompletedMail()
		{
			return Game1.MasterPlayer.hasOrWillReceiveMail(string.Format(Bundles.MailAreaCompleted, Kitchen.KitchenAreaName));
		}
	}
}
