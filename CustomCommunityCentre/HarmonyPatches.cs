using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
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
				targetType: typeof(GameLocation),
				targetMethod: "setUpLocationSpecificFlair",
				patchMethod: nameof(HarmonyPatches.SetUpLocationSpecificFlair_Postfix)),
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
			/*
			// FatalExecutionEngineError: Uncomment for SDV 1.5.5
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "getNotePosition",
				patchMethod: nameof(HarmonyPatches.GetNotePosition_Prefix)),
			*/

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

			// Area completion methods:
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "startGoodbyeDance",
				patchMethod: nameof(HarmonyPatches.StartGoodbyeDance_Prefix)),
			new Patch(
				targetType: typeof(CommunityCenter),
				targetMethod: "getMessageForAreaCompletion",
				patchMethod: nameof(HarmonyPatches.GetMessageForAreaCompletion_Postfix)),
		};

		public static void ApplyHarmonyPatches(string id)
		{
			Harmony harmony = new Harmony(id: id);
			foreach (Patch patch in Patches)
			{
				Log.D($"Applying Harmony patch {patch.TargetType}_{patch.PatchMethod}",
					ModEntry.Config.DebugMode);

				// Generate patch method
				string harmonyTypeName = patch.PatchMethod.Split('_').Last();
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
			Log.E($"Error in Harmony patch.{Environment.NewLine}{e}");
		}

		public static void Junimo_ctor_Postfix(
			Junimo __instance,
			Vector2 position,
			int whichArea)
		{
			if (whichArea >= Bundles.CustomAreaInitialIndex
				&& !Bundles.IsAbandonedJojaMartBundleAvailableOrComplete())
			{
				BundleMetadata bundleMetadata = Bundles.BundleMetadata
					.First(bmd => Bundles.GetAreaNumberFromName(bmd.AreaName) == whichArea);

				if (position == Vector2.Zero)
				{
					Vector2 noteTileLocation = Utility.PointToVector2(bundleMetadata.NoteTileLocation);
					noteTileLocation += bundleMetadata.JunimoOffsetFromNoteTileLocation;
					__instance.Position = noteTileLocation * Game1.tileSize;
				}

				Reflection.GetField
						<Netcode.NetColor>
						(obj: __instance, name: "color")
					.GetValue()
					.Set(bundleMetadata.JunimoColour);
				// TODO: FIX: Junimo colour in area completion dance is set for all junimos
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

		public static void HasCompletedCommunityCenter_Postfix(
			Farmer __instance,
			ref bool __result)
		{
			bool resultModifier = Bundles.HasOrWillReceiveAreaCompletedMailForAllCustomAreas();
			__result &= resultModifier;
		}

		public static bool LoadArea_Prefix(
			CommunityCenter __instance,
			int area)
		{
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
			return true;
		}

		public static void LoadArea_Postfix(
			CommunityCenter __instance,
			int area)
		{
			string areaName = CommunityCenter.getAreaNameFromNumber(area);
			string mail = string.Format(Bundles.MailAreaCompleted, string.Join(string.Empty, areaName.Split(' ')));
			if (area >= Bundles.CustomAreaInitialIndex && !Game1.MasterPlayer.hasOrWillReceiveMail(mail))
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
					&& cc.shouldNoteAppearInArea(areaNumber) && !cc.areasComplete[areaNumber])
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

			ModEntry.Instance._lastJunimoNoteMenuArea.Value = area;
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

		public static void SetUpLocationSpecificFlair_Postfix(
			GameLocation __instance)
		{
			if (!(__instance is CommunityCenter cc))
				return;

			if ((Game1.MasterPlayer.hasCompletedCommunityCenter()
				|| (cc.currentEvent != null && cc.currentEvent.id == (int)Bundles.EventIds.CommunityCentreComplete)))
			{
				Bundles.DrawStarInCommunityCentre(cc);
			}
		}

		public static void ResetSharedState_Postfix(
			CommunityCenter __instance)
		{
			if (__instance.areasComplete.Length > Bundles.DefaultMaxArea + 1)
			{
				__instance.numberOfStarsOnPlaque.Value -= 2;
			}
		}

		public static void GetAreaBounds_Postfix(
			CommunityCenter __instance,
			ref Rectangle __result,
			int area)
		{
			BundleMetadata bundleMetadata = Bundles.GetBundleMetadataFromAreaNumber(area);

			// Override any overlapping bundle areas
			foreach (BundleMetadata bmd in Bundles.BundleMetadata)
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
			string name = Bundles.GetAreaNameFromNumber(areaNumber: areaNumber);

			if (areaNumber < Bundles.CustomAreaInitialIndex || Bundles.IsCommunityCentreComplete(__instance) || string.IsNullOrEmpty(name))
				return;

			__result = name;
		}

		public static void AreaNumberFromName_Postfix(
			// Static
			ref int __result,
			string name)
		{
			int id = Bundles.GetAreaNumberFromName(areaName: name);
			bool isCommunityCentreComplete = Bundles.IsCommunityCentreComplete(Bundles.CC);

			if (id < 0 || id > Bundles.CC.areasComplete.Length || isCommunityCentreComplete)
				return;

			__result = id;
		}

		public static void AreaNumberFromLocation_Postfix(
			CommunityCenter __instance,
			ref int __result,
			Vector2 tileLocation)
		{
			BundleMetadata bundleMetadata = Bundles.BundleMetadata
				.FirstOrDefault(bmd => bmd.AreaBounds.Contains(Utility.Vector2ToPoint(tileLocation)));
			int areaNumber = bundleMetadata != null
				? Bundles.GetAreaNumberFromName(bundleMetadata.AreaName)
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
			BundleMetadata bundleMetadata = Bundles.GetBundleMetadataFromAreaNumber(areaNumber);

			if (areaNumber < Bundles.CustomAreaInitialIndex || Bundles.IsCommunityCentreComplete(Bundles.CC) || bundleMetadata == null)
				return;

			string displayName = BundleMetadata.GetLocalisedString(
				dict: bundleMetadata.AreaDisplayNames,
				defaultValue: bundleMetadata.AreaName,
				code: LocalizedContentManager.LanguageCode.en);
			__result = displayName;
		}

		public static void AreaDisplayNameFromNumber_Postfix(
			// Static
			ref string __result,
			int areaNumber)
		{
			BundleMetadata bundleMetadata = Bundles.GetBundleMetadataFromAreaNumber(areaNumber);

			if (areaNumber < Bundles.CustomAreaInitialIndex || Bundles.IsCommunityCentreComplete(Bundles.CC) || bundleMetadata == null)
				return;

			string displayName = BundleMetadata.GetLocalisedString(
				dict: bundleMetadata.AreaDisplayNames,
				defaultValue: bundleMetadata.AreaName);
			__result = displayName;
		}

		public static void ShouldNoteAppearInArea_Postfix(
			CommunityCenter __instance,
			ref bool __result,
			int area)
		{
			BundleMetadata bundleMetadata = Bundles.GetBundleMetadataFromAreaNumber(area);
			bool isCommunityCentreComplete = Bundles.IsCommunityCentreComplete(__instance);

			if (area < Bundles.CustomAreaInitialIndex || isCommunityCentreComplete || bundleMetadata == null)
				return;

			bool isAreaComplete = Bundles.IsAreaCompleteAndLoaded(cc: __instance, areaNumber: area);
			int required = bundleMetadata.BundlesRequired;
			__result = __instance.numberOfCompleteBundles() >= required && !isAreaComplete;
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
					Bundles.ReplaceAreaBundleConversions(cc: __instance);
					return false;
				}
			}
			catch (Exception e)
			{
				HarmonyPatches.ErrorHandler(e);
			}
			return true;
		}

		/// <summary>
		/// Add junimos for extra bundles to the CC completion goodbye dance.
		/// </summary>
		public static void StartGoodbyeDance_Prefix(
			CommunityCenter __instance)
		{
			Bundles.DrawStarInCommunityCentre(__instance);

			HarmonyPatches.SetUpJunimosForGoodbyeDance(cc: __instance);
			List<Junimo> junimos = __instance.getCharacters().OfType<Junimo>().ToList();
			foreach (Junimo junimo in junimos)
			{
				junimo.sayGoodbye();
			}
		}

		/// <summary>
		/// Add junimos for extra bundles to the CC completion goodbye dance.
		/// </summary>
		public static void JunimoGoodbyeDance_Prefix(
			CommunityCenter __instance)
		{
			HarmonyPatches.SetUpJunimosForGoodbyeDance(cc: __instance);
		}

		private static void SetUpJunimosForGoodbyeDance(CommunityCenter cc)
		{
			List<Junimo> junimos = cc.getCharacters().OfType<Junimo>().ToList();
			Vector2 min = new Vector2(junimos.Min(j => j.Position.X), junimos.Min(j => j.Position.Y));
			Vector2 max = new Vector2(junimos.Max(j => j.Position.X), junimos.Max(j => j.Position.Y));
			for (int i = 0; i < Bundles.CustomAreaNameAndNumberDictionary.Count; ++i)
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

		/// <summary>
		/// Return the area completion string for bundles above 6.
		/// </summary>
		public static void GetMessageForAreaCompletion_Postfix(
			CommunityCenter __instance,
			ref string __result)
		{
			if (!Bundles.IsAbandonedJojaMartBundleAvailableOrComplete())
			{
				int which = __instance.areasComplete.Count(isComplete => isComplete);
				string message = Game1.content.LoadString(
					$@"Strings/Locations:CommunityCenter_AreaCompletion{which}",
					Game1.player.Name);

				if (string.IsNullOrWhiteSpace(message))
					return;

				__result = message;
			}
		}
	}
}
