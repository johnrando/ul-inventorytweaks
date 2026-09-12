using UnityEngine;

namespace InventoryTweaks
{
	/// <summary>
	/// Open / close / per-frame hooks on UL's backpack window, shared by features 2 and 3.
	///
	/// UL's tab bar closes and reopens <c>windowBackpack</c> when the player switches between
	/// crafting, character, and so on. An open that follows a close within
	/// <see cref="TabSwitchSeconds"/> is treated as that, not as the player coming back to the
	/// inventory: it neither re-sorts nor ages the highlights.
	/// </summary>
	internal static class WindowLifecycle
	{
		private const float TabSwitchSeconds = 0.5f;

		private static float lastCloseTime = -1000f;

		/// <summary>Postfix on <c>XUiC_ULM_BackpackWindow.OnOpen()</c>. Runs after the grid's cells are filled.</summary>
		internal static void AfterOpen(XUiC_ULM_BackpackWindow __instance)
		{
			bool realOpen = Time.unscaledTime - lastCloseTime > TabSwitchSeconds;
			NewItemTracker.OnWindowOpened(__instance, realOpen);
			SortLock.OnWindowOpened(__instance, realOpen);
		}

		/// <summary>Postfix on <c>XUiC_ULM_BackpackWindow.OnClose()</c>.</summary>
		internal static void AfterClose(XUiC_ULM_BackpackWindow __instance)
		{
			lastCloseTime = Time.unscaledTime;
			NewItemTracker.OnWindowClosed();
		}

		/// <summary>Postfix on <c>XUiC_ULM_BackpackWindow.Update(float)</c>.</summary>
		internal static void AfterUpdate(XUiC_ULM_BackpackWindow __instance)
		{
			NewItemTracker.Tick(__instance);
		}
	}
}
