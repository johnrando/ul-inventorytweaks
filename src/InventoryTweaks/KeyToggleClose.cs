using HarmonyLib;

namespace InventoryTweaks
{
	/// <summary>
	/// Feature 4. Pressing a page's key again closes the inventory.
	///
	/// Vanilla's <c>XUiC_WindowSelector.openSelectorAndWindow</c> closes everything when the page
	/// asked for is the one already showing - so B (character) opens the bag and B closes it
	/// again. Undead Legacy replaces that method with a Harmony prefix of its own and drops the
	/// toggle, leaving only Tab and Escape to close.
	///
	/// Harmony runs every prefix even after one of them skips the original, so closing the
	/// inventory from a prefix does not work: UL's prefix runs next and opens it straight back
	/// up. Instead the prefix only records whether the page was already showing, UL's prefix
	/// runs as usual (re-opening a page that is already open does nothing), and the postfix then
	/// closes the inventory the way vanilla did.
	/// </summary>
	internal static class KeyToggleClose
	{
		internal const string SelectorWindow = "windowpaging";

		/// <summary>
		/// Prefix on <c>XUiC_WindowSelector.openSelectorAndWindow(string)</c>. Sets the state to
		/// true when this press should close the inventory.
		///
		/// Must run before UL's prefix (hence <c>Priority.First</c>): UL's prefix opens the paging
		/// window itself, so a prefix that ran after it would see every open as "already open" and
		/// close the inventory the moment the key opened it.
		/// </summary>
		[HarmonyPriority(Priority.First)]
		internal static void BeforeOpen(XUiC_WindowSelector __instance, string _selectedPage, out bool __state)
		{
			__state = false;
			if (!Settings.Enabled || !Settings.ToggleClose || __instance == null || _selectedPage == null)
			{
				return;
			}
			GUIWindowManager windowManager = __instance.xui?.playerUI?.windowManager;
			if (windowManager == null || __instance.OverrideClose
				|| !windowManager.IsWindowOpen(SelectorWindow)
				|| !__instance.SelectedName.EqualsCaseInsensitive(_selectedPage))
			{
				return;
			}
			// Never close while the player is holding an item: closing returns it to the bag,
			// which looks like a failed pickup.
			XUiC_DragAndDropWindow dragAndDrop = __instance.xui.dragAndDrop;
			if (dragAndDrop != null && !dragAndDrop.CurrentStack.IsEmpty())
			{
				return;
			}
			__state = true;
		}

		/// <summary>Postfix on the same method. Closes the inventory when the prefix said so.</summary>
		internal static void AfterOpen(XUiC_WindowSelector __instance, bool __state)
		{
			if (!__state)
			{
				return;
			}
			GUIWindowManager windowManager = __instance?.xui?.playerUI?.windowManager;
			if (windowManager == null)
			{
				return;
			}
			windowManager.CloseAllOpenWindows();
			if (windowManager.IsWindowOpen(SelectorWindow))
			{
				windowManager.Close(SelectorWindow);
			}
			Counters.KeyCloses++;
		}
	}
}
