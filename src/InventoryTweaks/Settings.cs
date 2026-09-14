using UnityEngine;

namespace InventoryTweaks
{
	/// <summary>
	/// Runtime knobs, all switchable from the <c>it</c> console command. The values here are the
	/// built-in defaults; <see cref="Config"/> reads the player's file over them at startup.
	/// </summary>
	internal static class Settings
	{
		/// <summary>Master switch. When off every hook returns immediately.</summary>
		internal static bool Enabled = true;

		/// <summary>Sorting the backpack jumps the list back to the top.</summary>
		internal static bool ScrollToTop = true;

		/// <summary>...and the same in loot-container and vehicle-storage windows.</summary>
		internal static bool ScrollContainers = true;

		/// <summary>
		/// The locked sort order, one of <see cref="SortModes"/>' names, or empty for none. Set by
		/// right-clicking a sort button or by <c>it lock</c>. Persisted.
		/// </summary>
		internal static string LockedSort = "";

		/// <summary>While a sort is locked, re-sort every time the inventory is opened.</summary>
		internal static bool AutoSort = true;

		/// <summary>
		/// Keep never-seen items at the front of the backpack: after every sort and on every real
		/// open. Toggled by the fifth button in UL's sort row or <c>it newfirst</c>. Persisted.
		/// </summary>
		internal static bool NewFirst = false;

		/// <summary>
		/// With <see cref="NewFirst"/>: changed stacks (the +/- ones) form a second tier behind the
		/// new ones. Toggled by right-clicking the same button or <c>it changedfirst</c>. Persisted.
		/// </summary>
		internal static bool ChangedFirst = false;

		/// <summary>Pressing the key of the page already showing (B for character, and so on) closes the inventory.</summary>
		internal static bool ToggleClose = true;

		/// <summary>Frame items that arrived or changed count since the player last looked at them.</summary>
		internal static bool Highlight = true;

		/// <summary>Frame colour for highlighted cells.</summary>
		internal static Color32 HighlightColor = new Color32(255, 200, 60, 255);

		/// <summary>Prefix the stack count of a highlighted stackable with +/- markers.</summary>
		internal static bool Markers = true;
	}
}
