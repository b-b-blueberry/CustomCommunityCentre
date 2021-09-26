using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.GameData;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomCommunityCentre
{
	public partial class AssetManager : IAssetLoader, IAssetEditor
	{
		private ITranslationHelper i18n => ModEntry.Instance.Helper.Translation;

		// Assets
		public static string AssetPrefix => ModEntry.Instance.ModManifest.UniqueID;

		// Game content assets
		public static readonly string RootGameContentPath = PathUtilities.NormalizeAssetName(
			$@"Mods/{ModEntry.Instance.ModManifest.Author}.{ModEntry.Instance.ModManifest.UniqueID}.Assets");
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
			@"LooseSprites/JunimoNote",
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

				// Set up the custom area bundle dictionary
				Bundles.CustomAreaNameAndNumberDictionary = new Dictionary<string, int>();
				Bundles.CustomAreaBundleDictionary = new Dictionary<string, string[]>();

				// Populate "Keys" fields dynamically
				for (int i = 0; i < data.Count; ++i)
				{
					RandomBundleData area = data[i];
					List<BundleData> bundles = area.BundleSets
						// Default bundles
						.SelectMany(bsd => bsd.Bundles)
						// Random bundles
						.Concat(area.Bundles)
						.ToList();
					List<int> indexes = bundles
						.Select(bd => bd.Index)
						.Distinct()
						.ToList();
					area.Keys = string.Join(" ", indexes);

					// Add area and bundles to the custom area-bundle dictionaries
					Bundles.CustomAreaNameAndNumberDictionary.Add(
						key: area.AreaName,
						value: i);
					Bundles.CustomAreaBundleDictionary.Add(
						key: area.AreaName,
						value: bundles
							.Select(bd => $"{bd.Name}/{bd.Index}")
							.ToArray());

					// At this stage the bundle indexes aren't respective of the base game bundles,
					// nor necessarily of one another.
					// We'll iron this out in the Bundles.Parse method.
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

					foreach (SubstituteBundleData bundleSubstitute in bundleSubstitutes[uniqueId])
					{
						// Find matching bundle for substitute bundle
						IEnumerable<BundleData> bundles = data
							// Default bundles
							.SelectMany(rbd => rbd.BundleSets.SelectMany(bd => bd.Bundles))
							// Random bundles
							.Concat(data.SelectMany(rbd => rbd.Bundles));
						BundleData bundle = bundles
							.FirstOrDefault(bd => bd.Name == bundleSubstitute.Name);

						if (bundle == null)
						{
							Log.D($"Skipping substitute bundle {bundleSubstitute.Name}: Match not found.",
								ModEntry.Config.DebugMode);
							continue;
						}

						Log.D($"Substituting bundle: {bundleSubstitute.Name}...",
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

				asset.ReplaceWith(data);
				return;
			}

			if (asset.AssetNameEquals(AssetManager.GameBundleSubstitutesPath))
			{
				var data = asset.AsDictionary<string, List<SubstituteBundleData>>();

				// . . .

				return;
			}

			if (asset.AssetNameEquals(@"LooseSprites/JunimoNote"))
			{
				var destImage = asset.AsImage();

				Rectangle sourceArea = new Rectangle(0, 0, 32 * 3, 32);
				Rectangle targetArea = new Rectangle(544, 212, 32 * 3, 32);
				Texture2D source = Game1.content.Load
					<Texture2D>
					(AssetManager.GameBundleSpritesPath);
				destImage.PatchImage(
					source: source,
					sourceArea: sourceArea,
					targetArea: targetArea,
					patchMode: PatchMode.Replace);

				asset.ReplaceWith(destImage.Data);
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

				foreach (BundleMetadata bundleMetadata in Bundles.BundleMetadata)
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

				// Add area names
				foreach (BundleMetadata bundleMetadata in Bundles.BundleMetadata)
				{
					data[$"CommunityCenter_AreaName_{bundleMetadata.AreaName}"] = bundleMetadata.AreaDisplayNames
						.TryGetValue(LocalizedContentManager.CurrentLanguageCode.ToString(), out string str)
						? str
						: bundleMetadata.AreaName;

					// Insert a new AreaCompletion line to account for our extra area
					const int newJunimoLineNumber = 3;
					for (int i = Bundles.CustomAreaNameAndNumberDictionary[bundleMetadata.AreaName] + 1; i > newJunimoLineNumber; --i)
					{
						string below = data[$"CommunityCenter_AreaCompletion{i - 1}"];
						data[$"CommunityCenter_AreaCompletion{i}"] = below;
					}
					str = BundleMetadata.GetLocalisedString(dict: bundleMetadata.AreaCompletionMessage, defaultValue: string.Empty);
					data[$"CommunityCenter_AreaCompletion{newJunimoLineNumber}"] = str;
				}

				asset.ReplaceWith(data);
				return;
			}

			if (asset.AssetNameEquals(@"Strings/UI"))
			{
				var data = asset.AsDictionary<string, string>().Data;

				// Add reward text
				foreach (BundleMetadata bundleMetadata in Bundles.BundleMetadata)
				{
					data[$"JunimoNote_Reward{bundleMetadata.AreaName}"] = bundleMetadata.AreaCompletionMessage
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

			Bundles.BundleMetadata = Game1.content.Load
				<List<BundleMetadata>>
				(AssetManager.GameBundleMetadataPath);
		}

		public static string PrefixAsset(string asset, string prefix = null)
		{
			return string.Join(".", prefix ?? AssetManager.AssetPrefix, asset);
		}
	}
}
