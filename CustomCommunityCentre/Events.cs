using StardewValley.Locations;
using System;

namespace CustomCommunityCentre
{
	public class LoadedAreaEventArgs : EventArgs
	{
		public CommunityCenter CommunityCenter { get; }
		public string AreaName { get; }
		public int AreaNumber { get; }


		internal LoadedAreaEventArgs(CommunityCenter communityCenter, string areaName, int areaNumber)
		{
			this.CommunityCenter = communityCenter;
			this.AreaName = areaName;
			this.AreaNumber = areaNumber;
		}
	}


	public class Events
	{
		public static EventHandler LoadedArea;


		internal static void InvokeOnLoadedArea(CommunityCenter communityCenter, string areaName, int areaNumber)
		{
			LoadedArea.Invoke(
				sender: null,
				e: new LoadedAreaEventArgs(
					communityCenter: communityCenter,
					areaName: areaName,
					areaNumber: areaNumber));
		}
	}
}
