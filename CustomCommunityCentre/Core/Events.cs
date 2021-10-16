using StardewValley.Locations;
using System;

namespace CustomCommunityCentre
{
	public class ResetSharedStateEventArgs : EventArgs
	{
		public CommunityCenter CommunityCentre { get; }


		internal ResetSharedStateEventArgs(CommunityCenter communityCentre)
		{
			this.CommunityCentre = communityCentre;
		}
	}

	public class AreaLoadedEventArgs : EventArgs
	{
		public CommunityCenter CommunityCentre { get; }
		public string AreaName { get; }
		public int AreaNumber { get; }


		internal AreaLoadedEventArgs(CommunityCenter communityCentre, string areaName, int areaNumber)
		{
			this.CommunityCentre = communityCentre;
			this.AreaName = areaName;
			this.AreaNumber = areaNumber;
		}
	}

	public class AreaCompleteCutsceneStartedEventArgs : EventArgs
	{
		public string AreaName { get; }
		public int AreaNumber { get; }


		internal AreaCompleteCutsceneStartedEventArgs(string areaName, int areaNumber)
		{
			this.AreaName = areaName;
			this.AreaNumber = areaNumber;
		}
	}


	public class Events
	{
		public static event EventHandler ResetSharedState;
		public static event EventHandler LoadedArea;
		public static event EventHandler AreaCompleteCutsceneStarted;


		internal static void InvokeOnResetSharedState(CommunityCenter communityCentre)
		{
			ResetSharedState?.Invoke(
				sender: null,
				e: new ResetSharedStateEventArgs(
					communityCentre: communityCentre));
		}

		internal static void InvokeOnAreaLoaded(CommunityCenter communityCentre, string areaName, int areaNumber)
		{
			LoadedArea?.Invoke(
				sender: null,
				e: new AreaLoadedEventArgs(
					communityCentre: communityCentre,
					areaName: areaName,
					areaNumber: areaNumber));
		}

		internal static void InvokeOnAreaCompleteCutsceneStarted(string areaName, int areaNumber)
		{
			AreaCompleteCutsceneStarted?.Invoke(
				sender: null,
				e: new AreaCompleteCutsceneStartedEventArgs(
					areaName: areaName,
					areaNumber: areaNumber));
		}
	}
}
