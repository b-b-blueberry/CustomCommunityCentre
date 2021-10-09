using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CommunityCentreKitchen
{
    public class AssetManager : IAssetEditor, IAssetLoader
	{
		// Game content assets
		public static readonly string RootGameContentPath = PathUtilities.NormalizeAssetName(
			$@"Mods/{ModEntry.Instance.ModManifest.UniqueID}.Assets");
		public static readonly string BundleSpritesAssetKey = Path.Combine(RootGameContentPath, "BundleSprites");
		public static readonly string DeliverySpritesAssetKey = Path.Combine(RootGameContentPath, "DeliverySprites");

		// Local content assets
		private static readonly string LocalBundleMetadataPath = @"assets/bundleMetadata";
		private static readonly string LocalBundleDefinitionsPath = @"assets/bundleDefinitions";
		private static readonly string LocalBundleSubstitutesPath = @"assets/bundleSubstitutes";
		private static readonly string LocalBundleSpritesPath = @"assets/bundleSprites";
		private static readonly string LocalDeliverySpritesPath = @"assets/deliverySprites";


		public bool CanLoad<T>(IAssetInfo asset)
		{
			return asset.AssetNameEquals(CommunityCentreKitchen.AssetManager.BundleSpritesAssetKey)
				|| asset.AssetNameEquals(CommunityCentreKitchen.AssetManager.DeliverySpritesAssetKey);
		}

		public T Load<T>(IAssetInfo asset)
		{
			if (asset.AssetNameEquals(CommunityCentreKitchen.AssetManager.BundleSpritesAssetKey))
			{
				return (T)(object)ModEntry.Instance.Helper.Content.Load
					<Texture2D>
					($"{CommunityCentreKitchen.AssetManager.LocalBundleSpritesPath}.png");
			}
			if (asset.AssetNameEquals(CommunityCentreKitchen.AssetManager.DeliverySpritesAssetKey))
			{
				return (T)(object)ModEntry.Instance.Helper.Content.Load
					<Texture2D>
					($"{CommunityCentreKitchen.AssetManager.LocalDeliverySpritesPath}.png");
			}
			return (T)(object)null;
		}

		public bool CanEdit<T>(IAssetInfo asset)
		{
			return CustomCommunityCentre.ModEntry.AssetManager.ModAssetKeys.Any(assetName => asset.AssetNameEquals(assetName))
				|| asset.AssetNameEquals(@"Maps/townInterior")
				|| asset.AssetNameEquals(@"Data/mail");
		}

		public void Edit<T>(IAssetData asset)
		{
			this.Edit(asset: ref asset); // eat that, ENC0036
		}

		public void Edit(ref IAssetData asset)
		{
			// Add entries to custom area-bundle assets

			if (asset.AssetNameEquals(CustomCommunityCentre.AssetManager.BundleMetadataAssetKey))
			{
				var data = asset.AsDictionary<string, Dictionary<string, List<CustomCommunityCentre.Data.BundleMetadata>>>().Data;
				var newData = ModEntry.Instance.Helper.Content.Load
					<Dictionary<string, Dictionary<string, List<CustomCommunityCentre.Data.BundleMetadata>>>>
					($"{CommunityCentreKitchen.AssetManager.LocalBundleMetadataPath}.json");

				data[CustomCommunityCentre.AssetManager.BundleMetadataKey]
					= data[CustomCommunityCentre.AssetManager.BundleMetadataKey]
						.Union(newData[CustomCommunityCentre.AssetManager.BundleMetadataKey])
						.ToDictionary(pair => pair.Key, pair => pair.Value);

				asset.ReplaceWith(data);
				return;
			}
			if (asset.AssetNameEquals(CustomCommunityCentre.AssetManager.BundleDefinitionsAssetKey))
			{
				var data = asset.AsDictionary<string, Dictionary<string, List<StardewValley.GameData.RandomBundleData>>>().Data;
				var newData = ModEntry.Instance.Helper.Content.Load
					<Dictionary<string, Dictionary<string, List<StardewValley.GameData.RandomBundleData>>>>
					($"{CommunityCentreKitchen.AssetManager.LocalBundleDefinitionsPath}.json");

				data[CustomCommunityCentre.AssetManager.BundleDefinitionsKey]
					= data[CustomCommunityCentre.AssetManager.BundleDefinitionsKey]
						.Union(newData[CustomCommunityCentre.AssetManager.BundleDefinitionsKey])
						.ToDictionary(pair => pair.Key, pair => pair.Value);

				asset.ReplaceWith(data);
				return;
			}
			if (asset.AssetNameEquals(CustomCommunityCentre.AssetManager.BundleSubstitutesAssetKey))
			{
				var data = asset.AsDictionary<string, Dictionary<string, Dictionary<string, List<CustomCommunityCentre.Data.SubstituteBundleData>>>>().Data;
				var newData = ModEntry.Instance.Helper.Content.Load
					<Dictionary<string, Dictionary<string, Dictionary<string, List<CustomCommunityCentre.Data.SubstituteBundleData>>>>>
					($"{CommunityCentreKitchen.AssetManager.LocalBundleSubstitutesPath}.json");

				data[CustomCommunityCentre.AssetManager.BundleSubstitutesKey]
					= data[CustomCommunityCentre.AssetManager.BundleSubstitutesKey]
						.Union(newData[CustomCommunityCentre.AssetManager.BundleSubstitutesKey])
						.ToDictionary(pair => pair.Key, pair => pair.Value);

				asset.ReplaceWith(data);
				return;
			}

			// Edit other game assets for the kitchen

			if (asset.AssetNameEquals(@"Data/mail"))
			{
				var data = asset.AsDictionary<string, string>().Data;

				// Append completed mail received for all custom areas as required flags for CC completion event

				string mailId = string.Format(CustomCommunityCentre.Bundles.MailAreaCompletedFollowup, Kitchen.KitchenAreaName);
				data[mailId] = ModEntry.i18n.Get("mail.areacompletedfollowup.gus");

				mailId = GusDeliveryService.MailSaloonDeliverySurchargeWaived;
				data[mailId] = ModEntry.i18n.Get("mail.saloondeliverysurchargewaived");

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
				Rectangle sourceArea = new(320, 224, targetArea.Width, targetArea.Height); // Apply base fridge sprite
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
		}
	}
}
