namespace InventoryTweaks
{
	/// <summary>
	/// Live counters behind <c>it info</c>: the startup log proves the patches were installed,
	/// these prove they are being reached. No locking: all writes happen on the main thread.
	/// </summary>
	internal static class Counters
	{
		/// <summary>Sorts that moved the list back to the top (any window).</summary>
		internal static int SortsScrolled;

		/// <summary>Sorts run automatically because a sort order is locked.</summary>
		internal static int AutoSorts;

		/// <summary>Times new items were moved to the front of the backpack.</summary>
		internal static int NewFirstSorts;

		internal static int KeyCloses;

		/// <summary>Backpack slots flagged as newly arrived.</summary>
		internal static int ItemsFlagged;

		/// <summary>Flagged slots cleared because they were on screen long enough.</summary>
		internal static int ItemsSeen;

		/// <summary>Bag change events the tracker examined.</summary>
		internal static int BagChanges;

		internal static void Reset()
		{
			SortsScrolled = 0;
			AutoSorts = 0;
			NewFirstSorts = 0;
			KeyCloses = 0;
			ItemsFlagged = 0;
			ItemsSeen = 0;
			BagChanges = 0;
		}
	}
}
