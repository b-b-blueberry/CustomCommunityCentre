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
	// TODO: ADD: Prefix all area and bundle names with the mod manifest unique ID when loaded

	public class AssetManager : IAssetLoader, IAssetEditor
	{
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

		// Local content assets
		public static readonly string RootLocalContentPath = "assets";
		public static string LocalBundleDefinitionsPath { get; private set; } = "bundleDefinitions";
		public static string LocalBundleSubstitutesPath { get; private set; } = "bundleSubstitutes";
		public static string LocalBundleMetadataPath { get; private set; } = "bundleMetadata";

		// Internal sneaky asset business
		internal static readonly string BundleDataAssetKey = System.IO.Path.Combine(RootGameContentPath, "BundleDataKey");

		// Asset lists
		public readonly List<string> ModAssetKeys = new List<string>();
		public readonly List<string> AssetsToEdit = new List<string>
		{
			@"Data/Events/Town",
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
					this.ModAssetKeys.Add(path);
				}
			}
			this.AssetsToEdit.AddRange(this.ModAssetKeys);

			Log.D($"Custom assets use root asset key:{Environment.NewLine}\"Target\": \"{AssetManager.RootGameContentPath}\"",
				ModEntry.Config.DebugMode);
			Log.D($"Custom assets keys:{Environment.NewLine}{string.Join(Environment.NewLine, this.ModAssetKeys.Select(s => $"\"{s}\""))}",
				ModEntry.Config.DebugMode);
		}

		public bool CanLoad<T>(IAssetInfo asset)
		{
			return this.ModAssetKeys.Any(assetName => asset.AssetNameEquals(assetName))
				|| asset.AssetNameEquals(CustomCommunityCentre.AssetManager.BundleDataAssetKey);
		}

		public T Load<T>(IAssetInfo asset)
		{
			// Load mod assets

			if (asset.AssetNameEquals(CustomCommunityCentre.AssetManager.GameBundleDefinitionsPath))
			{
				return (T)(object)new List<StardewValley.GameData.RandomBundleData>();
			}
			if (asset.AssetNameEquals(CustomCommunityCentre.AssetManager.GameBundleSubstitutesPath))
			{
				return (T)(object)new Dictionary<string, List<CustomCommunityCentre.Data.SubstituteBundleData>>();
			}
			if (asset.AssetNameEquals(CustomCommunityCentre.AssetManager.GameBundleMetadataPath))
			{
				return (T)(object)new List<CustomCommunityCentre.Data.BundleMetadata>();
			}

			// Internal sneaky asset business

			if (asset.AssetNameEquals(CustomCommunityCentre.AssetManager.BundleDataAssetKey))
			{
				return (T)(object)BundleManager.Parse(data: Bundles.BundleData);
			}

			return (T)(object)null;
		}

		public bool CanEdit<T>(IAssetInfo asset)
		{
			return this.AssetsToEdit.Any(assetName => asset.AssetNameEquals(assetName))
				|| this.ModAssetKeys.Any(assetName => asset.AssetNameEquals(assetName));
		}

		public void Edit<T>(IAssetData asset)
		{
			this.Edit(asset: ref asset); // eat that, ENC0036
		}

		public void Edit(ref IAssetData asset)
		{
			if (asset.DataType == typeof(Texture2D) && asset.AsImage().Data.IsDisposed)
				return;

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

			if (asset.AssetNameEquals(@"Strings/BundleNames"))
			{
				var data = asset.AsDictionary<string, string>().Data;

				// Add bundle display names to localised bundle names dictionary
				foreach (CustomCommunityCentre.Data.BundleMetadata bundleMetadata in Bundles.CustomBundleMetadata)
				{
					foreach (string bundleName in bundleMetadata.BundleDisplayNames.Keys)
					{
						data[bundleMetadata.AreaName] = CustomCommunityCentre.Data.BundleMetadata.GetLocalisedString(
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
				foreach (CustomCommunityCentre.Data.BundleMetadata bundleMetadata in Bundles.CustomBundleMetadata)
				{
					string areaNameAsAssetKey = Bundles.GetAreaNameAsAssetKey(bundleMetadata.AreaName);
					data[$"CommunityCenter_AreaName_{areaNameAsAssetKey}"] = bundleMetadata.AreaDisplayName
						.TryGetValue(LocalizedContentManager.CurrentLanguageCode.ToString(), out string str)
						? str
						: bundleMetadata.AreaName;

					str = CustomCommunityCentre.Data.BundleMetadata.GetLocalisedString(dict: bundleMetadata.AreaCompleteDialogue, defaultValue: string.Empty);
					data[$"CommunityCenter_AreaCompletion_{areaNameAsAssetKey}"] = str;
				}

				asset.ReplaceWith(data);
				return;
			}

			if (asset.AssetNameEquals(@"Strings/UI"))
			{
				var data = asset.AsDictionary<string, string>().Data;

				// Add reward text
				foreach (CustomCommunityCentre.Data.BundleMetadata bundleMetadata in Bundles.CustomBundleMetadata)
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
				<List<CustomCommunityCentre.Data.BundleMetadata>>
				(AssetManager.GameBundleMetadataPath);

			Bundles.BundleData = Game1.content.Load
				<List<RandomBundleData>>
				(CustomCommunityCentre.AssetManager.GameBundleDefinitionsPath);
		}

		public static string PrefixAsset(string asset, string prefix = null)
		{
			return string.Join(".", prefix ?? AssetManager.AssetPrefix, asset);
		}
	}
}
