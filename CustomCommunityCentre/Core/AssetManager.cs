using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CustomCommunityCentre
{
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
		public const string RequiredAssetNamePrefix = "Custom";
		public const char RequiredAssetNameDivider = '_';

		// Game content assets
		public static readonly string RootAssetKey = PathUtilities.NormalizeAssetName(
			$@"Mods/{ModEntry.Instance.ModManifest.UniqueID}.Assets");
		public static string BundleMetadataAssetKey { get; private set; } = "BundleMetadata";
		public static string BundleDefinitionsAssetKey { get; private set; } = "BundleDefinitions";
		public static string BundleSubstitutesAssetKey { get; private set; } = "BundleSubstitutes";

		// Internal sneaky asset business
		internal static string BundleDataAssetKey { get; private set; } = "BundleDataInternal";

		// Asset dictionary keys
		public const string BundleMetadataKey = "Metadata";
		public const string BundleDefinitionsKey = "Definitions";
		public const string BundleSubstitutesKey = "Substitutes";

		// Asset lists
		public readonly List<string> ModAssetKeys = new();
		public readonly List<string> GameAssetKeys = new()
        {
			@"Data/Events/Town",
			@"Strings/BundleNames",
			@"Strings/Locations",
			@"Strings/UI",
		};


		public AssetManager()
		{
			IEnumerable<PropertyInfo> properties = this
				.GetType()
				.GetProperties(bindingAttr: BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
				.Where(p => p.Name.EndsWith("AssetKey"))
				.ToList();
			foreach (PropertyInfo property in properties)
			{
				string path = System.IO.Path.Combine(
					AssetManager.RootAssetKey,
					(string)property.GetValue(obj: this));
				property.SetValue(
					obj: this,
					value: path);
				this.ModAssetKeys.Add(path);
			}

			Log.D($"Custom assets use root asset key:{Environment.NewLine}\"{AssetManager.RootAssetKey}\"",
				ModEntry.Config.DebugMode);
			Log.D($"Custom asset keys:{Environment.NewLine}{string.Join(Environment.NewLine, this.ModAssetKeys.Take(this.ModAssetKeys.Count - 1).Select(s => $"\"Target\": \"{s}\""))}",
				ModEntry.Config.DebugMode);
		}

		public bool CanLoad<T>(IAssetInfo asset)
		{
			return this.ModAssetKeys.Any(assetName => asset.AssetNameEquals(assetName));
		}

		public T Load<T>(IAssetInfo asset)
		{
			// Load mod assets

			if (asset.AssetNameEquals(CustomCommunityCentre.AssetManager.BundleMetadataAssetKey))
			{
				return (T)(object)new Dictionary<string, Dictionary<string, List<CustomCommunityCentre.Data.BundleMetadata>>>
				{
					{
						CustomCommunityCentre.AssetManager.BundleMetadataKey,
						new Dictionary<string, List<CustomCommunityCentre.Data.BundleMetadata>>()
					}
				};
			}
			if (asset.AssetNameEquals(CustomCommunityCentre.AssetManager.BundleDefinitionsAssetKey))
			{
				return (T)(object)new Dictionary<string, Dictionary<string, List<StardewValley.GameData.RandomBundleData>>>
				{
					{
						CustomCommunityCentre.AssetManager.BundleDefinitionsKey,
						new Dictionary<string, List<StardewValley.GameData.RandomBundleData>>()
					}
				};
			}
			if (asset.AssetNameEquals(CustomCommunityCentre.AssetManager.BundleSubstitutesAssetKey))
			{
				return (T)(object)new Dictionary<string, Dictionary<string, Dictionary<string, List<CustomCommunityCentre.Data.SubstituteBundleData>>>>
				{
					{ 
						CustomCommunityCentre.AssetManager.BundleSubstitutesKey,
						new Dictionary<string, Dictionary<string, List<CustomCommunityCentre.Data.SubstituteBundleData>>>()
					}
				};
			}

			// Internal sneaky asset business

			if (asset.AssetNameEquals(CustomCommunityCentre.AssetManager.BundleDataAssetKey))
			{
				return (T)(object)BundleManager.Parse(bundleDefinitions: Bundles.BundleData);
			}

			return (T)(object)null;
		}

		public bool CanEdit<T>(IAssetInfo asset)
		{
			return //this.ModAssetKeys.Any(assetName => asset.AssetNameEquals(assetName)) || 
				this.GameAssetKeys.Any(assetName => asset.AssetNameEquals(assetName));
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
				foreach (CustomCommunityCentre.Data.BundleMetadata bundleMetadata in Bundles.GetAllCustomBundleMetadataEntries())
				{
					foreach (string bundleName in bundleMetadata.BundleDisplayNames.Keys)
					{
						data[bundleName] = CustomCommunityCentre.Data.BundleMetadata.GetLocalisedString(
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
				foreach (CustomCommunityCentre.Data.BundleMetadata bundleMetadata in Bundles.GetAllCustomBundleMetadataEntries())
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
				foreach (CustomCommunityCentre.Data.BundleMetadata bundleMetadata in Bundles.GetAllCustomBundleMetadataEntries())
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
				<Dictionary<string, Dictionary<string, List<CustomCommunityCentre.Data.BundleMetadata>>>>
				(CustomCommunityCentre.AssetManager.BundleMetadataAssetKey)
				[CustomCommunityCentre.AssetManager.BundleMetadataKey];

			Bundles.BundleData = Game1.content.Load
				<Dictionary<string, Dictionary<string, List<StardewValley.GameData.RandomBundleData>>>>
				(CustomCommunityCentre.AssetManager.BundleDefinitionsAssetKey)
				[CustomCommunityCentre.AssetManager.BundleDefinitionsKey];
		}

		public static string PrefixAsset(string asset, string prefix = null)
		{
			return string.Join(".", prefix ?? CustomCommunityCentre.AssetManager.AssetPrefix, asset);
		}
	}
}
