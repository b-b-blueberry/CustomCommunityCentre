using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
using System.Linq;

// TODO: REFACTOR: Move important load/unload/parse/replace methods to BundleManager class

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
			? _cc ??= Game1.getLocationFromName(nameof(CommunityCenter)) as CommunityCenter
			: _cc;

		// Kitchen areas and bundles
		public static int DefaultMaxArea;
		public static int DefaultMaxBundle;
		public static int CustomAreaInitialIndex;
		public static int CustomBundleInitialIndex;
		public static int DefaultAreaCount => Bundles.CC.areasComplete.Count();
		public static int CustomAreaCount => Bundles.CustomAreasComplete.Count();
		public static int TotalAreaCount => Bundles.DefaultAreaCount + Bundles.CustomAreaCount;
		public static int TotalBundleCount => Bundles.CC.bundles.Count();
		public static Dictionary<string, int> CustomAreaNamesAndNumbers = new Dictionary<string, int>();
		public static Dictionary<string, string[]> CustomAreaBundleKeys = new Dictionary<string, string[]>();
		public static Dictionary<int, bool> CustomAreasComplete = new Dictionary<int, bool>();
		public static List<BundleMetadata> CustomBundleMetadata = new List<BundleMetadata>();
		public static readonly Point CustomBundleDonationsChestTile = new Point(32, 9); // immediately ahead of cc fireplace
		public static readonly Vector2 CustomAreaStarPosition = new Vector2(2096f, 344f);
		public const char BundleKeyDelim = '/';
		public const char ModDataKeyDelim = '/';
		public const char ModDataValueDelim = ':';

		// Mail
		// Ensure all Mail values are referenced with string.Format(pattern, areaName) where appropriate
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
			Helper.Events.GameLoop.DayEnding += Bundles.GameLoop_DayEnding;
			Helper.Events.GameLoop.DayStarted += Bundles.GameLoop_DayStarted;
			Helper.Events.Display.MenuChanged += Bundles.Display_MenuChanged;
			Helper.Events.Display.RenderedWorld += Bundles.Display_RenderedWorld;
			Helper.Events.Player.Warped += Bundles.Player_Warped;
			Helper.Events.Input.ButtonPressed += Bundles.Input_ButtonPressed;
		}

		internal static void AddConsoleCommands(string cmd)
		{
			Helper.ConsoleCommands.Add(cmd + "print", "Print Community Centre bundle states.", (s, args) =>
			{
				Bundles.Print(Bundles.CC);
			});
			Helper.ConsoleCommands.Add(cmd + "list", "List all bundle IDs currently loaded.", (s, args) =>
			{
				Dictionary<int, int> bundleAreaDict = Helper.Reflection.GetField
					<Dictionary<int, int>>
					(Bundles.CC, "bundleToAreaDictionary").GetValue();
				string msg = string.Join(Environment.NewLine,
					Game1.netWorldState.Value.BundleData.Select(
						pair => $"[Area {bundleAreaDict[int.Parse(pair.Key.Split(Bundles.BundleKeyDelim).Last())]}] {pair.Key}: {pair.Value.Split(Bundles.BundleKeyDelim).First()}"));
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
						: string.Join(" ", args) is string areaName && !string.IsNullOrWhiteSpace(areaName)
							&& CommunityCenter.getAreaNumberFromName(areaName) is int i1
							? i1
						: -1
					: -1;

				if (areaNumber < 0 || areaNumber > Bundles.TotalAreaCount + 2)
				{
					Log.D($"No valid area name or number found for '{string.Join(" ", args)}'.");
					return;
				}

				Point tileLocation = Helper.Reflection
					.GetMethod(Bundles.CC, "getNotePosition")
					.Invoke<Point>(areaNumber);

				Log.D($"Warping to area {areaNumber} - {CommunityCenter.getAreaNameFromNumber(areaNumber)} ({tileLocation.ToString()})");

				tileLocation = Utility.Vector2ToPoint(
					Utility.recursiveFindOpenTiles(
						l: Bundles.CC,
						tileLocation: Utility.PointToVector2(tileLocation),
						maxOpenTilesToFind: 1,
						maxIterations: 24).First());

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
			foreach (KeyValuePair<string, int> areaNameAndNumber in Bundles.CustomAreaNamesAndNumbers)
			{
				string mailId = string.Format(Bundles.MailAreaCompletedFollowup, areaNameAndNumber.Key);
				if (Bundles.IsAreaComplete(cc, areaNumber: areaNameAndNumber.Value)
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
			// Local co-op farmhands should not clear custom area-bundle data
			if (!Context.IsSplitScreen || Context.IsMainPlayer)
			{
				Bundles.Clear();
			}
		}

		private static void GameLoop_DayEnding(object sender, DayEndingEventArgs e)
		{
			// Save local (and/or persistent) community centre data
			Log.D("Unloading world bundle data at end of day.",
				ModEntry.Config.DebugMode);
			Bundles.SaveAndUnloadBundleData(Bundles.CC);
		}

		private static void GameLoop_DayStarted(object sender, DayStartedEventArgs e)
		{
			Bundles.DayStartedBehaviours(Bundles.CC);
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

		private static void Display_RenderedWorld(object sender, RenderedWorldEventArgs e)
		{
			// Draw final star on community centre areas complete plaque
			if (!(Game1.currentLocation is CommunityCenter cc) || cc.numberOfStarsOnPlaque.Value <= Bundles.DefaultAreaCount)
				return;

			float alpha = Math.Max(0f, (Bundles.GetTotalAreasComplete(cc) / Bundles.TotalAreaCount));
			e.SpriteBatch.Draw(
				texture: Game1.mouseCursors,
				position: Game1.GlobalToLocal(viewport: Game1.viewport, globalPosition: Bundles.CustomAreaStarPosition),
				sourceRectangle: new Rectangle(354, 401, 7, 7),
				color: Color.White * alpha,
				rotation: 0f,
				origin: Vector2.Zero,
				scale: Game1.pixelZoom,
				effects: SpriteEffects.None,
				layerDepth: 0.8f);
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
			return Bundles.CustomAreaNamesAndNumbers != null && Bundles.CustomAreaNamesAndNumbers.Any();
		}

		public static bool AreAnyCustomBundlesLoaded()
		{
			return Game1.netWorldState.Value.BundleData
				.Any(pair => Bundles.CustomAreaNamesAndNumbers.ContainsKey(pair.Key.Split(Bundles.BundleKeyDelim).First()));
		}

		public static bool AreaAllCustomAreasComplete(CommunityCenter cc)
		{
			if (cc == null)
				return false;

			bool customAreasLoaded = Bundles.AreAnyCustomAreasLoaded();
			bool customAreasComplete = !customAreasLoaded || Bundles.CustomAreasComplete.Values.All(isComplete => isComplete);
			bool customBundlesLoaded = Bundles.AreAnyCustomBundlesLoaded();
			bool customBundlesComplete = cc.bundles.Keys
				.Where(key => key >= CustomBundleInitialIndex)
				.All(key => cc.bundles[key].All(value => value));
			bool ccIsComplete = Bundles.IsCommunityCentreComplete(cc);

			return !customAreasLoaded || customAreasComplete || !customBundlesLoaded || customBundlesComplete || ccIsComplete;
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

			int[] areaNumbers = Bundles.GetAllAreaNames()
				.Select(areaName => CommunityCenter.getAreaNumberFromName(areaName))
				.Where(areaNumber => !Bundles.IsAreaComplete(cc: cc, areaNumber: areaNumber) && cc.shouldNoteAppearInArea(areaNumber))
				.ToArray();
			
			int lowestArea = areaNumbers.Min();
			int highestArea = areaNumbers.Max();

			int nextLowestArea = whichArea == lowestArea
				? highestArea
				: areaNumbers.Where(i => i < whichArea).Last();
			int nextHighestArea = whichArea == highestArea
				? lowestArea
				: areaNumbers.Where(i => i > whichArea).First();

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
				.FirstOrDefault(pair => pair.Key.Split(Bundles.BundleKeyDelim).Last() == whichBundle.ToString());
			string[] split = bundle.Value.Split(Bundles.BundleKeyDelim);
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
			Log.D($"Giving items for {bundle.Key}: {bundle.Value.Split(Bundles.BundleKeyDelim).First()} Bundle.",
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

		public static void ResetBundleProgress(CommunityCenter cc, int whichBundle, bool print)
		{
			Dictionary<int, int> bundleAreaDict = Reflection.GetField
				<Dictionary<int, int>>
				(cc, "bundleToAreaDictionary")
				.GetValue();

			cc.bundleRewards[whichBundle] = false;
			cc.bundles[whichBundle] = new bool[cc.bundles[whichBundle].Length];
			Game1.netWorldState.Value.BundleRewards[whichBundle] = false;
			Game1.netWorldState.Value.Bundles[whichBundle] = new bool[Game1.netWorldState.Value.Bundles[whichBundle].Length];

			int areaNumber = bundleAreaDict[whichBundle];

			if (Bundles.IsAreaComplete(cc: cc, areaNumber: areaNumber))
			{
				if (areaNumber < Bundles.DefaultMaxArea)
				{
					cc.areasComplete[areaNumber] = false;
				}
				else if (Bundles.CustomAreasComplete.ContainsKey(areaNumber))
				{
					Bundles.CustomAreasComplete[areaNumber] = false;
				}

				cc.loadMap(cc.Map.assetPath, force_reload: true);
			}

			KeyValuePair<string, string> bundle = Game1.netWorldState.Value.BundleData
				.FirstOrDefault(pair => pair.Key.Split(Bundles.BundleKeyDelim).Last() == whichBundle.ToString());
			Log.D($"Reset progress for {bundle.Key}: {bundle.Value.Split(Bundles.BundleKeyDelim).First()} bundle.",
				print);
		}

		internal static void Parse(bool isLoadingCustomContent)
		{
			// Fetch initial area-bundle values if not yet set
			if (Bundles.DefaultMaxArea == 0)
			{
				var bundleData = Game1.content.Load
					<Dictionary<string, string>>
					(@"Data/Bundles");

				// Area count is inclusive of Abandoned Joja Mart area to avoid conflicting logic and cases
				Bundles.DefaultMaxArea = bundleData.Keys
					.Select(key => key.Split(Bundles.BundleKeyDelim).First())
					.Distinct()
					.Count() - 1;

				// Bundle count is inclusive of Abandoned Joja Mart bundles, as each requires a unique ID
				Bundles.DefaultMaxBundle = bundleData.Keys
					.Select(key => key.Split(Bundles.BundleKeyDelim).Last())
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
			Random r = new Random((int)Game1.uniqueIDForThisGame * 9); // copied from StardewValley.Game1.GenerateBundles(...)
			if (isLoadingCustomContent)
			{
				Dictionary<string, string> bundleData = new BundleGenerator().Generate(
					bundle_data_path: AssetManager.GameBundleDefinitionsPath,
					rng: r);

				Game1.netWorldState.Value.SetBundleData(bundleData);
			}
			else
			{
				Game1.GenerateBundles(Game1.bundleType);

				if (Context.IsMainPlayer && Bundles.CustomAreaBundleKeys != null)
				{
					var netBundleData = Reflection.GetField
						<NetStringDictionary<string, NetString>>
						(obj: Game1.netWorldState.Value, name: "netBundleData")
						.GetValue();

					IEnumerable<int> bundleNumbers = Bundles.GetAllCustomBundleNumbers();

					foreach (int bundleNumber in bundleNumbers.Where(num => Bundles.IsCustomBundle(num)))
					{
						netBundleData.Remove(netBundleData.Keys
							.FirstOrDefault(b => bundleNumber == int.Parse(b.Split(Bundles.BundleKeyDelim).Last())));
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

			Bundles.Parse(isLoadingCustomContent: true);

			Dictionary<string, string> bundleData = Game1.netWorldState.Value.GetUnlocalizedBundleData();
			IEnumerable<string> areaNames = Bundles.CustomAreaBundleKeys.Keys;
			IDictionary<string, int> bundleNamesAndNumbers = Bundles.GetBundleNamesAndNumbersFromBundleKeys(
				Bundles.CustomAreaBundleKeys.Values
					.SelectMany(s => s));

			Dictionary<string, bool> areasCompleteData = null;
			Dictionary<string, bool> bundleRewardsData = null;

			// Host player loads preserved/persistent data from world:
			if (Context.IsMainPlayer)
			{
				// Deserialise saved mod data:

				if (cc.modData.TryGetValue(Bundles.KeyAreasComplete, out string rawAreasComplete) && !string.IsNullOrWhiteSpace(rawAreasComplete))
				{
					// Deserialise saved area-bundle mod data
					Dictionary<string, bool> savedAreasComplete = rawAreasComplete
					.Split(Bundles.ModDataKeyDelim)
					.ToDictionary(
						keySelector: s => s.Split(Bundles.ModDataValueDelim).First(),
						elementSelector: s => bool.Parse(s.Split(Bundles.ModDataValueDelim).Last()));

					// Read saved area-bundle setups

					areasCompleteData = areaNames
						// Include newly-added area-bundles
						.Where(name => !savedAreasComplete.ContainsKey(name))
						.ToDictionary(name => name, name => false)
						// Include saved area-bundles if their metadata is loaded
						// Saved area-bundles are excluded if metadata is missing
						.Concat(savedAreasComplete.Where(pair => areaNames.Contains(pair.Key)))
						.ToDictionary(pair => pair.Key, pair => pair.Value);

					// Check for saved data with no matching metadata
					IEnumerable<string> excludedAreas = savedAreasComplete.Select(pair => pair.Key).Except(areasCompleteData.Keys);
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
						.Split(Bundles.ModDataKeyDelim)
						.ToDictionary(
							keySelector: s => s.Split(Bundles.ModDataValueDelim).First(),
							elementSelector: s => bool.Parse(s.Split(Bundles.ModDataValueDelim).Last()));

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
				Vector2 tileLocation = Utility.PointToVector2(Bundles.CustomBundleDonationsChestTile);
				if (cc.Objects.TryGetValue(tileLocation, out StardewValley.Object o) && o is Chest chest)
				{
					// Compare items against bundle requirements fields in bundle data
					IEnumerable<string> customBundleDataKeys = Game1.netWorldState.Value.BundleData
						.Where(pair => int.Parse(pair.Key.Split(Bundles.BundleKeyDelim).Last()) > Bundles.DefaultMaxBundle)
						.Select(pair => pair.Key);
					foreach (string bundleKey in customBundleDataKeys)
					{
						const int fields = 3;
						int bundleId = int.Parse(bundleKey.Split(Bundles.BundleKeyDelim).Last());
						string rawBundleInfo = Game1.netWorldState.Value.BundleData[bundleKey];
						string[] split = rawBundleInfo.Split(Bundles.BundleKeyDelim);
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
			if (areasCompleteData == null || !areasCompleteData.Any())
			{
				areasCompleteData = areaNames.ToDictionary(name => name, name => false);
			}
			if (bundleRewardsData == null || !bundleRewardsData.Any())
			{
				bundleRewardsData = bundleNamesAndNumbers.ToDictionary(nameAndNum => nameAndNum.Key, name => false);
			}

			// Set CC state to include custom content:

			// Set areas complete array to reflect saved data length and contents
			if (areasCompleteData.Any())
			{
				foreach (string areaName in areasCompleteData.Keys)
				{
					int areaNumber = Bundles.GetCustomAreaNumberFromName(areaName);
					Bundles.CustomAreasComplete[areaNumber] = areasCompleteData[areaName];
				}
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
					Vector2 tileLocation = Utility.PointToVector2(Bundles.CustomBundleDonationsChestTile);
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
								.FirstOrDefault(pair => int.Parse(pair.Key.Split(Bundles.BundleKeyDelim).Last()) == bundleNumber).Value.Split(Bundles.BundleKeyDelim).First());
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
				if (Bundles.CustomAreaBundleKeys != null)
				{
					Dictionary<string, bool> areasCompleteData =
						Bundles.CustomAreasComplete
						.ToDictionary(
							keySelector: pair => Bundles.GetCustomAreaNameFromNumber(pair.Key),
							elementSelector: pair => pair.Value);
					Dictionary<string, bool> bundleRewardsData =
						Bundles.CustomAreaBundleKeys.Values
						.SelectMany(s => s)
						.ToDictionary(
							keySelector: bundleKey => bundleKey.Split(Bundles.BundleKeyDelim).First(),
							elementSelector: bundleKey => cc.bundleRewards[int.Parse(bundleKey.Split(Bundles.BundleKeyDelim).Last())]);

					string serialisedAreasCompleteData = string.Join(
						Bundles.ModDataKeyDelim.ToString(),
						areasCompleteData.Select(pair => $"{pair.Key}{Bundles.ModDataValueDelim}{pair.Value}"));
					string serialisedBundleRewardsData = string.Join(
						Bundles.ModDataKeyDelim.ToString(),
						bundleRewardsData.Select(pair => $"{pair.Key}{Bundles.ModDataValueDelim}{pair.Value}"));

					cc.modData[Bundles.KeyAreasComplete] = serialisedAreasCompleteData;
					cc.modData[Bundles.KeyBundleRewards] = serialisedBundleRewardsData;
				}
			}

			// Reset world state to exclude custom content
			Bundles.Parse(isLoadingCustomContent: false);
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
				.Select(bundleKey => bundleKey.Split(Bundles.BundleKeyDelim).First())
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
				string areaName = bundleKey.Split(Bundles.BundleKeyDelim).First();
				int areaNumber = areaNamesAndNumbers[areaName];
				int bundleNumber = Convert.ToInt32(bundleKey.Split(Bundles.BundleKeyDelim).Last());

				bundleAreaDict[bundleNumber] = areaNumber;
				if (!areaBundleDict[areaNumber].Contains(bundleNumber))
				{
					areaBundleDict[areaNumber].Add(bundleNumber);
				}
			}
			
			areaBundleDictField.SetValue(areaBundleDict);
			bundleAreaDictField.SetValue(bundleAreaDict);
		}

		public static BundleMetadata GetCustomBundleMetadataFromAreaNumber(int areaNumber)
		{
			string areaName = Bundles.GetCustomAreaNameFromNumber(areaNumber);
			return Bundles.CustomBundleMetadata.FirstOrDefault(bmd => bmd.AreaName == areaName);
		}

		public static int GetCustomAreaNumberFromName(string areaName)
		{
			return Bundles.CustomAreaNamesAndNumbers.TryGetValue(areaName, out int i)
				? i
				: -1;
		}

		public static string GetCustomAreaNameFromNumber(int areaNumber)
		{
			string name = Bundles.CustomAreaNamesAndNumbers.Keys
					.FirstOrDefault(key => Bundles.CustomAreaNamesAndNumbers[key] == areaNumber);
			return name;
		}

		public static string GetAreaNameAsAssetKey(string areaName)
		{
			return string.Join(string.Empty, areaName.Split(' '));
		}

		public static IEnumerable<string> GetAllAreaNames()
		{
			return Game1.netWorldState.Value.BundleData.Keys.Select(s => s.Split(Bundles.BundleKeyDelim).First()).Distinct();
		}

		public static IEnumerable<int> GetAllCustomBundleNumbers()
		{
			return Bundles.CustomAreaNamesAndNumbers.Keys
				.SelectMany(areaName => Bundles.GetBundleNumbersForArea(areaName));
		}

		public static IDictionary<string, int> GetAllBundleNamesAndNumbers()
		{
			return Game1.netWorldState.Value.BundleData.ToDictionary(
				keySelector: pair => pair.Value.Split(Bundles.BundleKeyDelim).First(),
				elementSelector: pair => int.Parse(pair.Key.Split(Bundles.BundleKeyDelim).Last()));
		}

		public static Dictionary<string, int> GetBundleNamesAndNumbersFromBundleKeys(IEnumerable<string> bundleKeys)
		{
			return bundleKeys.ToDictionary(
				keySelector: s => s.Split(Bundles.BundleKeyDelim).First(),
				elementSelector: s => int.Parse(s.Split(Bundles.BundleKeyDelim).Last()));
		}

		public static Dictionary<int, int[]> GetAllCustomAreaNumbersAndBundleNumbers()
		{
			return Bundles.CustomAreaNamesAndNumbers.Keys.ToDictionary(
				areaName => Bundles.GetCustomAreaNumberFromName(areaName),
				areaName => Bundles.GetBundleNumbersForArea(areaName).ToArray());
		}

		public static bool IsCustomArea(int areaNumber)
		{
			return areaNumber >= Bundles.CustomAreaInitialIndex;
		}

		public static bool IsCustomBundle(int bundleNumber)
		{
			return bundleNumber >= Bundles.CustomBundleInitialIndex;
		}

		public static bool IsAreaComplete(CommunityCenter cc, int areaNumber)
		{
			return Bundles.IsDefaultAreaComplete(cc: cc, areaNumber: areaNumber)
				|| (Bundles.IsCustomArea(areaNumber) && Bundles.IsCustomAreaComplete(areaNumber: areaNumber));
		}

		public static bool IsDefaultAreaComplete(CommunityCenter cc, int areaNumber)
		{
			return areaNumber >= 0 && areaNumber < cc.areasComplete.Length && cc.areasComplete[areaNumber];
		}

		public static List<int> GetBundleNumbersForArea(string areaName)
		{
			List<int> bundleNumbers = Bundles.CustomAreaBundleKeys.TryGetValue(areaName, out string[] bundles)
				&& bundles != null && bundles.Length > 0
				? bundles
				.Select(bundle => int.Parse(bundle.Split(Bundles.BundleKeyDelim).Last()))
					.Where(bundleNumber => Game1.netWorldState.Value.Bundles.Keys.Contains(bundleNumber))
					.Distinct()
					.ToList()
				: new List<int>();
			return bundleNumbers;
		}

		public static bool IsCustomAreaComplete(int areaNumber)
		{
			// Check for AreasComplete entry
			bool isAreaComplete = Bundles.CustomAreasComplete.TryGetValue(areaNumber, out bool isComplete) && isComplete;

			// Custom area is also considered complete if it has no bundles loaded
			List<int> bundleNumbers = Bundles.GetBundleNumbersForArea(Bundles.GetCustomAreaNameFromNumber(areaNumber));
			bool isBundleSetComplete = !bundleNumbers.Any()
				|| bundleNumbers.All(bundleNumber => Game1.netWorldState.Value.Bundles[bundleNumber].All(b => b));

			return isAreaComplete || isBundleSetComplete;
		}

		public static int GetNumberOfCustomAreasComplete()
		{
			return Bundles.CustomAreasComplete.Values.Count(isComplete => isComplete);
		}

		public static int GetTotalAreasComplete(CommunityCenter cc)
		{
			return cc.areasComplete.Count(isComplete => isComplete) + Bundles.CustomAreasComplete.Values.Count(isComplete => isComplete);
		}

		public static bool HasOrWillReceiveAreaCompletedMailForAllCustomAreas()
		{
			return Bundles.CustomBundleMetadata
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

		internal static void SetUpJunimosForGoodbyeDance(CommunityCenter cc)
		{
			List<Junimo> junimos = cc.getCharacters().OfType<Junimo>().ToList();
			Vector2 min = new Vector2(junimos.Min(j => j.Position.X), junimos.Min(j => j.Position.Y));
			Vector2 max = new Vector2(junimos.Max(j => j.Position.X), junimos.Max(j => j.Position.Y));
			for (int i = 0; i < Bundles.CustomAreaNamesAndNumbers.Count; ++i)
			{
				Junimo junimo = cc.getJunimoForArea(Bundles.CustomAreaInitialIndex + i);

				int xOffset = i * Game1.tileSize;
				Vector2 position;
				position.X = Game1.random.NextDouble() < 0.5f
					? min.X - Game1.tileSize - xOffset
					: max.X + Game1.tileSize + xOffset;
				position.Y = Game1.random.NextDouble() < 0.5f
					? min.Y
					: max.Y;

				junimo.Position = min + (position * Game1.tileSize);

				// Do as in the target method
				junimo.stayStill();
				junimo.faceDirection(1);
				junimo.fadeBack();
				junimo.IsInvisible = false;
				junimo.setAlpha(1f);
			}
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

		public static void SetCC(CommunityCenter cc)
		{
			Bundles._cc = cc;
		}

		public static void Clear()
		{
			Bundles._cc = null;
			Bundles.CustomAreaBundleKeys.Clear();
			Bundles.CustomAreaNamesAndNumbers.Clear();
			Bundles.CustomAreasComplete.Clear();
			Bundles.CustomAreaInitialIndex = 0;
			Bundles.CustomBundleInitialIndex = 0;
			Bundles.DefaultMaxArea = 0;
			Bundles.DefaultMaxBundle = 0;
		}

		internal static void Print(CommunityCenter cc)
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

				// Custom info
				($"CCC: CCC {nameof(Bundles.CustomBundleMetadata)}[{Bundles.CustomBundleMetadata.Count}]:",
				string.Join(Environment.NewLine,
					Bundles.CustomBundleMetadata.Select(bmd => $"{bmd.AreaName}: {bmd.BundleDisplayNames.Keys}"))),
				($"CCC: CCC {nameof(Bundles.CustomAreaNamesAndNumbers)}[{Bundles.CustomAreaNamesAndNumbers.Count}]:",
				string.Join(Environment.NewLine,
					Bundles.CustomAreaNamesAndNumbers.Select(pair => $"{pair.Key}: {pair.Value}"))),
				($"CCC: CCC {nameof(Bundles.CustomAreasComplete)}[{Bundles.GetNumberOfCustomAreasComplete()}/{Bundles.CustomAreasComplete.Count}]:",
				string.Join(Environment.NewLine,
					Bundles.CustomAreasComplete)),
				($"CCC: CCC {nameof(Bundles.CustomAreaBundleKeys)}[{Bundles.CustomAreaBundleKeys.Count}]:",
				string.Join(Environment.NewLine,
					Bundles.CustomAreaBundleKeys.Select(pair => $"{pair.Key}: {string.Join(" ", pair.Value.Select(i => i))}"))),

				// Area info
				($"CCC: CC {nameof(cc.areasComplete)}[{Reflection.GetMethod(cc, "getNumberOfAreasComplete").Invoke<int>()}/{cc.areasComplete.Count}]:",
				string.Join(Environment.NewLine,
					cc.areasComplete)),
				($"CCC: CC {nameof(bundleAreaDict)}[{bundleAreaDict.Count}]:",
				string.Join(Environment.NewLine,
				bundleAreaDict.Select(pair => $"({pair.Key}: {pair.Value})"))),
				($"CCC: CC {nameof(areaBundleDict)}[{areaBundleDict.Count}]:",
				string.Join(Environment.NewLine,
					areaBundleDict.Select(pair => $"({pair.Key}: {string.Join(" ", pair.Value.Select(i => i))})"))),

				// Bundle info
				($"CCC: GW {nameof(Game1.netWorldState.Value.BundleData)}[{Game1.netWorldState.Value.BundleData.Count}]:",
				string.Join(Environment.NewLine,
					Game1.netWorldState.Value.BundleData.Select(pair => $"{pair.Key}: {pair.Value}"))),
				($"CCC: GW {nameof(Game1.netWorldState.Value.Bundles)}[{Game1.netWorldState.Value.Bundles.Count()}]:",
				string.Join(Environment.NewLine,
					Game1.netWorldState.Value.Bundles.Pairs.Select(pair => $"{pair.Key}: {string.Join(" ", pair.Value)}"))),
				($"CCC: GW {nameof(Game1.netWorldState.Value.BundleRewards)}[{Game1.netWorldState.Value.BundleRewards.Count()}]:",
				string.Join(Environment.NewLine,
					Game1.netWorldState.Value.BundleRewards.Pairs.Select(pair => $"{pair.Key}: {pair.Value}"))),
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
			JunimoNoteMenu junimoNoteMenu = Game1.activeClickableMenu as JunimoNoteMenu;
			// Set JunimoNoteArea field for area currently being displayed
			Reflection.GetField
				<int>
				(junimoNoteMenu, "whichArea")
				.SetValue(ModEntry.Instance._lastJunimoNoteMenuArea.Value);
			// Force menu to refresh and display bundles for this area
			junimoNoteMenu.bundles.Clear();
			junimoNoteMenu.setUpMenu(
				whichArea: ModEntry.Instance._lastJunimoNoteMenuArea.Value,
				bundlesComplete: Bundles.CC.bundlesDict());
		}
	}
}
