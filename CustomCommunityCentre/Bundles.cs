using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.GameData;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using xTile;
using xTile.Tiles;

// TODO: FIX: Pantry area complete also refurbishes Kitchen area

namespace CustomCommunityCentre
{
	public static class Bundles
	{
		private static IModHelper Helper => ModEntry.Instance.Helper;
		private static IMonitor Monitor => ModEntry.Instance.Monitor;
		private static IReflectionHelper Reflection => ModEntry.Instance.Helper.Reflection;
		private static ITranslationHelper i18n => ModEntry.Instance.Helper.Translation;
		private static Config Config => ModEntry.Config;
		private static CommunityCenter _cc;
		internal static CommunityCenter CC => Context.IsWorldReady
			? _cc ??= Game1.getLocationFromName("CommunityCenter") as CommunityCenter
			: _cc;

		// Kitchen areas and bundles
		public static int DefaultMaxArea;
		public static int DefaultMaxBundle;
		public static int CustomAreaInitialIndex;
		public static int CustomBundleInitialIndex;
		public static int TotalAreaCount => Bundles.CC.areasComplete.Count();
		public static int TotalBundleCount => Bundles.CC.bundles.Count();
		public static Dictionary<string, int> CustomAreaNameAndNumberDictionary;
		public static Dictionary<string, string[]> CustomAreaBundleDictionary;
		public static List<BundleMetadata> BundleMetadata = new List<BundleMetadata>();
		public static readonly Point BundleDonationsChestTile = new Point(32, 9); // immediately ahead of cc fireplace

		// Mail
		// TODO: Ensure all Mail values are referenced with string.Format(pattern, areaName) where appropriate
		internal const string MailPrefix = "blueberry.ccc.mail.";
		internal const string MailAreaCompleted = "cc{0}";
		internal const string MailAreaCompletedFollowup = MailPrefix + "{0}_completed_followup";
		internal const string MailAreaLastBundleCompleteRewardDelivery = MailPrefix + "{0}_reward_guarantee";

		// Events
		internal enum EventIds
		{
			CommunityCentreUnlocked = 611439,
			CommunityCentreComplete = 191393,
			JojaWarehouseComplete = 502261,
			AbandonedJojaMartComplete = 192393
		}

		// ModData keys
		internal static string KeyAreasComplete => AssetManager.PrefixAsset(asset: "AreasComplete");
		internal static string KeyBundleRewards => AssetManager.PrefixAsset(asset: "BundleRewards");
		internal static string KeyMutexes => AssetManager.PrefixAsset(asset: "Mutexes");


		internal static void RegisterEvents()
		{
			Helper.Events.Multiplayer.PeerConnected += Bundles.Multiplayer_PeerConnected;
			Helper.Events.GameLoop.ReturnedToTitle += Bundles.GameLoop_ReturnedToTitle;
			Helper.Events.GameLoop.Saving += Bundles.GameLoop_Saving;
			Helper.Events.GameLoop.DayEnding += Bundles.GameLoop_DayEnding;
			Helper.Events.GameLoop.DayStarted += Bundles.GameLoop_DayStarted;
			Helper.Events.Display.MenuChanged += Bundles.Display_MenuChanged;
			Helper.Events.Player.Warped += Bundles.Player_Warped;
			Helper.Events.Input.ButtonPressed += Bundles.Input_ButtonPressed;
		}

		public static void AddConsoleCommands(string cmd)
		{
			Helper.ConsoleCommands.Add(cmd + "print", "Print Community Centre bundle states.", (s, args) =>
			{
				Bundles.PrintBundleData(Bundles.CC);
			});
			Helper.ConsoleCommands.Add(cmd + "list", "List all bundle IDs currently loaded.", (s, args) =>
			{
				Dictionary<int, int> bundleAreaDict = Helper.Reflection.GetField
					<Dictionary<int, int>>
					(Bundles.CC, "bundleToAreaDictionary").GetValue();
				string msg = string.Join(Environment.NewLine,
					Game1.netWorldState.Value.BundleData.Select(
						pair => $"[Area {bundleAreaDict[int.Parse(pair.Key.Split('/')[1])]}] {pair.Key}: {pair.Value.Split('/')[0]}"));
				Log.D(msg);
			});
			Helper.ConsoleCommands.Add(cmd + "bundle", "Give items needed for the given bundle.", (s, args) =>
			{
				if (args.Length == 0 || !int.TryParse(args[0], out int bundle) || !Game1.netWorldState.Value.Bundles.ContainsKey(bundle))
				{
					Log.D("No bundle found.");
					return;
				}

				Bundles.GiveBundleItems(cc: Bundles.CC, bundle, print: true);
			});
			Helper.ConsoleCommands.Add(cmd + "area", "Give items needed for all bundles in the given area.", (s, args) =>
			{
				if (args.Length == 0 || !int.TryParse(args[0], out int area))
				{
					Log.D("No area found.");
					return;
				}

				Bundles.GiveAreaItems(cc: Bundles.CC, whichArea: area, print: true);
			});
			Helper.ConsoleCommands.Add(cmd + "reset.b", "Reset a bundle's saved progress to entirely incomplete.", (s, args) =>
			{
				if (args.Length == 0 || !int.TryParse(args[0], out int bundle)
					|| !Game1.netWorldState.Value.Bundles.ContainsKey(bundle))
				{
					Log.D("No bundle found.");
					return;
				}
				if (Bundles.IsCommunityCentreComplete(Bundles.CC))
				{
					Log.D("Too late.");
					return;
				}

				Bundles.ResetBundleProgress(cc: Bundles.CC, bundle, print: true);
			});
			Helper.ConsoleCommands.Add(cmd + "reset.a", "Reset all bundle progress for an area to entirely incomplete.", (s, args) =>
			{
				var abd = Helper.Reflection.GetField
					<Dictionary<int, List<int>>>
					(Bundles.CC, "areaToBundleDictionary").GetValue();
				if (args.Length == 0 || !int.TryParse(args[0], out int area) || !abd.ContainsKey(area))
				{
					Log.D("No area found.");
					return;
				}
				if (Bundles.IsCommunityCentreComplete(Bundles.CC))
				{
					Log.D("Too late.");
					return;
				}

				foreach (int bundle in abd[area])
				{
					Bundles.ResetBundleProgress(cc: Bundles.CC, bundle, print: true);
				}
			});

			Helper.ConsoleCommands.Add(cmd + "setup", $"Prepare the CC for custom bundles.", (s, args) =>
			{
				new List<string> { "ccDoorUnlock", "seenJunimoNote", "wizardJunimoNote", "canReadJunimoText" }
					.ForEach(id => Game1.player.mailReceived.Add(id));
				new List<int> { 611439 }
					.ForEach(id => Game1.player.eventsSeen.Add(id));
				Game1.player.increaseBackpackSize(24);
				Bundles.GiveAreaItems(cc: Bundles.CC, whichArea: Bundles.CustomAreaInitialIndex, print: true);
			});

			Helper.ConsoleCommands.Add(cmd + "goto", $"Warp to a junimo note for an area in the CC.", (s, args) =>
			{
				int areaNumber = args.Length > 0
					? int.TryParse(args[0], out int i)
						? i
						: string.Join(" ", args) is string name && !string.IsNullOrWhiteSpace(name)
							&& CommunityCenter.getAreaNumberFromName(name) is int i1
							? i1
						: -1
					: -1;

				if (areaNumber < 0 || areaNumber >= Bundles.CC.areasComplete.Count)
				{
					Log.D($"No valid area name or number found for '{string.Join(" ", args)}'.");
					return;
				}

				Point tileLocation = Helper.Reflection
					.GetMethod(Bundles.CC, "getNotePosition")
					.Invoke<Point>(areaNumber);

				Log.D($"Warping to area {areaNumber} - {CommunityCenter.getAreaNameFromNumber(areaNumber)} ({tileLocation.ToString()})");

				tileLocation = Utility.Vector2ToPoint(
					Utility.recursiveFindOpenTileForCharacter(
						c: Game1.player,
						l: Bundles.CC,
						tileLocation: Utility.PointToVector2(tileLocation),
						maxIterations: 8));

				Game1.warpFarmer(
					locationName: Bundles.CC.Name,
					tileX: tileLocation.X,
					tileY: tileLocation.Y,
					facingDirectionAfterWarp: 2);
			});
		}

		internal static void SaveLoadedBehaviours(CommunityCenter cc)
		{
			Log.D($"Loaded save: {Game1.player.Name} ({Game1.player.farmName}).",
				Config.DebugMode);

			// Prime custom area-bundle metadata
			Helper.Content.InvalidateCache(AssetManager.GameBundleDefinitionsPath);
		}

		internal static void DayStartedBehaviours(CommunityCenter cc)
		{
			// Load in new community centre area-bundle data if ready
			if (Bundles.IsCommunityCentreComplete(cc))
			{
				Log.D("Community centre complete, unloading any bundle data.",
					ModEntry.Config.DebugMode);
				Bundles.SaveAndUnloadBundleData(cc);
			}
			else
			{
				Log.D("Community centre incomplete, loading bundle data.",
					ModEntry.Config.DebugMode);
				Bundles.LoadBundleData(cc);
			}

			// Send followup mail when an area is completed
			foreach (KeyValuePair<string, int> areaNameAndNumber in Bundles.CustomAreaNameAndNumberDictionary)
			{
				string mailId = string.Format(Bundles.MailAreaCompletedFollowup, areaNameAndNumber.Key);
				if (Bundles.IsAreaCompleteAndLoaded(cc, areaNumber: areaNameAndNumber.Value)
					&& !Game1.MasterPlayer.hasOrWillReceiveMail(mailId))
				{
					Game1.addMailForTomorrow(mailId);
				}
			}
		}

		private static void Multiplayer_PeerConnected(object sender, PeerConnectedEventArgs e)
		{
			IManifest manifest = ModEntry.Instance.ModManifest;

			IMultiplayerPeerMod mod = e.Peer.HasSmapi ? e.Peer.GetMod(id: manifest.UniqueID) : null;
			Log.D($"Multiplayer peer connected:{Environment.NewLine}{e.Peer.PlayerID} SMAPI:{(e.Peer.HasSmapi ? $"{e.Peer.ApiVersion.ToString()} (SDV:{e.Peer.Platform.Value} {e.Peer.GameVersion})" : "N/A")}",
				Config.DebugMode);

			if (mod == null)
			{
				Log.D($"Peer does not have {manifest.Name} loaded.",
					Config.DebugMode);
			}
			else if (mod.Version.CompareTo(manifest.Version) != 0)
			{
				Log.D($"Peer {manifest.Name} version does not match host (peer: {mod.Version.ToString()}, host: {manifest.Version.ToString()}).",
					Config.DebugMode);
			}
		}

		private static void GameLoop_ReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
		{
			Bundles._cc = null;
			Bundles.CustomAreaBundleDictionary = null;
			Bundles.CustomAreaNameAndNumberDictionary = null;
			Bundles.CustomAreaInitialIndex = 0;
			Bundles.CustomBundleInitialIndex = 0;
			Bundles.DefaultMaxArea = 0;
			Bundles.DefaultMaxBundle = 0;
		}

		private static void GameLoop_Saving(object sender, SavingEventArgs e)
		{
			// Unload community centre data late
			if (!Context.IsMainPlayer)
			{
				Log.D("Unloading world bundle data at saving.",
					ModEntry.Config.DebugMode);
				Bundles.SaveAndUnloadBundleData(Bundles.CC);
			}
		}

		private static void GameLoop_DayEnding(object sender, DayEndingEventArgs e)
		{
			// Save local (and/or persistent) community centre data
			if (Context.IsMainPlayer)
			{
				Log.D("Unloading world bundle data at end of day.",
					ModEntry.Config.DebugMode);
				Bundles.SaveAndUnloadBundleData(Bundles.CC);
			}
		}

		private static void GameLoop_DayStarted(object sender, DayStartedEventArgs e)
		{
			Bundles.DayStartedBehaviours(Bundles.CC);
		}

		internal static void SetCustomAreaMutex(CommunityCenter cc, int areaNumber, bool isLocked)
		{
			if (!cc.modData.ContainsKey(Bundles.KeyMutexes))
			{
				cc.modData[Bundles.KeyMutexes] = string.Empty;
			}

			string sArea = areaNumber.ToString();
			string lockedAreas = cc.modData[Bundles.KeyMutexes];
			if (isLocked)
			{
				if (string.IsNullOrWhiteSpace(lockedAreas) || lockedAreas.Split(' ').All(s => s != sArea))
				{
					cc.modData[Bundles.KeyMutexes] = lockedAreas.Any() ? string.Join(" ", lockedAreas, sArea) : sArea;
					Game1.activeClickableMenu = new JunimoNoteMenu(whichArea: areaNumber, cc.bundlesDict());
				}
			}
			else
			{
				cc.modData[Bundles.KeyMutexes] = string.Join(" ", lockedAreas.Split(' ').Where(s => s != sArea));
			}
		}

		private static void Display_MenuChanged(object sender, MenuChangedEventArgs e)
		{
			if (e.OldMenu is TitleMenu || e.NewMenu is TitleMenu || Game1.currentLocation == null || Game1.player == null)
				return;

			CommunityCenter cc = Bundles.CC;
			if (e.OldMenu is JunimoNoteMenu junimoNoteMenu && e.NewMenu == null && !Bundles.IsCommunityCentreComplete(cc))
			{
				int areaNumber = Reflection.GetField
					<int>
					(junimoNoteMenu, "whichArea")
					.GetValue();

				// Unlock makeshift mutex
				Bundles.SetCustomAreaMutex(cc: cc, areaNumber: areaNumber, isLocked: false);

				// Play area complete cutscene on closing the completed junimo note menu
				// Without this override the cutscene only plays after re-entering and closing the menu
				if (areaNumber >= Bundles.CustomAreaInitialIndex
					&& Bundles.AreaAllCustomAreasComplete(cc))
				{
					cc.restoreAreaCutscene(areaNumber);
				}
			}
		}

		private static void Player_Warped(object sender, WarpedEventArgs e)
		{
			if ((!(e.NewLocation is CommunityCenter) && e.OldLocation is CommunityCenter)
				|| (!(e.OldLocation is CommunityCenter) && e.NewLocation is CommunityCenter))
			{
				Helper.Content.InvalidateCache(@"Maps/townInterior");
			}
		}

		private static void Input_ButtonPressed(object sender, ButtonPressedEventArgs e)
		{
			// In-game interactions
			if (!Game1.game1.IsActive || Game1.currentLocation == null || !Context.IsWorldReady)
				return;

			// Menu interactions
			if (e.Button.IsUseToolButton())
			{
				// Navigate community centre bundles inventory menu
				if (Game1.activeClickableMenu is JunimoNoteMenu menu && menu != null
					&& !Bundles.IsCommunityCentreComplete(Bundles.CC)
					&& Reflection.GetField
						<int>
						(menu, "whichArea")
						.GetValue() is int whichArea
					&& Bundles.CC.shouldNoteAppearInArea(whichArea))
				{
					if (!Game1.player.hasOrWillReceiveMail("canReadJunimoText"))
					{
						Game1.activeClickableMenu.exitThisMenu();
						return;
					}

					Point cursor = Utility.Vector2ToPoint(e.Cursor.ScreenPixels);
					Bundles.NavigateJunimoNoteMenu(cc: Bundles.CC, menu: menu, x: cursor.X, y: cursor.Y, whichArea: whichArea);
				}
			}

			// World interactions
			if (!Context.CanPlayerMove)
				return;

			// . . .
		}

		public static bool IsCommunityCentreComplete(CommunityCenter cc)
		{
			if (cc == null)
				return false;

			// Check pre-completion bundle mail
			bool masterPlayerComplete = Game1.MasterPlayer.hasCompletedCommunityCenter();

			// Check completion cutscenes
			bool cutsceneSeen = new int[]
			{
				(int)Bundles.EventIds.CommunityCentreComplete,
				(int)Bundles.EventIds.JojaWarehouseComplete
			}
			.Any(id => Game1.MasterPlayer.eventsSeen.Contains(id));

			// Check post-completion mail flags
			bool mailReceived = new string[]
			{
				"ccIsComplete",
				"abandonedJojaMartAccessible",
				"ccMovieTheater",
				"ccMovieTheater%&NL&%"
			}
			.Any(id => Game1.MasterPlayer.hasOrWillReceiveMail(id));

			return masterPlayerComplete || cutsceneSeen || mailReceived;
		}

		public static bool IsAbandonedJojaMartBundleAvailableOrComplete()
		{
			return Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("abandonedJojaMartAccessible");
		}

		public static bool AreAnyCustomAreasLoaded()
		{
			return Bundles.CustomAreaNameAndNumberDictionary != null && Bundles.CustomAreaNameAndNumberDictionary.Any();
		}

		public static bool AreAnyCustomBundlesLoaded()
		{
			return Game1.netWorldState.Value.BundleData
				.Any(pair => Bundles.CustomAreaNameAndNumberDictionary.ContainsKey(pair.Key.Split('/').First()));
		}

		public static bool AreaAllCustomAreasComplete(CommunityCenter cc)
		{
			if (cc == null)
				return false;

			bool customAreasLoaded = Bundles.AreAnyCustomAreasLoaded();
			bool customAreasComplete = !customAreasLoaded || cc.areasComplete.Skip(Bundles.DefaultMaxArea).All(isComplete => isComplete);
			bool customBundlesLoaded = Bundles.AreAnyCustomBundlesLoaded();
			bool customBundlesComplete = cc.bundles.Keys
				.Where(key => key >= CustomBundleInitialIndex)
				.All(key => cc.bundles[key].All(value => value));
			bool ccIsComplete = Bundles.IsCommunityCentreComplete(cc);

			string msg = $"AreaAllCustomAreasComplete:"
				+ $" (areas loaded: {customAreasLoaded}) || (areas complete: {customAreasComplete})"
				+ $" || (bundles loaded: {customBundlesLoaded}) || (bundles complete: {customBundlesComplete})"
				+ $" || (cc complete: {ccIsComplete})";
			Log.D(msg, Config.DebugMode);

			return !customAreasLoaded || customAreasComplete || !customBundlesLoaded || customBundlesComplete || ccIsComplete;
		}

		internal static void DrawStarInCommunityCentre(CommunityCenter cc)
		{
			const int id = ModEntry.DummyId + 5742;
			if (cc.getTemporarySpriteByID(id) != null)
				return;

			Multiplayer multiplayer = Reflection.GetField
				<Multiplayer>
				(typeof(Game1), "multiplayer")
				.GetValue();
			Vector2 position = new Vector2(2096f, 344f);
			multiplayer.broadcastSprites(cc,
				new TemporaryAnimatedSprite(
					textureName: @"LooseSprites/Cursors",
					sourceRect: new Rectangle(354, 401, 7, 7),
					animationInterval: 9999, animationLength: 1, numberOfLoops: 9999,
					position: position,
					flicker: false, flipped: false,
					layerDepth: 0.8f,
					alphaFade: 0f, color: Color.White,
					scale: Game1.pixelZoom, scaleChange: 0f,
					rotation: 0f, rotationChange: 0f)
				{
					id = id,
					holdLastFrame = true
				});
		}

		internal static void NavigateJunimoNoteMenu(CommunityCenter cc, JunimoNoteMenu menu, int x, int y, int whichArea)
		{
			bool isPrevious = menu.areaBackButton != null && menu.areaBackButton.visible && menu.areaBackButton.containsPoint(x, y);
			bool isNext = menu.areaNextButton != null && menu.areaNextButton.visible && menu.areaNextButton.containsPoint(x, y);

			if (!isPrevious && !isNext)
				return;

			ModEntry.Instance._lastJunimoNoteMenuArea.Value = -1;

			// Fetch the bounds of the menu, exclusive of our new area since we're assuming we start there
			// Exclude any already-completed areas from this search, since we're looking for unfinished areas

			int isAreaValid(int i, bool isMax = true)
			{
				return !cc.areasComplete[i] && cc.shouldNoteAppearInArea(i)
					? i
					: isMax ? -1 : 999;
			}

			int[] areaNumbers = Bundles.GetAllAreaNames()
				.Select(areaName => CommunityCenter.getAreaNumberFromName(areaName))
				.ToArray();
			
			int lowestArea = areaNumbers.Min(i => isAreaValid(i, false));
			int highestArea = areaNumbers.Max(i => isAreaValid(i, true));

			int nextLowestArea = whichArea == lowestArea
				? highestArea
				: areaNumbers.Where(i => i < whichArea).Last(i => isAreaValid(i) != -1);
			int nextHighestArea = whichArea == highestArea
				? lowestArea
				: areaNumbers.Where(i => i > whichArea).First(i => isAreaValid(i) != -1);

			if (isPrevious)
				ModEntry.Instance._lastJunimoNoteMenuArea.Value = nextLowestArea;
			else
				ModEntry.Instance._lastJunimoNoteMenuArea.Value = nextHighestArea;

			if (ModEntry.Instance._lastJunimoNoteMenuArea.Value > -1
				&& ModEntry.Instance._lastJunimoNoteMenuArea.Value != whichArea)
			{
				// Change the menu tab on the next tick to avoid errors
				Helper.Events.GameLoop.UpdateTicked += Bundles.Event_ChangeJunimoMenuArea;
			}
		}

		public static void GiveBundleItems(CommunityCenter cc, int whichBundle, bool print)
		{
			KeyValuePair<string, string> bundle = Game1.netWorldState.Value.BundleData
				.FirstOrDefault(pair => pair.Key.Split('/')[1] == whichBundle.ToString());
			string[] split = bundle.Value.Split('/');
			string[] itemData = split[2].Split(' ');
			int itemLimit = split.Length < 5 ? 99 : int.Parse(split[4]);
			for (int i = 0; i < itemData.Length && i < itemLimit * 3; ++i)
			{
				int index = int.Parse(itemData[i]);
				int quantity = int.Parse(itemData[++i]);
				int quality = int.Parse(itemData[++i]);
				if (index == -1)
					Game1.player.addUnearnedMoney(quantity);
				else
					Game1.createItemDebris(
						item: new StardewValley.Object(index, quantity, isRecipe: false, price: -1, quality: quality),
						origin: Game1.player.Position,
						direction: -1);
			}
			Log.D($"Giving items for {bundle.Key}: {bundle.Value.Split('/')[0]} bundle.",
				print);
		}

		public static void GiveAreaItems(CommunityCenter cc, int whichArea, bool print)
		{
			var areaBundleDict = Helper.Reflection.GetField
				<Dictionary<int, List<int>>>
				(cc, "areaToBundleDictionary")
				.GetValue();
			if (!areaBundleDict.ContainsKey(whichArea))
			{
				Log.D("No area found.");
				return;
			}

			foreach (int bundle in areaBundleDict[whichArea])
			{
				if (whichArea >= Bundles.CustomAreaInitialIndex)
				{
					if (Bundles.IsAbandonedJojaMartBundleAvailableOrComplete() && bundle >= CustomBundleInitialIndex)
						continue;
					else if (!Bundles.IsAbandonedJojaMartBundleAvailableOrComplete() && bundle < CustomBundleInitialIndex)
						continue;
				}
				if (cc.isBundleComplete(bundle))
					continue;
				Bundles.GiveBundleItems(cc: cc, bundle, print: print);
			}
		}

		private static void ResetBundleProgress(CommunityCenter cc, int whichBundle, bool print)
		{
			Dictionary<int, int> bad = Reflection.GetField
				<Dictionary<int, int>>
				(cc, "bundleToAreaDictionary")
				.GetValue();

			cc.bundleRewards[whichBundle] = false;
			cc.bundles[whichBundle] = new bool[cc.bundles[whichBundle].Length];
			Game1.netWorldState.Value.BundleRewards[whichBundle] = false;
			Game1.netWorldState.Value.Bundles[whichBundle] = new bool[Game1.netWorldState.Value.Bundles[whichBundle].Length];

			if (cc.areasComplete[bad[whichBundle]])
			{
				cc.areasComplete[bad[whichBundle]] = false;
				cc.loadMap(cc.Map.assetPath, force_reload: true);
			}

			KeyValuePair<string, string> bundle = Game1.netWorldState.Value.BundleData
				.FirstOrDefault(pair => pair.Key.Split('/')[1] == whichBundle.ToString());
			Log.D($"Reset progress for {bundle.Key}: {bundle.Value.Split('/')[0]} bundle.",
				print);
		}

		internal static void ParseBundleData(bool isLoadingCustomContent)
		{
			// Fetch initial area-bundle values if not yet set
			if (Bundles.DefaultMaxArea == 0)
			{
				var bundleData = Game1.content.Load
					<Dictionary<string, string>>
					(@"Data/Bundles");

				// Area count is inclusive of Abandoned Joja Mart area to avoid conflicting logic and cases
				Bundles.DefaultMaxArea = bundleData.Keys
					.Select(key => key.Split('/').First())
					.Distinct()
					.Count() - 1;

				// Bundle count is inclusive of Abandoned Joja Mart bundles, as each requires a unique ID
				Bundles.DefaultMaxBundle = bundleData.Keys
					.Select(key => key.Split('/').Last())
					.ToList()
					.ConvertAll(int.Parse)
					.Max();

				// Starting index for our custom bundles' unique IDs is after the highest base game bundle ID
				Bundles.CustomBundleInitialIndex = Bundles.DefaultMaxBundle + 1;

				// Starting index for our custom areas' numbers is after the Abandoned Joja Mart area
				// The game will usually consider area 7 as the bulletin board extras, and area 8 as the Junimo Hut
				Bundles.CustomAreaInitialIndex = 9;
			}

			// Reassign world state with or without custom values
			Random r = new Random((int)Game1.uniqueIDForThisGame * 9);
			if (isLoadingCustomContent)
			{
				Dictionary<string, string> bundleData = new BundleGenerator().Generate(
					bundle_data_path: AssetManager.GameBundleDefinitionsPath,
					rng: r);

				// Assign each bundle a unique number in both game and custom dictionaries
				// At this stage, bundle keys use a number unique to their area
				int areaSum = 0;
				int bundleSum = 0;
				var bundleKeysToEdit = bundleData.Keys
					.Select(s => s.Split('/'))
					.Where(key => Bundles.CustomAreaNameAndNumberDictionary.ContainsKey(key.First()))
					.ToArray();

				Dictionary<string, int[]> areasAndBundlesToEdit = bundleKeysToEdit
					.Select(ss => ss.First()).Distinct()
					.ToDictionary(
						keySelector: key => key,
						elementSelector: key => bundleKeysToEdit
							.Where(bk => bk.First() == key)
							.Select(bk => bk.Last())
							.ToList()
							.ConvertAll(int.Parse)
							.ToArray());

				foreach (KeyValuePair<string, int[]> areaAndBundleNumbers in areasAndBundlesToEdit)
				{
					string areaName = areaAndBundleNumbers.Key;

					Bundles.CustomAreaNameAndNumberDictionary[areaName] = areaSum;

					foreach (int oldBundleNumber in areaAndBundleNumbers.Value)
					{
						int newBundleNumber = Bundles.CustomBundleInitialIndex + bundleSum + oldBundleNumber;
						string newBundleKey = $"{areaName}/{newBundleNumber}";
						string oldBundleKey = $"{areaName}/{oldBundleNumber}";
						string bundleName = Bundles.CustomAreaBundleDictionary[areaName][oldBundleNumber].Split('/').First();

						// Update custom area-bundle dict with bundle name and new number
						Bundles.CustomAreaBundleDictionary[areaName][oldBundleNumber] =
							$"{bundleName}/{newBundleNumber}";
						// Update game area-bundle dict with area name and new number
						bundleData[newBundleKey] = bundleData[oldBundleKey];
						bundleData.Remove(oldBundleKey);
					}

					bundleSum += areaAndBundleNumbers.Value.Length;
					areaSum++;
				}

				Game1.netWorldState.Value.SetBundleData(bundleData);
			}
			else
			{
				Game1.GenerateBundles(Game1.bundleType);

				if (Context.IsMainPlayer && Bundles.CustomAreaBundleDictionary != null)
				{
					var netBundleData = Reflection.GetField
						<NetStringDictionary<string, NetString>>
						(obj: Game1.netWorldState.Value, name: "netBundleData")
						.GetValue();

					foreach (string bundleKey in Bundles.CustomAreaBundleDictionary.Values.SelectMany(bundleKey => bundleKey))
					{
						int bundleNumber = int.Parse(bundleKey.Split('/').Last());
						netBundleData.Remove(netBundleData.Keys
							.FirstOrDefault(b => bundleNumber == int.Parse(b.Split('/').Last())));
						Game1.netWorldState.Value.Bundles.Remove(bundleNumber);
						Game1.netWorldState.Value.BundleRewards.Remove(bundleNumber);
					}
				}
			}
		}
		
		internal static void LoadBundleData(CommunityCenter cc)
		{
			if (Bundles.IsCommunityCentreComplete(cc))
				return;

			// Set world state to include custom content:

			Bundles.ParseBundleData(isLoadingCustomContent: true);

			Dictionary<string, string> bundleData = Game1.netWorldState.Value.GetUnlocalizedBundleData();
			IEnumerable<string> areaNames = Bundles.CustomAreaBundleDictionary.Keys;
			IDictionary<string, int> bundleNamesAndNumbers = Bundles.GetBundleNamesAndNumbersFromBundleKeys(
				Bundles.CustomAreaBundleDictionary.Values
					.SelectMany(s => s));

			Dictionary<string, bool> areasData = null;
			Dictionary<string, bool> bundleRewardsData = null;

			// Host player loads preserved/persistent data from world:
			if (Context.IsMainPlayer)
			{
				// Deserialise saved mod data:

				if (cc.modData.TryGetValue(Bundles.KeyAreasComplete, out string rawAreasComplete) && !string.IsNullOrWhiteSpace(rawAreasComplete))
				{
					// Deserialise saved area-bundle mod data
					Dictionary<string, bool> savedAreasComplete = rawAreasComplete
					.Split('/')
					.ToDictionary(
						keySelector: s => s.Split(':').First(),
						elementSelector: s => bool.Parse(s.Split(':').Last()));

					// Read saved area-bundle setups

					areasData = areaNames
						// include newly-added area-bundles
						.Where(name => !savedAreasComplete.ContainsKey(name))
						.ToDictionary(name => name, name => false)
						// include saved area-bundles if their metadata is loaded
						// saved area-bundles are excluded if metadata is missing
						.Concat(savedAreasComplete.Where(pair => areaNames.Contains(pair.Key)))
						.ToDictionary(pair => pair.Key, pair => pair.Value);

					// Check for saved data with no matching metadata
					IEnumerable<string> excludedAreas = savedAreasComplete.Select(pair => pair.Key).Except(areasData.Keys);
					if (excludedAreas.Any())
					{
						string s1 = string.Join(", ", excludedAreas);
						string message = $"Removing saved data for area-bundles with missing metadata:{Environment.NewLine}{s1}";
						Log.W(message);
					}
				}

				// Same as above code for deserialising areas complete, but for bundle rewards
				if (cc.modData.TryGetValue(Bundles.KeyBundleRewards, out string rawBundleRewards) && !string.IsNullOrWhiteSpace(rawBundleRewards))
				{
					Dictionary<string, bool> savedBundleRewards = rawBundleRewards
						.Split('/')
						.ToDictionary(
							keySelector: s => s.Split(':').First(),
							elementSelector: s => bool.Parse(s.Split(':').Last()));

					bundleRewardsData = bundleNamesAndNumbers
						.Where(nameAndNum => !savedBundleRewards.Keys.Any(key => key == nameAndNum.Key))
						.ToDictionary(nameAndNum => nameAndNum.Key, name => false)
						.Concat(savedBundleRewards.Where(pair => bundleNamesAndNumbers.Keys.Contains(pair.Key)))
						.ToDictionary(pair => pair.Key, pair => pair.Value);

					IEnumerable<string> excludedBundles = savedBundleRewards.Select(pair => pair.Key).Except(bundleRewardsData.Keys);
					if (excludedBundles.Any())
					{
						string s = string.Join(", ", excludedBundles);
						string message = $"Removing saved data for bundle rewards with missing metadata:{Environment.NewLine}{s}";
						Log.W(message);
					}
				}

				// Load donated bundle items from world storage, populating bundle progress dictionary:

				// Fetch world storage chest
				Vector2 tileLocation = Utility.PointToVector2(Bundles.BundleDonationsChestTile);
				if (cc.Objects.TryGetValue(tileLocation, out StardewValley.Object o) && o is Chest chest)
				{
					// Compare items against bundle requirements fields in bundle data
					IEnumerable<string> customBundleDataKeys = Game1.netWorldState.Value.BundleData
						.Where(pair => int.Parse(pair.Key.Split('/').Last()) > Bundles.DefaultMaxBundle)
						.Select(pair => pair.Key);
					foreach (string bundleKey in customBundleDataKeys)
					{
						const int fields = 3;
						int bundleId = int.Parse(bundleKey.Split('/').Last());
						string rawBundleInfo = Game1.netWorldState.Value.BundleData[bundleKey];
						string[] split = rawBundleInfo.Split('/');
						string[] ingredientsSplit = split[2].Split(' ');
						bool[] ingredientsComplete = new bool[ingredientsSplit.Length];
						cc.bundles[bundleId].CopyTo(array: ingredientsComplete, index: 0);

						for (int ingredient = 0; ingredient < ingredientsSplit.Length; ingredient += fields)
						{
							int ingredientIndex = ingredient / fields;
							BundleIngredientDescription bundleIngredientDescription =
								new BundleIngredientDescription(
									index: Convert.ToInt32(ingredientsSplit[ingredient]),
									stack: Convert.ToInt32(ingredientsSplit[ingredient + 1]),
									quality: Convert.ToInt32(ingredientsSplit[ingredient + 2]),
									completed: true);

							// Check chest items for use in bundles and repopulate bundle progress data
							foreach (Item item in chest.items.ToList())
							{
								if (item is StardewValley.Object o1
									&& (o1.ParentSheetIndex == bundleIngredientDescription.index
										|| (bundleIngredientDescription.index < 0 && o1.Category == bundleIngredientDescription.index))
									&& o1.Quality >= bundleIngredientDescription.quality
									&& o1.Stack == bundleIngredientDescription.stack)
								{
									ingredientsComplete[ingredientIndex] = true;
									chest.items.Remove(item);
									break;
								}
							}
						}

						// If the first (len/3) elements are true, the bundle should be marked as complete (mark all other elements true)
						if (ingredientsComplete.Take(ingredientsSplit.Length / 3).All(isComplete => isComplete))
						{
							for (int i = 0; i < ingredientsComplete.Length; ++i)
							{
								ingredientsComplete[i] = true;
							}
						}

						cc.bundles[bundleId] = ingredientsComplete;
					}

					chest.clearNulls();

					// Remove chest if empty
					if (!chest.items.Any(i => i != null))
					{
						cc.Objects.Remove(tileLocation);
					}
				}
			}

			// With no previous saved data, load current area-bundle setup as-is
			if (areasData == null || !areasData.Any())
			{
				areasData = areaNames.ToDictionary(name => name, name => false);
			}
			if (bundleRewardsData == null || !bundleRewardsData.Any())
			{
				bundleRewardsData = bundleNamesAndNumbers.ToDictionary(nameAndNum => nameAndNum.Key, name => false);
			}

			// Set CC state to include custom content:

			// Set areas complete array to reflect saved data length and contents
			if (areasData.Any())
			{
				cc.areasComplete.SetCount(size: Bundles.CustomAreaInitialIndex + areasData.Count);
				foreach (string areaName in areasData.Keys)
				{
					int areaNumber = CommunityCenter.getAreaNumberFromName(areaName);
					cc.areasComplete[areaNumber] = areasData[areaName];
				}

				cc.areasComplete[CommunityCenter.AREA_Bulletin2] = true;
				cc.areasComplete[CommunityCenter.AREA_JunimoHut] = true;
			}

			Bundles.ReplaceAreaBundleConversions(cc: cc);
			cc.refreshBundlesIngredientsInfo();

			// Set bundle rewards dictionary to reflect saved data
			foreach (string bundleName in bundleRewardsData.Keys)
			{
				int bundleNumber = bundleNamesAndNumbers[bundleName];
				cc.bundleRewards[bundleNumber] = bundleRewardsData[bundleName];
			}

			Log.D("Loaded bundle data.",
				Config.DebugMode);
		}

		internal static void SaveAndUnloadBundleData(CommunityCenter cc)
		{
			// Host player saves preserved/persistent data to world:
			if (Context.IsMainPlayer)
			{
				// Save donated bundle items to world storage
				{
					Vector2 tileLocation = Utility.PointToVector2(Bundles.BundleDonationsChestTile);
					Chest chest = cc.Objects.TryGetValue(tileLocation, out StardewValley.Object o) && o is Chest
						? o as Chest
						: new Chest(playerChest: true, tileLocation: tileLocation);

					// Cross-reference bundle numbers with names to get bundle requirement item strings
					BundleGenerator bundleGenerator = new BundleGenerator();
					List<RandomBundleData> generatedBundles = Bundles.GetGeneratedBundles();
					Dictionary<string, string> bundleNamesAndItems = generatedBundles
						.Select(rbd => rbd.BundleSets.SelectMany(bsd => bsd.Bundles).Concat(rbd.Bundles))
						.SelectMany(bd => bd.Select(bd => new KeyValuePair<string, string>(bd.Name, bd.Items)))
						.ToDictionary(pair => pair.Key, pair => pair.Value);
					int[] bundleNumbers = Game1.netWorldState.Value.Bundles.Keys
						.Where(key => key > Bundles.DefaultMaxBundle)
						.ToArray();
					foreach (int bundleNumber in bundleNumbers)
					{
						string bundleName = bundleNamesAndItems.Keys
							.FirstOrDefault(s => s == Game1.netWorldState.Value.BundleData
								.FirstOrDefault(pair => int.Parse(pair.Key.Split('/').Last()) == bundleNumber).Value.Split('/').First());
						if (bundleName != null && bundleNamesAndItems.TryGetValue(bundleName, out string itemString))
						{
							string[] itemStrings = itemString.Split(',');
							for (int i = 0; i < itemStrings.Length; ++i)
							{
								if (Game1.netWorldState.Value.Bundles[bundleNumber][i])
								{
									Item item = bundleGenerator.ParseItemString(itemStrings[i].Trim());
									chest.items.Add(item);
								}
							}
						}
					}

					chest.clearNulls();
					if (chest.items.Any())
					{
						if (cc.Objects.ContainsKey(tileLocation))
						{
							if (o != null)
							{
								// Add colliding items into the chest
								chest.addItem(cc.Objects[tileLocation]);
							}
						}
						cc.Objects[tileLocation] = chest;
					}
				}

				// Serialise mod data to be saved
				if (Bundles.CustomAreaBundleDictionary != null)
				{
					Dictionary<string, bool> areasCompleteData =
						Bundles.CustomAreaBundleDictionary.Keys
						.ToDictionary(
							keySelector: areaName => areaName,
							elementSelector: areaName => cc.areasComplete[CommunityCenter.getAreaNumberFromName(areaName)]);
					Dictionary<string, bool> bundleRewardsData =
						Bundles.CustomAreaBundleDictionary.Values
						.SelectMany(s => s)
						.ToDictionary(
							keySelector: bundleKey => bundleKey.Split('/').First(),
							elementSelector: bundleKey => cc.bundleRewards[int.Parse(bundleKey.Split('/').Last())]);

					string serialisedAreasCompleteData = string.Join("/", areasCompleteData.Select(pair => $"{pair.Key}:{pair.Value}"));
					string serialisedBundleRewardsData = string.Join("/", bundleRewardsData.Select(pair => $"{pair.Key}:{pair.Value}"));

					cc.modData[Bundles.KeyAreasComplete] = serialisedAreasCompleteData;
					cc.modData[Bundles.KeyBundleRewards] = serialisedBundleRewardsData;
				}
			}

			// Set CC state to exclude custom content

			List<bool> areasCompleteAbridged = cc.areasComplete.Take(Bundles.DefaultMaxArea).ToList();

			/*
			FOR host WITH farmhand AFTER farmhand goto+wh AND sleep
			 * 
			[01:13:21 ERROR Custom Community Centre] This mod failed in the GameLoop.DayEnding event. Technical details: 
			System.InvalidOperationException: Operation is not valid due to the current state of the object.
				at Netcode.NetArray`2.Clear() in C:\GitlabRunner\builds\Gq5qA5P4\0\ConcernedApe\stardewvalley\Farmer\Netcode\NetArray.cs:line 93
				at CustomCommunityCentre.Bundles.SaveAndUnloadBundleData(CommunityCenter cc) in E:\Dev\Projects\SDV\Projects\CustomCommunityCentre\CustomCommunityCentre\Bundles.cs:line 970
				at CustomCommunityCentre.Bundles.GameLoop_DayEnding(Object sender, DayEndingEventArgs e) in E:\Dev\Projects\SDV\Projects\CustomCommunityCentre\CustomCommunityCentre\Bundles.cs:line 276
				at StardewModdingAPI.Framework.Events.ManagedEvent`1.Raise(TEventArgs args, Func`2 match) in C:\source\_Stardew\SMAPI\src\SMAPI\Framework\Events\ManagedEvent.cs:line 126
			*/

			cc.areasComplete.MarkDirty();

			cc.areasComplete.Clear(); // ERROR
			cc.areasComplete.SetCount(size: areasCompleteAbridged.Count);
			cc.areasComplete.Set(areasCompleteAbridged);

			// Reset world state to exclude custom content
			Bundles.ParseBundleData(isLoadingCustomContent: false);
			Bundles.ReplaceAreaBundleConversions(cc: cc);
			cc.refreshBundlesIngredientsInfo();

			Log.D("Unloaded bundle data.",
				Config.DebugMode);
		}

		internal static void ReplaceAreaBundleConversions(CommunityCenter cc)
		{
			IReflectedField<Dictionary<int, List<int>>> areaBundleDictField = Reflection.GetField
				<Dictionary<int, List<int>>>
				(cc, "areaToBundleDictionary");
			IReflectedField<Dictionary<int, int>> bundleAreaDictField = Reflection.GetField
				<Dictionary<int, int>>
				(cc, "bundleToAreaDictionary");

			Dictionary<int, List<int>> areaBundleDict = areaBundleDictField.GetValue() ?? new Dictionary<int, List<int>>();
			Dictionary<int, int> bundleAreaDict = bundleAreaDictField.GetValue() ?? new Dictionary<int, int>();

			Dictionary<string, int> areaNamesAndNumbers = Game1.netWorldState.Value.BundleData.Keys
				.Select(bundleKey => bundleKey.Split('/').First())
				.Distinct()
				.ToDictionary(
					keySelector: areaName => areaName,
					elementSelector: areaName => CommunityCenter.getAreaNumberFromName(areaName));

			// Load custom area-bundle pairs (and base game data if not yet loaded)
			if (!cc.bundleMutexes.Any())
			{
				for (int i = 0; i < Bundles.DefaultMaxArea + 1; ++i)
				{
					NetMutex netMutex = new NetMutex();
					cc.bundleMutexes.Add(netMutex);
					cc.NetFields.AddField(netMutex.NetFields);
				}
			}
			if (!areaBundleDict.Any())
			{
				for (int i = 0; i < Bundles.DefaultMaxArea + 1; ++i)
				{
					areaBundleDict[i] = new List<int>();
				}
			}
			for (int i = Bundles.DefaultMaxArea + 1; i < areaNamesAndNumbers.Count + 2; ++i)
			{
				areaBundleDict[i] = new List<int>();
			}
			foreach (string bundleKey in Game1.netWorldState.Value.BundleData.Keys)
			{
				string areaName = bundleKey.Split('/').First();
				int areaNumber = areaNamesAndNumbers[areaName];
				int bundleNumber = Convert.ToInt32(bundleKey.Split('/').Last());

				bundleAreaDict[bundleNumber] = areaNumber;
				if (!areaBundleDict[areaNumber].Contains(bundleNumber))
				{
					areaBundleDict[areaNumber].Add(bundleNumber);
				}
			}
			
			areaBundleDictField.SetValue(areaBundleDict);
			bundleAreaDictField.SetValue(bundleAreaDict);
		}

		internal static BundleMetadata GetBundleMetadataFromAreaNumber(int areaNumber)
		{
			string areaName = Bundles.GetAreaNameFromNumber(areaNumber);
			return Bundles.BundleMetadata.FirstOrDefault(bmd => bmd.AreaName == areaName);
		}

		internal static int GetAreaNumberFromName(string areaName)
		{
			return !string.IsNullOrWhiteSpace(areaName)
				&& Bundles.CustomAreaNameAndNumberDictionary != null
				&& Bundles.CustomAreaNameAndNumberDictionary.TryGetValue(areaName, out int i)
				? Bundles.CustomAreaInitialIndex + i
				: -1;
		}

		internal static string GetAreaNameFromNumber(int areaNumber)
		{
			areaNumber -= Bundles.CustomAreaInitialIndex;
			string name = Bundles.CustomAreaNameAndNumberDictionary?.Keys
					.FirstOrDefault(key => Bundles.CustomAreaNameAndNumberDictionary[key] == areaNumber);
			return name;
		}

		internal static IEnumerable<string> GetAllAreaNames()
		{
			return Game1.netWorldState.Value.BundleData.Keys.Select(s => s.Split('/').First()).Distinct();
		}

		internal static IDictionary<string, int> GetAllBundleNamesAndNumbers()
		{
			return Game1.netWorldState.Value.BundleData.ToDictionary(
				keySelector: pair => pair.Value.Split('/').First(),
				elementSelector: pair => int.Parse(pair.Key.Split('/').Last()));
		}

		internal static Dictionary<string, int> GetBundleNamesAndNumbersFromBundleKeys(IEnumerable<string> bundleKeys)
		{
			return bundleKeys.ToDictionary(
				keySelector: s => s.Split('/').First(),
				elementSelector: s => int.Parse(s.Split('/').Last()));
		}

		public static bool IsAreaCompleteAndLoaded(CommunityCenter cc, string areaName)
		{
			int areaNumber = Bundles.GetAreaNumberFromName(Bundles.GetAllAreaNames().FirstOrDefault(s => s == areaName));
			return Bundles.IsAreaCompleteAndLoaded(cc: cc, areaNumber: areaNumber);
		}

		public static bool IsAreaCompleteAndLoaded(CommunityCenter cc, int areaNumber)
		{
			return areaNumber >= 0 && areaNumber < cc.areasComplete.Length && cc.areasComplete[areaNumber];
		}

		public static bool HasOrWillReceiveAreaCompletedMailForAllCustomAreas()
		{
			return Bundles.BundleMetadata
				.Select(bm => bm.AreaName)
				.Distinct()
				.All(areaName => Game1.MasterPlayer.hasOrWillReceiveMail(string.Format(Bundles.MailAreaCompleted, areaName)));
		}

		public static List<RandomBundleData> GetGeneratedBundles()
		{
			return Game1.content.Load
				<List<RandomBundleData>>
				(AssetManager.GameBundleDefinitionsPath);
		}

		public static bool IsMultiplayer()
		{
			return Game1.IsMultiplayer || Bundles.GetNumberOfCabinsBuilt() > 0;
		}

		public static int GetNumberOfCabinsBuilt()
		{
			return Game1.getFarm().buildings.Count(building => building.buildingType.Value.EndsWith("Cabin"));
		}

		public static void SetCC(CommunityCenter cc)
		{
			Bundles._cc = cc;
		}

		internal static void PrintBundleData(CommunityCenter cc)
		{
			if (cc == null)
			{
				Log.D("Cannot print Community Centre info when location is not loaded.");
				return;
			}

			var bundleAreaDict = Reflection.GetField
				<Dictionary<int, int>>
				(cc, "bundleToAreaDictionary")
				.GetValue();
			var areaBundleDict = Reflection.GetField
				<Dictionary<int, List<int>>>
				(cc, "areaToBundleDictionary")
				.GetValue();

			LogLevel logHigh = Config.DebugMode ? LogLevel.Warn : LogLevel.Trace;
			LogLevel logLow = Config.DebugMode ? LogLevel.Info : LogLevel.Trace;

			System.Text.StringBuilder msg = new System.Text.StringBuilder()
				.AppendLine($"Area Max: {DefaultMaxArea}, Count: {TotalAreaCount}")
				.AppendLine($"Bundle Max: {DefaultMaxBundle}, Count: {TotalBundleCount}")
				.AppendLine($"Multiplayer: (G:{Game1.IsMultiplayer}-B:{Bundles.IsMultiplayer()}), Host game: ({Game1.IsMasterGame}), Host player: ({Context.IsMainPlayer})")
				.AppendLine($"IsAbandonedJojaMartBundleAvailableOrComplete: {Bundles.IsAbandonedJojaMartBundleAvailableOrComplete()}")
				.AppendLine($"IsCommunityCentreComplete: {Bundles.IsCommunityCentreComplete(cc)}")
				.AppendLine($"AreAnyCustomAreasLoaded:  {Bundles.AreAnyCustomAreasLoaded()}")
				.AppendLine($"AreaAllCustomAreasComplete:  {Bundles.AreaAllCustomAreasComplete(cc)}")
				.AppendLine($"HasOrWillReceiveAreaCompletedMailForAllCustomAreas:  {Bundles.HasOrWillReceiveAreaCompletedMailForAllCustomAreas()}")
				.AppendLine($"BundleMutexes: {cc.bundleMutexes.Count}")
				;

			(string title, string body)[] messages = new[]
			{
				// General info
				($"CCC: General info",
				msg.ToString()),

				// Area info
				($"CCC: CC areasComplete[{Reflection.GetMethod(cc, "getNumberOfAreasComplete").Invoke<int>()}/{cc.areasComplete.Count}]:",
				string.Join(Environment.NewLine, cc.areasComplete)),
				($"CCC: CC bundleToAreaDictionary[{bundleAreaDict.Count}]:",
				string.Join(Environment.NewLine, bundleAreaDict.Select(pair => $"({pair.Key}: {pair.Value})"))),
				($"CCC: CC areaToBundleDictionary[{areaBundleDict.Count}]:",
				string.Join(Environment.NewLine, areaBundleDict.Select(pair => $"({pair.Key}: {string.Join(" ", pair.Value.Select(i => i))})"))),

				// Bundle info
				($"CCC: GW bundleData[{Game1.netWorldState.Value.BundleData.Count}]:",
				string.Join(Environment.NewLine, Game1.netWorldState.Value.BundleData.Select(pair => $"{pair.Key}: {pair.Value}"))),
				($"CCC: GW bundles[{Game1.netWorldState.Value.Bundles.Count()}]:",
				string.Join(Environment.NewLine, Game1.netWorldState.Value.Bundles.Pairs.Select(pair => $"{pair.Key}: {string.Join(" ", pair.Value)}"))),
				($"CCC: GW bundleRewards[{Game1.netWorldState.Value.BundleRewards.Count()}]:",
				string.Join(Environment.NewLine, Game1.netWorldState.Value.BundleRewards.Pairs.Select(pair => $"{pair.Key}: {pair.Value}"))),
			};

			foreach ((string title, string body) in messages)
			{
				Bundles.Monitor.Log(title, logHigh);
				Bundles.Monitor.Log($"{Environment.NewLine}{body}", logLow);
			}
		}

		private static void Event_ChangeJunimoMenuArea(object sender, UpdateTickedEventArgs e)
		{
			Helper.Events.GameLoop.UpdateTicked -= Bundles.Event_ChangeJunimoMenuArea;
			Reflection.GetField
				<int>
				((JunimoNoteMenu)Game1.activeClickableMenu, "whichArea")
				.SetValue(ModEntry.Instance._lastJunimoNoteMenuArea.Value);
			if (ModEntry.Instance._lastJunimoNoteMenuArea.Value >= Bundles.CustomAreaInitialIndex)
			{
				((JunimoNoteMenu)Game1.activeClickableMenu).bundles.Clear();
				((JunimoNoteMenu)Game1.activeClickableMenu).setUpMenu(
					whichArea: ModEntry.Instance._lastJunimoNoteMenuArea.Value,
					bundlesComplete: Bundles.CC.bundlesDict());
			}
		}
	}
}
