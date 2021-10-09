using StardewModdingAPI;
using System.Collections.Generic;

namespace CustomCommunityCentre.API
{
	public interface ICustomCommunityCentreAPI
	{
		StardewValley.Locations.CommunityCenter GetCommunityCentre();
		Dictionary<string, int> GetCustomAreaNamesAndNumbers();
		bool IsCommunityCentreCompleteEarly();
		bool IsCommunityCentreDefinitelyComplete();
		bool AreAnyCustomAreasLoaded();
		bool AreAnyCustomBundlesLoaded();
		bool AreaAllCustomAreasComplete();
		IEnumerable<string> GetAllAreaNames();
		Dictionary<int, int[]> GetAllCustomAreaNumbersAndBundleNumbers();
		bool IsCustomArea(int areaNumber);
		bool IsCustomBundle(int bundleNumber);
		int GetTotalAreasComplete();
		int GetTotalAreaCount();
		string GetAreaNameAsAssetKey(string areaName);
	}

	public class CustomCommunityCentreAPI
	{
		private readonly IReflectionHelper Reflection;

		public CustomCommunityCentreAPI(IReflectionHelper reflection)
		{
			this.Reflection = reflection;
		}

		public static StardewValley.Locations.CommunityCenter GetCommunityCentre()
		{
			return Bundles.CC;
		}

		Dictionary<string, int> GetCustomAreaNamesAndNumbers()
		{
			return Bundles.CustomAreaNamesAndNumbers;
		}

		bool IsCommunityCentreCompleteEarly()
		{
			return Bundles.IsCommunityCentreCompleteEarly(Bundles.CC);
		}

		bool IsCommunityCentreDefinitelyComplete()
		{
			return Bundles.IsCommunityCentreDefinitelyComplete(Bundles.CC);
		}

		bool AreAnyCustomAreasLoaded()
		{
			return Bundles.AreAnyCustomAreasLoaded();
		}

		bool AreAnyCustomBundlesLoaded()
		{
			return Bundles.AreAnyCustomBundlesLoaded();
		}

		bool AreaAllCustomAreasComplete()
		{
			return Bundles.AreaAllCustomAreasComplete(Bundles.CC);
		}

		IEnumerable<string> GetAllAreaNames()
		{
			return Bundles.GetAllAreaNames();
		}

		string GetAreaNameAsAssetKey(string areaName)
		{
			return Bundles.GetAreaNameAsAssetKey(areaName);
		}

		Dictionary<int, int[]> GetAllCustomAreaNumbersAndBundleNumbers()
		{
			return Bundles.GetAllCustomAreaNumbersAndBundleNumbers();
		}

		bool IsCustomArea(int areaNumber)
		{
			return Bundles.IsCustomArea(areaNumber);
		}

		bool IsCustomBundle(int bundleNumber)
		{
			return Bundles.IsCustomBundle(bundleNumber);
		}

		int GetTotalAreasComplete()
		{
			return Bundles.TotalAreasCompleteCount;
		}

		int GetTotalAreaCount()
		{
			return Bundles.TotalAreaCount;
		}
	}
}
