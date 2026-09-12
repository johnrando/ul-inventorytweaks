using UnityEngine;

namespace InventoryTweaks
{
	/// <summary>
	/// Feature 3, the drawing. UL's cell has one colour-bindable overlay, the <c>selection</c>
	/// sprite (a frame, <c>ui_stack_hover</c>), whose colour is <c>GetSelectionColor()</c>. UL
	/// returns the cell's background colour - fully transparent - when nothing is going on. That
	/// is the only case we take over: hover, selection, holding, drag and the search overlay all
	/// keep UL's colour. The stack-count label (<c>{itemCount}</c>) gets the +/- marker prefixed.
	/// </summary>
	internal static class NewItemHighlight
	{
		/// <summary>Postfix on <c>XUiC_ULM_ItemStack.GetSelectionColor()</c>.</summary>
		internal static void AfterSelectionColor(XUiC_ULM_ItemStack __instance, ref Color32 __result)
		{
			if (!Settings.Enabled || !Settings.Highlight || __instance == null)
			{
				return;
			}
			if (!(__instance.Parent is XUiC_ULM_BackpackGrid))
			{
				return;
			}
			if (!BackpackAccess.SameColor(__result, __instance.backgroundColor))
			{
				return;
			}
			if (NewItemTracker.IsHighlighted(__instance.SlotNumber))
			{
				__result = Settings.HighlightColor;
			}
		}

		/// <summary>Postfix on <c>XUiC_ULM_ItemStack.GetBindingValueInternal(ref string, string)</c>.</summary>
		internal static void AfterBinding(XUiC_ULM_ItemStack __instance, ref string value, string bindingName,
			bool __result)
		{
			if (!__result || bindingName != "itemCount" || string.IsNullOrEmpty(value))
			{
				return;
			}
			if (!Settings.Enabled || !Settings.Highlight || !Settings.Markers || __instance == null
				|| !(__instance.Parent is XUiC_ULM_BackpackGrid))
			{
				return;
			}
			string marker = NewItemTracker.Marker(__instance.SlotNumber);
			if (marker.Length > 0)
			{
				value = marker + " " + value;
			}
		}
	}
}
