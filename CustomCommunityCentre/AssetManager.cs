using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.GameData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CustomCommunityCentre
{
	// TODO: Prefix all area and bundle names with the mod manifest unique ID

	public class AssetManager : IAssetLoader, IAssetEditor
	{
		private ITranslationHelper i18n => ModEntry.Instance.Helper.Translation;

		// Assets
		public static string AssetPrefix => ModEntry.Instance.ModManifest.UniqueID;
		public static readonly char[] ForbiddenAssetNameCharacters = new char[]
		{
			System.IO.Path.DirectorySeparatorChar,
			Bundles.ModDataKeyDelim,
			Bundles.ModDataValueDelim
		};

		// Game content assets
		public static readonly string RootGameContentPath = PathUtilities.NormalizeAssetName(
			$@"Mods/{ModEntry.Instance.ModManifest.UniqueID}.Assets");
		public static string GameBundleDefinitionsPath { get; private set; } = "BundleDefinitions";
		public static string GameBundleSubstitutesPath { get; private set; } = "BundleSubstitutes";
		public static string GameBundleMetadataPath { get; private set; } = "BundleMetadata";
		public static string GameBundleSpritesPath { get; private set; } = "BundleSprites";

		// Local content assets
		public static readonly string RootLocalContentPath = "assets";
		public static string LocalBundleDefinitionsPath { get; private set; } = "bundleDefinitions";
		public static string LocalBundleSubstitutesPath { get; private set; } = "bundleSubstitutes";
		public static string LocalBundleMetadataPath { get; private set; } = "bundleMetadata";
		public static string LocalBundleSpritesPath { get; private set; } = "bundleSprites";

		// Asset lists
		private readonly List<string> _assetsToLoad = new List<string>();
		private readonly List<string> _assetsToEdit = new List<string>
		{
			@"Data/Events/Town",
			@"Maps/townInterior",
			@"Strings/BundleNames",
			@"Strings/Locations",
			@"Strings/UI",
		};


		public AssetManager()
		{
			IEnumerable<System.Reflection.PropertyInfo> properties = this
				.GetType()
				.GetProperties()
				.Where(p => p.Name.EndsWith("Path"));
			foreach (System.Reflection.PropertyInfo property in properties)
			{
				string path = System.IO.Path.Combine(
						property.Name.StartsWith("Game")
							? AssetManager.RootGameContentPath
							: AssetManager.RootLocalContentPath,
						property.GetValue(obj: this) as string);
				property.SetValue(
					obj: this,
					value: path);
				if (property.Name.StartsWith("Game"))
				{
					path = property.GetValue(obj: this) as string;
					this._assetsToLoad.Add(path);
				}
			}
			this._assetsToEdit.AddRange(this._assetsToLoad);

			Log.D($"Custom assets use asset key:{Environment.NewLine}\"Target\": \"{AssetManager.RootGameContentPath}\"",
				ModEntry.Config.DebugMode);
		}

		public bool CanLoad<T>(IAssetInfo asset)
		{
			return this._assetsToLoad.Any(assetName => asset.AssetNameEquals(assetName));
		}

		public bool CanEdit<T>(IAssetInfo asset)
		{
			return this._assetsToEdit.Any(assetName => asset.AssetNameEquals(assetName))
				|| this._assetsToLoad.Any(assetName => asset.AssetNameEquals(assetName));
		}

		public T Load<T>(IAssetInfo asset)
		{
			if (asset.AssetNameEquals(AssetManager.GameBundleDefinitionsPath))
			{
				return (T)(object)ModEntry.Instance.Helper.Content.Load
					<List<RandomBundleData>>
					($"{AssetManager.LocalBundleDefinitionsPath}.json");
			}
			if (asset.AssetNameEquals(AssetManager.GameBundleSubstitutesPath))
			{
				return (T)(object)ModEntry.Instance.Helper.Content.Load
					<Dictionary<string, List<SubstituteBundleData>>>
					($"{AssetManager.LocalBundleSubstitutesPath}.json");
			}
			if (asset.AssetNameEquals(AssetManager.GameBundleMetadataPath))
			{
				return (T)(object)ModEntry.Instance.Helper.Content.Load
					<List<BundleMetadata>>
					($"{AssetManager.LocalBundleMetadataPath}.json");
			}
			if (asset.AssetNameEquals(AssetManager.GameBundleSpritesPath))
			{
				return (T)(object)ModEntry.Instance.Helper.Content.Load
					<Texture2D>
					($"{AssetManager.LocalBundleSpritesPath}.png");
			}
			return (T)(object)null;
		}

		public void Edit<T>(IAssetData asset)
		{
			this.Edit(asset: ref asset); // eat that, ENC0036
		}

		public void Edit(ref IAssetData asset)
		{
			if (asset.DataType == typeof(Texture2D) && asset.AsImage().Data.IsDisposed)
				return;

			if (asset.AssetNameEquals(AssetManager.GameBundleDefinitionsPath))
			{
				var data = asset.GetData
					<List<RandomBundleData>>();
				var bundleSubstitutes = Game1.content.Load
					<Dictionary<string, List<SubstituteBundleData>>>
					(AssetManager.GameBundleSubstitutesPath);
				var randomBundleData = Game1.content.Load
					<List<RandomBundleData>>
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
					RandomBundleData area = data[i];

					// Validate areas
					if (area.AreaName == null)
					{
						errorMessage.AppendLine($"Custom area {i + 1} was not loaded: {nameof(BundleMetadata.AreaName)} was null.");
						data.RemoveAt(i);
						continue;
					}
					IEnumerable<char> badChars = area.AreaName
						.Where(c => AssetManager.ForbiddenAssetNameCharacters.Contains(c))
						.Distinct();
					if (badChars.Any())
					{
						errorMessage.AppendLine($"Custom area '{area.AreaName}' was not loaded: Area {nameof(BundleMetadata.AreaName)} contains forbidden characters '{string.Join(", ", badChars)}'");
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
						SubstituteBundleData bundleSubstitute = bundleSubstitutes[uniqueId][i];

						// Validate bundle substitutes
						if (bundleSubstitute.BundleName == null)
						{
							errorMessage.AppendLine($"Bundle substitute for '{uniqueId}' was not loaded: {nameof(SubstituteBundleData.BundleName)} was null.");
							bundleSubstitutes[uniqueId].RemoveAt(i);
							continue;
						}
						IEnumerable<char> badChars = bundleSubstitute.BundleName.Where(c => AssetManager.ForbiddenAssetNameCharacters.Contains(c)).Distinct();
						if (badChars.Any())
						{
							errorMessage.AppendLine($"Bundle substitute '{bundleSubstitute.BundleName}' was not loaded: {nameof(SubstituteBundleData.BundleName)} contains forbidden characters '{string.Join(", ", badChars)}'");
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

				asset.ReplaceWith(data);
				return;
			}

			if (asset.AssetNameEquals(AssetManager.GameBundleSubstitutesPath))
			{
				var data = asset.AsDictionary<string, List<SubstituteBundleData>>().Data;

				// . . .

				return;
			}

			if (asset.AssetNameEquals(@"Data/Events/Town"))
			{
				var data = asset.AsDictionary<string, string>().Data;

				// Append completed mail received for all custom areas as required flags for CC completion event

				const char delimiter = '/';
				const string mailFlag = "Hn";

				string eventId = ((int)Bundles.EventIds.CommunityCentreComplete).ToString();
				string eventKey = data.Keys.FirstOrDefault(key => key.Split(delimiter).First() == eventId);
				string eventScript = data[eventKey];
				string[] mailFlags = new List<string> { eventKey }
					.Concat(Bundles.CustomAreaNamesAndNumbers.Keys
						.Select(areaName => $"{mailFlag} {string.Format(Bundles.MailAreaCompleted, Bundles.GetAreaNameAsAssetKey(areaName))}"))
					.ToArray();

				data.Remove(eventKey);
				eventKey = string.Join(delimiter.ToString(), mailFlags);
				data[eventKey] = eventScript;

				asset.ReplaceWith(data);
				return;
			}

			if (asset.AssetNameEquals(@"Maps/townInterior"))
			{
				if (!(Game1.currentLocation is StardewValley.Locations.CommunityCenter))
					return;

				var image = asset.AsImage();

				// Openable fridge in the kitchen
				Rectangle targetArea = Kitchen.FridgeOpenedSpriteArea; // Target some unused area of the sheet for this location
				Rectangle sourceArea = new Rectangle(320, 224, targetArea.Width, targetArea.Height); // Apply base fridge sprite
				image.PatchImage(
					source: image.Data,
					sourceArea: sourceArea,
					targetArea: targetArea,
					patchMode: PatchMode.Replace);

				sourceArea = new Rectangle(0, 192, 16, 32); // Patch in opened-door fridge sprite from mouseCursors sheet
				image.PatchImage(
					source: Game1.mouseCursors2,
					sourceArea: sourceArea,
					targetArea: targetArea,
					patchMode: PatchMode.Overlay);

				// New star on the community centre area tracker wall
				sourceArea = new Rectangle(370, 705, 7, 7);
				targetArea = new Rectangle(380, 710, sourceArea.Width, sourceArea.Height);
				image.PatchImage(
					source: image.Data,
					sourceArea: sourceArea,
					targetArea: targetArea,
					patchMode: PatchMode.Replace);

				asset.ReplaceWith(image.Data);
				return;
			}

			if (asset.AssetNameEquals(@"Strings/BundleNames"))
			{
				var data = asset.AsDictionary<string, string>().Data;

				foreach (BundleMetadata bundleMetadata in Bundles.CustomBundleMetadata)
				{
					foreach (string bundleName in bundleMetadata.BundleDisplayNames.Keys)
					{
						data[bundleMetadata.AreaName] = BundleMetadata.GetLocalisedString(
							dict: bundleMetadata.BundleDisplayNames[bundleName],
							defaultValue: bundleName);
					}
				}

				asset.ReplaceWith(data);
				return;
			}

			if (asset.AssetNameEquals(@"Strings/Locations"))
			{
				var data = asset.AsDictionary<string, string>().Data;

				// Add area display names and completion strings
				foreach (BundleMetadata bundleMetadata in Bundles.CustomBundleMetadata)
				{
					string areaNameAsAssetKey = Bundles.GetAreaNameAsAssetKey(bundleMetadata.AreaName);
					data[$"CommunityCenter_AreaName_{areaNameAsAssetKey}"] = bundleMetadata.AreaDisplayName
						.TryGetValue(LocalizedContentManager.CurrentLanguageCode.ToString(), out string str)
						? str
						: bundleMetadata.AreaName;

					str = BundleMetadata.GetLocalisedString(dict: bundleMetadata.AreaCompleteDialogue, defaultValue: string.Empty);
					data[$"CommunityCenter_AreaCompletion_{areaNameAsAssetKey}"] = str;
				}

				asset.ReplaceWith(data);
				return;
			}

			if (asset.AssetNameEquals(@"Strings/UI"))
			{
				var data = asset.AsDictionary<string, string>().Data;

				// Add reward text
				foreach (BundleMetadata bundleMetadata in Bundles.CustomBundleMetadata)
				{
					int areaNumber = Bundles.GetCustomAreaNumberFromName(bundleMetadata.AreaName);
					data[$"JunimoNote_Reward{areaNumber}"] = bundleMetadata.AreaCompleteDialogue
						.TryGetValue(LocalizedContentManager.CurrentLanguageCode.ToString(), out string str)
						? str
						: string.Empty;
				}

				asset.ReplaceWith(data);
				return;
			}
		}

		public static void ReloadAssets(IModHelper helper)
		{
			helper.Content.InvalidateCache(@"Strings/UI");

			Bundles.CustomBundleMetadata = Game1.content.Load
				<List<BundleMetadata>>
				(AssetManager.GameBundleMetadataPath);
		}

		public static string PrefixAsset(string asset, string prefix = null)
		{
			return string.Join(".", prefix ?? AssetManager.AssetPrefix, asset);
		}
	}
}
