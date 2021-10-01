﻿using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Events;
using StardewValley.Locations;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomCommunityCentre
{
	public static class HarmonyPatches
	{
		private class Patch
		{
			public readonly Type TargetType;
			public readonly string TargetMethod;
			public readonly Type PatchType;
			public readonly string PatchMethod;
			public readonly Type[] TargetParams;

			public Patch(
				Type targetType, string targetMethod,
				Type patchType = null, string patchMethod = null,
				Type[] targetParams = null)
			{
				this.TargetType = targetType;
				this.TargetMethod = targetMethod;
				this.PatchType = patchType ?? typeof(HarmonyPatches);
				this.PatchMethod = patchMethod;
				this.TargetParams = targetParams;
			}
		}

		private static IReflectionHelper Reflection => ModEntry.Instance.Helper.Reflection;

		private const string ConstructorName = ".ctor";
		private const char PatchDelimiter = '_';

		private static readonly Patch[] Patches = new Patch[]
		{
			// Junimo methods:
			new Patch(
				targetType: typeof(Junimo),
				targetMethod: ConstructorName,
				patchMethod: nameof(HarmonyPatches.Junimo_ctor_Postfix),
				targetParams: new Type[] { typeof(Vector2), typeof(int), typeof(bool) }),

			// Menu methods:
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "checkBundle",
				patchMethod: nameof(HarmonyPatches.CheckBundle_Prefix)),
			new Patch(
				targetType: typeof(JunimoNoteMenu),
				targetMethod: ConstructorName,
				patchMethod: nameof(HarmonyPatches.JunimoNoteMenu_ctor_Postfix),
				targetParams: new Type[] { typeof(bool), typeof(int), typeof(bool) }),

			// Hate hate hate hate hate hate hate hate hate
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: ConstructorName,
				patchMethod: nameof(HarmonyPatches.CommunityCenter_ctor_Postfix),
				targetParams: new Type[] {}),
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: ConstructorName,
				patchMethod: nameof(HarmonyPatches.CommunityCenter_ctor_Postfix),
				targetParams: new Type[] { typeof(string) }),
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "cleanupBeforeSave",
				patchMethod: nameof(HarmonyPatches.CleanupBeforeSave_Prefix)),
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "resetSharedState",
				patchMethod: nameof(HarmonyPatches.ResetSharedState_Postfix)),
			new Patch(
				targetType: typeof(Farmer),
				targetMethod: "hasCompletedCommunityCenter",
				patchMethod: nameof(HarmonyPatches.HasCompletedCommunityCenter_Postfix)),

			// Area position methods:
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "getAreaBounds",
				patchMethod: nameof(HarmonyPatches.GetAreaBounds_Postfix)),
			#if NET5_0
			// FatalExecutionEngineError with .NET Framework <5.0
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "getNotePosition",
				patchMethod: nameof(HarmonyPatches.GetNotePosition_Prefix)),
			#endif
			// Area name and number methods:
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "getAreaNameFromNumber",
				patchMethod: nameof(HarmonyPatches.AreaNameFromNumber_Postfix)),
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "getAreaNumberFromName",
				patchMethod: nameof(HarmonyPatches.AreaNumberFromName_Postfix)),
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "getAreaNumberFromLocation",
				patchMethod: nameof(HarmonyPatches.AreaNumberFromLocation_Postfix)),
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "getAreaEnglishDisplayNameFromNumber",
				patchMethod: nameof(HarmonyPatches.AreaEnglishDisplayNameFromNumber_Postfix)),
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "getAreaDisplayNameFromNumber",
				patchMethod: nameof(HarmonyPatches.AreaDisplayNameFromNumber_Postfix)),
			new Patch(
				targetType: typeof(JunimoNoteMenu),
				targetMethod: "getRewardNameForArea",
				patchMethod: nameof(HarmonyPatches.GetRewardNameForArea_Postfix)),

			// Area progress methods:
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "loadArea",
				patchMethod: nameof(HarmonyPatches.LoadArea_Prefix)),
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "loadArea",
				patchMethod: nameof(HarmonyPatches.LoadArea_Postfix)),
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "shouldNoteAppearInArea",
				patchMethod: nameof(HarmonyPatches.ShouldNoteAppearInArea_Postfix)),
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "initAreaBundleConversions",
				patchMethod: nameof(HarmonyPatches.InitAreaBundleConversions_Prefix)),

			// oh my god
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "markAreaAsComplete",
				patchMethod: nameof(HarmonyPatches.MarkAreaAsComplete_Prefix)),
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "getNumberOfAreasComplete",
				patchMethod: nameof(HarmonyPatches.GetNumberOfAreasComplete_Postfix)),
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "areAllAreasComplete",
				patchMethod: nameof(HarmonyPatches.AreAllAreasComplete_Postfix)),
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "MakeMapModifications",
				patchMethod: nameof(HarmonyPatches.MakeMapModifications_Postfix)),
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "checkForMissedRewards",
				patchMethod: nameof(HarmonyPatches.CheckForMissedRewards_Postfix)),

			// Area completion methods:
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "doCheckForNewJunimoNotes",
				patchMethod: nameof(HarmonyPatches.DoCheckForNewJunimoNotes_Postfix)),
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "startGoodbyeDance",
				patchMethod: nameof(HarmonyPatches.StartGoodbyeDance_Prefix)),
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "getMessageForAreaCompletion",
				patchMethod: nameof(HarmonyPatches.GetMessageForAreaCompletion_Postfix)),

			// Community Centre completion methods:
			new Patch(
				targetType: typeof(Utility),
				targetMethod: "pickFarmEvent",
				patchMethod: nameof(HarmonyPatches.PickFarmEvent_Prefix))
		};

		public static void ApplyHarmonyPatches(string id)
		{
			Harmony harmony = new Harmony(id: id);
			foreach (Patch patch in Patches)
			{
				Log.D($"Applying Harmony patch {patch.TargetType}{PatchDelimiter}{patch.PatchMethod}",
					ModEntry.Config.DebugMode);

				// Generate patch method
				string harmonyTypeName = patch.PatchMethod.Split(PatchDelimiter).Last();
				HarmonyPatchType harmonyType = (HarmonyPatchType)Enum.Parse(
					enumType: typeof(HarmonyPatchType),
					value: harmonyTypeName);
				HarmonyMethod harmonyMethod = new HarmonyMethod(
					methodType: patch.PatchType,
					methodName: patch.PatchMethod);

				// Get original method
				System.Reflection.MethodBase original = (patch.TargetMethod == ConstructorName)
					? (System.Reflection.MethodBase)AccessTools.Constructor(
						type: patch.TargetType,
						parameters: patch.TargetParams)
					: AccessTools.Method(
						type: patch.TargetType,
						name: patch.TargetMethod,
						parameters: patch.TargetParams);

				// Apply patch to original
				harmony.Patch(
					original: original,
					prefix: harmonyType == HarmonyPatchType.Prefix ? harmonyMethod : null,
					postfix: harmonyType == HarmonyPatchType.Postfix ? harmonyMethod : null,
					transpiler: harmonyType == HarmonyPatchType.Transpiler ? harmonyMethod : null,
					finalizer: harmonyType == HarmonyPatchType.Finalizer ? harmonyMethod : null);
			}
		}

		private static void ErrorHandler(Exception e)
		{
			Log.E($"Error in Harmony patch:{Environment.NewLine}{e}");
		}

		public static void Junimo_ctor_Postfix(
			Junimo __instance,
			Vector2 position,
			int whichArea)
		{
			if (whichArea >= Bundles.CustomAreaInitialIndex
				&& !Bundles.IsAbandonedJojaMartBundleAvailableOrComplete())
			{
				CustomCommunityCentre.Data.BundleMetadata bundleMetadata = Bundles.CustomBundleMetadata
					.First(bmd => Bundles.GetCustomAreaNumberFromName(bmd.AreaName) == whichArea);

				__instance.friendly.Value = Bundles.IsAreaComplete(cc: Bundles.CC, areaNumber: whichArea);

				int restoreAreaPhase = Reflection.GetField
						<int>
						(obj: Bundles.CC, name: "restoreAreaPhase")
					.GetValue();
				if (restoreAreaPhase != CommunityCenter.PHASE_junimoAppear)
				{
					Reflection.GetField
							<Netcode.NetColor>
							(obj: __instance, name: "color")
						.GetValue()
						.Set(bundleMetadata.JunimoColour);
				}
			}
		}

		public static bool CheckBundle_Prefix(
			CommunityCenter __instance,
			int area)
		{
			try
			{
				if (area < Bundles.CustomAreaInitialIndex)
					return true;

				Bundles.SetCustomAreaMutex(cc: __instance, areaNumber: area, isLocked: true);

				return false;
			}
			catch (Exception e)
			{
				HarmonyPatches.ErrorHandler(e: e);
			}
			return true;
		}

		public static void GetRewardNameForArea_Postfix(
			JunimoNoteMenu __instance,
			int whichArea,
			ref string __result)
		{
			CustomCommunityCentre.Data.BundleMetadata bundleMetadata = Bundles.GetCustomBundleMetadataFromAreaNumber(whichArea);

			if (bundleMetadata == null || !Bundles.IsCustomArea(whichArea) || Bundles.IsCommunityCentreComplete(Bundles.CC))
				return;

			__result = CustomCommunityCentre.Data.BundleMetadata.GetLocalisedString(
				dict: bundleMetadata.AreaRewardMessage,
				defaultValue: "???");
		}

		public static bool LoadArea_Prefix(
			CommunityCenter __instance,
			int area)
		{
			/*
			try
			{
				if ((area == CommunityCenter.AREA_Bulletin2 && !__instance.areasComplete[CommunityCenter.AREA_Bulletin])
					|| (area == CommunityCenter.AREA_JunimoHut && !__instance.areAllAreasComplete()))
				{
					return false;
				}
			}
			catch (Exception e)
			{
				HarmonyPatches.ErrorHandler(e);
			}
			*/
			return true;
		}

		public static void LoadArea_Postfix(
			CommunityCenter __instance,
			int area)
		{
			string areaName = CommunityCenter.getAreaNameFromNumber(area);
			string mail = string.Format(Bundles.MailAreaCompleted, Bundles.GetAreaNameAsAssetKey(areaName));
			if (string.IsNullOrEmpty(areaName) || Bundles.IsCustomArea(area) && !Game1.MasterPlayer.hasOrWillReceiveMail(mail))
			{
				// Add some mail flag to this bundle to indicate completion
				Log.D($"Sending mail for custom bundle completion ({mail})",
					ModEntry.Config.DebugMode);
				Game1.addMailForTomorrow(mail, noLetter: true);
			}

			Events.InvokeOnLoadedArea(communityCenter: __instance, areaName: areaName, areaNumber: area);
		}

		public static void JunimoNoteMenu_ctor_Postfix(
			JunimoNoteMenu __instance,
			bool fromGameMenu,
			int area,
			bool fromThisMenu)
		{
			CommunityCenter cc = Bundles.CC;
			if (Bundles.IsCommunityCentreComplete(cc))
				return;

			IReflectedField<int> whichAreaField = Reflection.GetField
				<int>
				(__instance, "whichArea");
			
			bool isAreaSet = false;
			bool isNavigationSet = false;
			foreach (string areaName in Bundles.GetAllAreaNames())
			{
				int areaNumber = CommunityCenter.getAreaNumberFromName(areaName);

				// Set default area for menu view with custom areas
				if (!isAreaSet
					&& fromGameMenu && !fromThisMenu && !isAreaSet
					&& cc.shouldNoteAppearInArea(areaNumber) && !Bundles.IsAreaComplete(cc: cc, areaNumber: areaNumber))
				{
					area = areaNumber;
					whichAreaField.SetValue(area);
					isAreaSet = true;
				}

				// Show navigation arrows when custom areas
				if (!isNavigationSet
					&& areaNumber >= 0 && areaNumber != area && cc.shouldNoteAppearInArea(areaNumber))
				{
					__instance.areaNextButton.visible = true;
					__instance.areaBackButton.visible = true;
					isNavigationSet = true;
				}

				if (isAreaSet && isNavigationSet)
					break;
			}
		}

		public static void CommunityCenter_ctor_Postfix(
			CommunityCenter __instance)
		{
			Bundles.SetCC(__instance);
		}

		public static void CleanupBeforeSave_Prefix(
			CommunityCenter __instance)
		{
			__instance.modData.Remove(Bundles.KeyMutexes);
		}

		public static void ResetSharedState_Postfix(
			CommunityCenter __instance)
		{
			if (Game1.MasterPlayer.mailReceived.Contains("JojaMember"))
				return;
			
			if (__instance.areAllAreasComplete())
			{
				if (Bundles.AreAnyCustomAreasLoaded())
				{
					__instance.numberOfStarsOnPlaque.Value += 1;
				}
			}
			else
			{
				foreach (int areaNumber in Bundles.CustomAreasComplete.Keys)
				{
					bool shouldNoteAppearInArea = false;
					HarmonyPatches.ShouldNoteAppearInArea_Postfix(
						__instance: __instance,
						__result: ref shouldNoteAppearInArea,
						area: areaNumber);
					if (shouldNoteAppearInArea)
					{
						string areaName = Bundles.GetCustomAreaNameFromNumber(areaNumber);
						CustomCommunityCentre.Data.BundleMetadata bundleMetadata = Bundles.CustomBundleMetadata
							.First(bmd => bmd.AreaName == areaName);

						Vector2 tileLocation = Utility.PointToVector2(bundleMetadata.NoteTileLocation);
						tileLocation += bundleMetadata.JunimoOffsetFromNoteTileLocation;

						Junimo j = new Junimo(position: tileLocation * Game1.tileSize, whichArea: areaNumber);
						__instance.characters.Add(j);
					}
				}
			}
		}

		public static void HasCompletedCommunityCenter_Postfix(
			Farmer __instance,
			ref bool __result)
		{
			bool resultModifier = Bundles.HasOrWillReceiveAreaCompletedMailForAllCustomAreas();
			__result &= resultModifier;
		}

		public static void GetAreaBounds_Postfix(
			CommunityCenter __instance,
			ref Rectangle __result,
			int area)
		{
			CustomCommunityCentre.Data.BundleMetadata bundleMetadata = Bundles.GetCustomBundleMetadataFromAreaNumber(area);

			// Override any overlapping bundle areas
			foreach (CustomCommunityCentre.Data.BundleMetadata bmd in Bundles.CustomBundleMetadata)
			{
				if ((bundleMetadata == null || bmd.AreaName != bundleMetadata.AreaName)
					&& __result != Rectangle.Empty
					&& !Bundles.IsCommunityCentreComplete(__instance))
				{
					Rectangle intersection = Rectangle.Intersect(__result, bmd.AreaBounds);
					if (intersection.Width > 0)
					{
						__result.X += intersection.Width;
						__result.Width -= intersection.Width;
					}
					intersection = Rectangle.Intersect(__result, bmd.AreaBounds);
					if (intersection.Height > 0)
					{
						__result.Y += intersection.Height;
						__result.Height -= intersection.Height;
					}
				}
			}

			if (area < Bundles.CustomAreaInitialIndex || Bundles.IsCommunityCentreComplete(__instance) || bundleMetadata == null)
				return;

			__result = bundleMetadata.AreaBounds;
		}

		public static void GetNotePosition_Prefix(
			CommunityCenter __instance,
			ref Point __result,
			int area)
		{
			// FatalExecutionEngineError
		}

		public static void AreaNameFromNumber_Postfix(
			CommunityCenter __instance,
			ref string __result,
			int areaNumber)
		{
			string name = Bundles.GetCustomAreaNameFromNumber(areaNumber: areaNumber);

			if (!Bundles.IsCustomArea(areaNumber) || Bundles.IsCommunityCentreComplete(__instance) || string.IsNullOrEmpty(name))
				return;

			__result = name;
		}

		public static void AreaNumberFromName_Postfix(
			// Static
			ref int __result,
			string name)
		{
			int id = Bundles.GetCustomAreaNumberFromName(areaName: name);
			bool isCommunityCentreComplete = Bundles.IsCommunityCentreComplete(Bundles.CC);

			if (id < 0 || isCommunityCentreComplete)
				return;

			__result = id;
		}

		public static void AreaNumberFromLocation_Postfix(
			CommunityCenter __instance,
			ref int __result,
			Vector2 tileLocation)
		{
			CustomCommunityCentre.Data.BundleMetadata bundleMetadata = Bundles.CustomBundleMetadata
				.FirstOrDefault(bmd => bmd.AreaBounds.Contains(Utility.Vector2ToPoint(tileLocation)));
			int areaNumber = bundleMetadata != null
				? Bundles.GetCustomAreaNumberFromName(bundleMetadata.AreaName)
				: -1;

			if (areaNumber < 0 || Bundles.IsCommunityCentreComplete(__instance))
				return;

			__result = areaNumber;
		}

		public static void AreaEnglishDisplayNameFromNumber_Postfix(
			// Static
			ref string __result,
			int areaNumber)
		{
			CustomCommunityCentre.Data.BundleMetadata bundleMetadata = Bundles.GetCustomBundleMetadataFromAreaNumber(areaNumber);

			if (!Bundles.IsCustomArea(areaNumber) || Bundles.IsCommunityCentreComplete(Bundles.CC) || bundleMetadata == null)
				return;

			string displayName = CustomCommunityCentre.Data.BundleMetadata.GetLocalisedString(
				dict: bundleMetadata.AreaDisplayName,
				defaultValue: bundleMetadata.AreaName,
				code: LocalizedContentManager.LanguageCode.en);
			__result = displayName;
		}

		public static void AreaDisplayNameFromNumber_Postfix(
			// Static
			ref string __result,
			int areaNumber)
		{
			CustomCommunityCentre.Data.BundleMetadata bundleMetadata = Bundles.GetCustomBundleMetadataFromAreaNumber(areaNumber);

			if (!Bundles.IsCustomArea(areaNumber) || Bundles.IsCommunityCentreComplete(Bundles.CC) || bundleMetadata == null)
				return;

			string displayName = CustomCommunityCentre.Data.BundleMetadata.GetLocalisedString(
				dict: bundleMetadata.AreaDisplayName,
				defaultValue: bundleMetadata.AreaName);
			__result = displayName;
		}

		public static void ShouldNoteAppearInArea_Postfix(
			CommunityCenter __instance,
			ref bool __result,
			int area)
		{
			CustomCommunityCentre.Data.BundleMetadata bundleMetadata = Bundles.GetCustomBundleMetadataFromAreaNumber(area);

			if (!Bundles.IsCustomArea(area) || bundleMetadata == null)
				return;

			bool isAreaComplete = Bundles.IsAreaComplete(cc: __instance, areaNumber: area);
			int bundlesRequired = bundleMetadata.BundlesRequired;
			int bundlesCompleted = __instance.numberOfCompleteBundles();
			__result = bundlesCompleted >= bundlesRequired && !isAreaComplete;
		}

		public static bool InitAreaBundleConversions_Prefix(
			CommunityCenter __instance)
		{
			try
			{
				Dictionary<int, List<int>> areaBundleDict = Reflection.GetField
					<Dictionary<int, List<int>>>
					(__instance, "areaToBundleDictionary")
					.GetValue();

				if (!Context.IsMainPlayer
					&& Bundles.DefaultMaxArea > 0
					&& (areaBundleDict == null || areaBundleDict.Count == Bundles.DefaultMaxArea + 1))
				{
					BundleManager.ReplaceAreaBundleConversions(cc: __instance);
					return false;
				}
			}
			catch (Exception e)
			{
				HarmonyPatches.ErrorHandler(e);
			}
			return true;
		}

		public static bool MarkAreaAsComplete_Prefix(
			CommunityCenter __instance,
			int area)
		{
			try
			{
				if (Bundles.IsCustomArea(area))
				{
					if (Game1.currentLocation is CommunityCenter)
					{
						Bundles.CustomAreasComplete[area] = true;

						if (__instance.areAllAreasComplete())
						{
							Reflection.GetField
								<bool>
								(obj: __instance, name: "_isWatchingJunimoGoodbye")
								.SetValue(true);
						}
					}
					return false;
				}
			}
			catch (Exception e)
			{
				HarmonyPatches.ErrorHandler(e);
			}
			return true;
		}

		public static void GetNumberOfAreasComplete_Postfix(
			CommunityCenter __instance,
			ref int __result)
		{
			//__result = Bundles.GetTotalAreasComplete(__instance);
		}

		public static void AreAllAreasComplete_Postfix(
			CommunityCenter __instance,
			ref bool __result)
		{
			bool resultModifier = Bundles.AreaAllCustomAreasComplete(__instance);
			__result &= resultModifier;
		}

		public static void MakeMapModifications_Postfix(
			CommunityCenter __instance)
		{
			if (!Game1.MasterPlayer.mailReceived.Contains("JojaMember") && !__instance.areAllAreasComplete())
			{
				foreach (int areaNumber in Bundles.CustomAreasComplete.Keys)
				{
					if (__instance.shouldNoteAppearInArea(area: areaNumber))
					{
						__instance.addJunimoNote(area: areaNumber);
					}
					else if (Bundles.IsCustomAreaComplete(areaNumber))
					{
						__instance.loadArea(area: areaNumber, showEffects: false);
					}
				}
			}
		}

		// TODO: FIX: 'Whiff' sprite on interacting with missedRewardsChest while only custom rewards are unclaimed.
		// Related to base method behaviour: if (... || areasComplete.Count() <= area || ...) continue;

		public static void CheckForMissedRewards_Postfix(
			CommunityCenter __instance)
		{
			Dictionary<int, int[]> areaNumbersAndBundleNumbers = Bundles.GetAllCustomAreaNumbersAndBundleNumbers();
			List<Item> rewards = new List<Item>();

			foreach (KeyValuePair<int, int[]> areaAndBundles in areaNumbersAndBundleNumbers)
			{
				bool isUnclaimed = areaAndBundles.Value
					.All(bundleNumber => __instance.bundleRewards.TryGetValue(bundleNumber, out bool isUnclaimed) && isUnclaimed);
				if (!isUnclaimed)
					continue;

				rewards.Clear();
				JunimoNoteMenu.GetBundleRewards(areaAndBundles.Key, rewards);
				foreach (Item item in rewards)
				{
					__instance.missedRewardsChest.Value.addItem(item);
				}
			}

			bool isRewardsListClear = Game1.netWorldState.Value.BundleRewards.Values.All(isUnclaimed => !isUnclaimed);

			if ((!isRewardsListClear && !__instance.missedRewardsChestVisible.Value)
				|| (isRewardsListClear && __instance.missedRewardsChestVisible.Value))
			{
				__instance.showMissedRewardsChestEvent.Fire(arg: !isRewardsListClear);
				if (isRewardsListClear)
				{
					Vector2 missedRewardsChestTile = Reflection.GetField
						<Vector2>
						(obj: __instance, name: "missedRewardsChestTile")
						.GetValue();

					Bundles.BroadcastPuffSprites(
						multiplayer: null,
						location: __instance,
						tilePosition: missedRewardsChestTile);
				}
				else
				{
					// TODO: TEST: missedRewardsChest TemporarySprites.Remove(sprite)
					TemporaryAnimatedSprite sprite = __instance.TemporarySprites
						.FirstOrDefault(s => s.initialParentTileIndex == 5 || s.initialParentTileIndex == 46);
					if (sprite != null)
					{
						__instance.TemporarySprites.Remove(sprite);
					}
				}
			}
			__instance.missedRewardsChestVisible.Value = !isRewardsListClear;
		}

		public static void DoCheckForNewJunimoNotes_Postfix(
			CommunityCenter __instance)
		{
			if (!(Game1.currentLocation is CommunityCenter))
				return;
			
			foreach (int areaNumber in Bundles.CustomAreasComplete.Keys)
			{
				Point p = Reflection.GetMethod(obj: __instance, name: "getNotePosition").Invoke<Point>(areaNumber);
				bool isNoteSuperAtAreaAreYouSure = __instance.Map.GetLayer("Buildings").Tiles[p.X, p.Y] is xTile.Tiles.Tile tile
					&& tile != null && tile.TileIndex != 0;

				bool isNoteAtArea = __instance.isJunimoNoteAtArea(areaNumber);
				bool isNoteReady = __instance.shouldNoteAppearInArea(areaNumber);

				if (!(isNoteAtArea || isNoteSuperAtAreaAreYouSure) && isNoteReady)
				{
					__instance.addJunimoNoteViewportTarget(areaNumber);
				}
			}
		}

		/// <summary>
		/// Add junimos for extra bundles to the CC completion goodbye dance.
		/// </summary>
		public static void StartGoodbyeDance_Prefix(
			CommunityCenter __instance)
		{
			Bundles.SetUpJunimosForGoodbyeDance(cc: __instance);
			List<Junimo> junimos = __instance.getCharacters().OfType<Junimo>().ToList();
			foreach (Junimo junimo in junimos)
			{
				junimo.sayGoodbye();
			}
		}

		public static void GetMessageForAreaCompletion_Postfix(
			CommunityCenter __instance,
			ref string __result)
		{
			int areaNumber = Reflection.GetField
					<int>
					(obj: __instance, name: "restoreAreaIndex")
				.GetValue();
			string areaName = Bundles.GetCustomAreaNameFromNumber(areaNumber);

			if (areaNumber < Bundles.DefaultMaxArea
				|| string.IsNullOrWhiteSpace(areaName)
				|| Bundles.IsAbandonedJojaMartBundleAvailableOrComplete())
				return;
			
			string message = Game1.content.LoadString(
				$@"Strings/Locations:CommunityCenter_AreaCompletion_{Bundles.GetAreaNameAsAssetKey(areaName)}",
				Game1.player.Name);

			__result = message;
		}

		public static bool PickFarmEvent_Prefix(
			ref FarmEvent __result)
		{
			try
			{
				if (Game1.weddingToday)
				{
					return true;
				}
				foreach (Farmer onlineFarmer in Game1.getOnlineFarmers())
				{
					Friendship spouseFriendship = onlineFarmer.GetSpouseFriendship();
					if (spouseFriendship != null && spouseFriendship.IsMarried() && spouseFriendship.WeddingDate == Game1.Date)
					{
						return true;
					}
				}

				foreach (KeyValuePair<string, int> areaNameAndNumber in Bundles.CustomAreaNamesAndNumbers)
				{
					string mailId = string.Format(Bundles.MailAreaCompleted, areaNameAndNumber.Key);
					if (Game1.player.mailForTomorrow.Contains(mailId) || Game1.player.mailForTomorrow.Contains($"{mailId}%&NL&%"))
					{
						int whichEvent = areaNameAndNumber.Value;
						__result = new AreaCompleteNightEvent(whichEvent);
						Log.D($"Adding new {nameof(AreaCompleteNightEvent)} for area {areaNameAndNumber.Value} ({areaNameAndNumber.Key})");
						return false;
					}
				}
			}
			catch (Exception e)
			{
				HarmonyPatches.ErrorHandler(e);
			}
			return true;
		}
	}
}