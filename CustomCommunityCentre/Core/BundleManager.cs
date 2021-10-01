using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CustomCommunityCentre
{
	public static class BundleManager
	{
		private static IModHelper Helper => ModEntry.Instance.Helper;
		private static IMonitor Monitor => ModEntry.Instance.Monitor;
		private static IReflectionHelper Reflection => ModEntry.Instance.Helper.Reflection;
		private static Config Config => ModEntry.Config;


		internal static void RegisterEvents()
		{
			Helper.Events.GameLoop.ReturnedToTitle += BundleManager.GameLoop_ReturnedToTitle;
			Helper.Events.GameLoop.DayEnding += BundleManager.GameLoop_DayEnding;
		}

		private static void GameLoop_ReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
		{
			// Local co-op farmhands should not clear custom area-bundle data
			if (!Context.IsSplitScreen || Context.IsMainPlayer)
			{
				BundleManager.Clear();
			}
		}

		private static void GameLoop_DayEnding(object sender, DayEndingEventArgs e)
		{
			// Save local (and/or persistent) community centre data
			Log.D("Unloading world bundle data at end of day.",
				ModEntry.Config.DebugMode);
			BundleManager.Save(Bundles.CC);
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
				BundleManager.Save(cc);
			}
			else
			{
				Log.D("Community centre incomplete, loading bundle data.",
					ModEntry.Config.DebugMode);
				BundleManager.Load(cc);
			}
		}

		internal static void Clear()
		{
			Bundles.SetCC(cc: null);
			Bundles.CustomAreaBundleKeys.Clear();
			Bundles.CustomAreaNamesAndNumbers.Clear();
			Bundles.CustomAreasComplete.Clear();
			Bundles.CustomAreaInitialIndex = 0;
			Bundles.CustomBundleInitialIndex = 0;
			Bundles.DefaultMaxArea = 0;
			Bundles.DefaultMaxBundle = 0;
		}

		internal static void Generate(bool isLoadingCustomContent)
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
				// The game will usually consider area 7 as the bulletin board extras, and area 8 as the Junimo Hut, so skip those too
				Bundles.CustomAreaInitialIndex = 9;
			}

			// Reassign world state with or without custom values
			Random r = new Random((int)Game1.uniqueIDForThisGame * 9); // copied from StardewValley.Game1.GenerateBundles(...)
			if (isLoadingCustomContent)
			{
				Dictionary<string, string> bundleData = new BundleGenerator().Generate(
					bundle_data_path: AssetManager.BundleDataAssetKey, // Internal sneaky asset business
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

		internal static List<StardewValley.GameData.RandomBundleData> Parse(List<StardewValley.GameData.RandomBundleData> data)
		{
			var bundleSubstitutes = Game1.content.Load
				<Dictionary<string, List<CustomCommunityCentre.Data.SubstituteBundleData>>>
				(AssetManager.GameBundleSubstitutesPath);
			var randomBundleData = Game1.content.Load
				<List<StardewValley.GameData.RandomBundleData>>
				(@"Data/RandomBundles");
			var defaultBundleData = Game1.content.LoadBase
				<Dictionary<string, string>>
				(@"Data/Bundles");

			StringBuilder errorMessage = new StringBuilder();

			// Reset custom area-bundle dictionaries
			Bundles.CustomAreaNamesAndNumbers.Clear();
			Bundles.CustomAreaBundleKeys.Clear();
			Bundles.CustomAreasComplete.Clear();

			int areaSum = Bundles.CustomAreaInitialIndex;
			int bundleSum = Bundles.CustomBundleInitialIndex;

			for (int i = data.Count - 1; i >= 0; --i)
			{
				StardewValley.GameData.RandomBundleData area = data[i];

				// Validate areas
				if (area.AreaName == null)
				{
					errorMessage.AppendLine($"Custom area {i + 1} was not loaded: {nameof(CustomCommunityCentre.Data.BundleMetadata.AreaName)} was null.");
					data.RemoveAt(i);
					continue;
				}
				IEnumerable<char> badChars = area.AreaName
					.Where(c => AssetManager.ForbiddenAssetNameCharacters.Contains(c))
					.Distinct();
				if (badChars.Any())
				{
					errorMessage.AppendLine($"Custom area '{area.AreaName}' was not loaded: Area {nameof(CustomCommunityCentre.Data.BundleMetadata.AreaName)} contains forbidden characters '{string.Join(", ", badChars)}'");
					data.RemoveAt(i);
					continue;
				}

				// Validate bundles
				List<BundleData> bundles = area.BundleSets
				   // Default bundles
				   .SelectMany(bsd => bsd.Bundles)
				   // Random bundles
				   .Concat(area.Bundles)
				   .ToList();
				if (bundles.Any(bundle => bundle?.Name == null))
				{
					errorMessage.AppendLine($"Custom area {i + 1} was not loaded: Bundle or {nameof(BundleData.Name)} was null.");
					data.RemoveAt(i);
					continue;
				}
				badChars = bundles
					.SelectMany(b => b.Name.Where(c => AssetManager.ForbiddenAssetNameCharacters.Contains(c)))
					.Distinct();
				if (badChars.Any())
				{
					IEnumerable<string> badNames = bundles
						.Where(b => b.Name.Any(c => AssetManager.ForbiddenAssetNameCharacters.Contains(c)))
						.Select(b => b.Name);
					errorMessage.AppendLine($"Custom area '{area.AreaName}' was not loaded: Bundle '{string.Join(",", badNames)}' contains forbidden characters '{string.Join(", ", badChars)}'");
					data.RemoveAt(i);
					continue;
				}

				// Set unique area-bundle index keys
				Dictionary<string, int> bundleNamesAndNumbers = bundles.ToDictionary(
					keySelector: bundle => bundle.Name,
					elementSelector: bundle => bundleSum + bundle.Index);

				string[] bundleKeys = bundleNamesAndNumbers
					.Select(pair => $"{pair.Key}{Bundles.BundleKeyDelim}{pair.Value}")
					.ToArray();

				int[] bundleNumbers = bundleNamesAndNumbers.Values.Distinct().ToArray();

				area.Keys = string.Join(" ", bundleNumbers);

				// Set custom area-bundle dictionary values
				Bundles.CustomAreaBundleKeys[area.AreaName] = bundleKeys;
				Bundles.CustomAreasComplete[areaSum] = false;
				Bundles.CustomAreaNamesAndNumbers[area.AreaName] = areaSum;

				bundleSum += bundleNumbers.Length;
				areaSum++;
			}

			// Replace bundle entries with substitute values
			foreach (string uniqueId in bundleSubstitutes.Keys)
			{
				if (!ModEntry.Instance.Helper.ModRegistry.IsLoaded(uniqueID: uniqueId))
				{
					Log.D($"Skipping substitute bundles from {uniqueId}: Not loaded.",
						ModEntry.Config.DebugMode);
					continue;
				}

				Log.D($"Loading substitute bundles from {uniqueId}...",
					ModEntry.Config.DebugMode);

				for (int i = bundleSubstitutes[uniqueId].Count - 1; i >= 0; --i)
				{
					CustomCommunityCentre.Data.SubstituteBundleData bundleSubstitute = bundleSubstitutes[uniqueId][i];

					// Validate bundle substitutes
					if (bundleSubstitute.BundleName == null)
					{
						errorMessage.AppendLine($"Bundle substitute for '{uniqueId}' was not loaded: {nameof(CustomCommunityCentre.Data.SubstituteBundleData.BundleName)} was null.");
						bundleSubstitutes[uniqueId].RemoveAt(i);
						continue;
					}
					IEnumerable<char> badChars = bundleSubstitute.BundleName.Where(c => AssetManager.ForbiddenAssetNameCharacters.Contains(c)).Distinct();
					if (badChars.Any())
					{
						errorMessage.AppendLine($"Bundle substitute '{bundleSubstitute.BundleName}' was not loaded: {nameof(CustomCommunityCentre.Data.SubstituteBundleData.BundleName)} contains forbidden characters '{string.Join(", ", badChars)}'");
						bundleSubstitutes[uniqueId].RemoveAt(i);
						continue;
					}

					// Find matching bundle for substitute bundle
					IEnumerable<BundleData> bundles = data
						// Default bundles
						.SelectMany(rbd => rbd.BundleSets.SelectMany(bd => bd.Bundles))
						// Random bundles
						.Concat(data.SelectMany(rbd => rbd.Bundles));
					BundleData bundle = bundles
						.FirstOrDefault(bd => bd.Name == bundleSubstitute.BundleName);

					if (bundle == null)
					{
						Log.D($"Skipping substitute bundle {bundleSubstitute.BundleName}: Match not found.",
							ModEntry.Config.DebugMode);
						continue;
					}

					Log.D($"Substituting bundle: {bundleSubstitute.BundleName}...",
						ModEntry.Config.DebugMode);

					// Substitute entries in bundle with provided values
					if (!string.IsNullOrWhiteSpace(bundleSubstitute.Items))
						bundle.Items = bundleSubstitute.Items;
					if (bundleSubstitute.Pick.HasValue)
						bundle.Pick = bundleSubstitute.Pick.Value;
					if (bundleSubstitute.RequiredItems.HasValue)
						bundle.RequiredItems = bundleSubstitute.RequiredItems.Value;
					if (!string.IsNullOrWhiteSpace(bundleSubstitute.Reward))
						bundle.Reward = bundleSubstitute.Reward;
				}
			}

			if (errorMessage.Length > 0)
			{
				Log.E(errorMessage.ToString());
			}

			// Append custom bundle data to default random bundle data after modifications
			data = randomBundleData.Union(data).ToList();

			return data;
		}

		internal static void Load(CommunityCenter cc)
		{
			if (Bundles.IsCommunityCentreComplete(cc))
				return;

			// Set world state to include custom content:

			BundleManager.Generate(isLoadingCustomContent: true);

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

			BundleManager.ReplaceAreaBundleConversions(cc: cc);
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

		internal static void Save(CommunityCenter cc)
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

				if (Bundles.CustomAreaBundleKeys != null)
				{
					// Serialise mod data to be saved

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

					// Unload bundle data from world state
					/*foreach (string bundleKey in Bundles.CustomAreaBundleKeys.Values.SelectMany(b => b))
					{
						Game1.netWorldState.Value.BundleData.Remove(bundleKey);
					}*/
				}
			}

			// Reset world state to exclude custom content
			BundleManager.Generate(isLoadingCustomContent: false);
			BundleManager.ReplaceAreaBundleConversions(cc: cc);
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
					elementSelector: areaName => CommunityCenter.getAreaNumberFromName(areaName) is int i && i >= 0 ? i : Bundles.GetCustomAreaNumberFromName(areaName));

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
	}
}
