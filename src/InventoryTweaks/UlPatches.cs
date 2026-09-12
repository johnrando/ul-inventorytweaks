using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace InventoryTweaks
{
	/// <summary>
	/// Installs every Harmony patch. All targets are Undead Legacy members (plus vanilla
	/// <c>Bag.onBackpackChanged</c>), resolved by name and gated individually, so a UL update that
	/// moves one degrades to a log line and an <c>it info</c> status rather than an exception.
	///
	/// Only referenced from <see cref="Patches.Apply"/> after UL is confirmed present - this is the
	/// class that mentions UL types, so it must not be JIT-compiled without them.
	/// </summary>
	internal static class UlPatches
	{
		/// <summary>Outcome of each patch, as reported by <c>it info</c>.</summary>
		internal static string SortScrollStatus = Patches.NotRunYet;

		internal static string StandardSortScrollStatus = Patches.NotRunYet;

		internal static string SortLockStatus = Patches.NotRunYet;

		internal static string WindowOpenStatus = Patches.NotRunYet;

		internal static string WindowCloseStatus = Patches.NotRunYet;

		internal static string WindowUpdateStatus = Patches.NotRunYet;

		internal static string BagChangeStatus = Patches.NotRunYet;

		internal static string HighlightStatus = Patches.NotRunYet;

		internal static string MarkerStatus = Patches.NotRunYet;

		internal static void MarkAllSkipped(string _status)
		{
			SortScrollStatus = _status;
			StandardSortScrollStatus = _status;
			SortLockStatus = _status;
			WindowOpenStatus = _status;
			WindowCloseStatus = _status;
			WindowUpdateStatus = _status;
			BagChangeStatus = _status;
			HighlightStatus = _status;
			MarkerStatus = _status;
		}

		internal static void Install(Harmony _harmony)
		{
			BackpackAccess.Resolve();
			NewItemTracker.RegisterLifecycle();

			// Feature 1: every UL sort (backpack, loot container, vehicle) scrolls back to the top.
			SortScrollStatus = Postfix(_harmony, typeof(ULM_StackSorter), "OnBtnSort",
				typeof(SortScrollToTop), nameof(SortScrollToTop.AfterSort),
				"sorting scrolls the list to the top");

			// The vanilla-style sort button (standard controls) takes a different path.
			StandardSortScrollStatus = Postfix(_harmony, typeof(XUiC_ULM_BackpackWindow), "BtnSort_OnPress",
				typeof(SortScrollToTop), nameof(SortScrollToTop.AfterStandardSort),
				"the standard-controls sort also scrolls to the top");

			// Feature 2: right-click on the four sort buttons, wired once the window exists.
			SortLockStatus = Postfix(_harmony, typeof(XUiC_ULM_BackpackWindow), "Init",
				typeof(SortLock), nameof(SortLock.AfterInit),
				"right-click on a sort button locks that order");

			// Features 2 + 3: window lifecycle.
			WindowOpenStatus = Postfix(_harmony, typeof(XUiC_ULM_BackpackWindow), "OnOpen",
				typeof(WindowLifecycle), nameof(WindowLifecycle.AfterOpen),
				"opening the inventory re-sorts when locked and ages unseen highlights");
			WindowCloseStatus = Postfix(_harmony, typeof(XUiC_ULM_BackpackWindow), "OnClose",
				typeof(WindowLifecycle), nameof(WindowLifecycle.AfterClose),
				"closing the inventory marks what was left unseen");
			WindowUpdateStatus = Postfix(_harmony, typeof(XUiC_ULM_BackpackWindow), "Update",
				typeof(WindowLifecycle), nameof(WindowLifecycle.AfterUpdate),
				"hovering a highlighted cell clears it");

			// Feature 3: what changed in the bag, and how a cell draws it.
			BagChangeStatus = Postfix(_harmony, typeof(Bag), "onBackpackChanged",
				typeof(NewItemTracker), nameof(NewItemTracker.AfterBagChanged),
				"new items in the backpack are tracked");
			HighlightStatus = Postfix(_harmony, typeof(XUiC_ULM_ItemStack), "GetSelectionColor",
				typeof(NewItemHighlight), nameof(NewItemHighlight.AfterSelectionColor),
				"unseen items draw a coloured frame");
			MarkerStatus = Postfix(_harmony, typeof(XUiC_ULM_ItemStack), "GetBindingValueInternal",
				typeof(NewItemHighlight), nameof(NewItemHighlight.AfterBinding),
				"changed stack counts carry +/- markers");
		}

		/// <summary>
		/// One postfix. Resolves the target by name (UL's DLL is not publicized, so private members
		/// still resolve through AccessTools), reports and logs either way.
		/// </summary>
		private static string Postfix(Harmony _harmony, Type _targetType, string _targetName,
			Type _patchType, string _patchName, string _effect)
		{
			string site = _targetType.Name + "." + _targetName;
			MethodInfo target = AccessTools.DeclaredMethod(_targetType, _targetName);
			if (target == null)
			{
				Log.Error(Patches.LogPrefix + "Patch NOT applied: " + site + " could not be found, so "
					+ _effect + " will not work.");
				return "NOT APPLIED - " + site + " not found";
			}

			MethodInfo patch = AccessTools.DeclaredMethod(_patchType, _patchName);
			if (patch == null)
			{
				Log.Error(Patches.LogPrefix + "Patch NOT applied: own method " + _patchType.Name + "."
					+ _patchName + " is missing - this is a build error in the mod.");
				return "NOT APPLIED - patch method missing";
			}

			try
			{
				_harmony.Patch(target, postfix: new HarmonyMethod(patch));
			}
			catch (Exception e)
			{
				Log.Error(Patches.LogPrefix + "Patch NOT applied on " + site + ": " + e.Message);
				return "NOT APPLIED - " + e.GetType().Name + " on " + site;
			}

			Log.Out(Patches.LogPrefix + "Patched " + site + ": " + _effect + ".");
			return "applied - postfix on " + site;
		}
	}

	/// <summary>
	/// The one place that knows how Undead Legacy's backpack window is put together: how to reach
	/// the window from any controller, its private sorter, its scrollbar, its grid and cells.
	/// </summary>
	internal static class BackpackAccess
	{
		internal const string WindowName = "windowBackpack";

		private static FieldInfo sorterField;

		/// <summary>Whether the private <c>stackSorter</c> field resolved; feature 2 needs it.</summary>
		internal static string SorterStatus = Patches.NotRunYet;

		internal static void Resolve()
		{
			sorterField = AccessTools.Field(typeof(XUiC_ULM_BackpackWindow), "stackSorter");
			SorterStatus = sorterField != null
				? "resolved"
				: "NOT FOUND - XUiC_ULM_BackpackWindow.stackSorter; locked sort cannot sort";
			if (sorterField == null)
			{
				Log.Error(Patches.LogPrefix + "XUiC_ULM_BackpackWindow.stackSorter not found: a locked sort "
					+ "order can be set but will not be able to sort.");
			}
		}

		internal static XUiC_ULM_BackpackWindow Window(XUi _xui)
		{
			XUiV_Window window = _xui?.GetWindow(WindowName);
			return window?.Controller as XUiC_ULM_BackpackWindow;
		}

		internal static ULM_StackSorter Sorter(XUiC_ULM_BackpackWindow _window)
		{
			if (_window == null || sorterField == null)
			{
				return null;
			}
			return sorterField.GetValue(_window) as ULM_StackSorter;
		}

		internal static XUiC_ULM_BackpackGrid Grid(XUiC_ULM_BackpackWindow _window)
		{
			return _window?.GetChildByType<XUiC_ULM_BackpackGrid>();
		}

		/// <summary>The backpack cells, indexed by slot (the grid assigns SlotNumber = index).</summary>
		internal static XUiC_ItemStack[] Cells(XUiC_ULM_BackpackWindow _window)
		{
			return Grid(_window)?.GetItemStackControllers();
		}

		/// <summary>True while the window is actually being drawn.</summary>
		internal static bool IsShowing(XUiC_ULM_BackpackWindow _window)
		{
			return _window != null && _window.WindowGroup != null && _window.WindowGroup.isShowing
				&& _window.ViewComponent != null && _window.ViewComponent.IsVisible;
		}

		/// <summary>
		/// UL's "scrollbar" is row paging: <c>page</c> picks which rows of the grid are visible.
		/// Page 0 is the top. Returns whether anything moved.
		/// </summary>
		internal static bool ScrollToTop(ULM_StackScrollBar _scrollBar)
		{
			if (_scrollBar == null || _scrollBar.page == 0)
			{
				return false;
			}
			_scrollBar.page = 0;
			_scrollBar.UpdateSlots();
			_scrollBar.UpdateScrollBarPosition();
			return true;
		}

		/// <summary>Forces every cell to re-evaluate its bindings next frame (cheap: 300 flags).</summary>
		internal static void DirtyCells(XUiC_ULM_BackpackWindow _window)
		{
			XUiC_ItemStack[] cells = Cells(_window);
			if (cells == null)
			{
				return;
			}
			for (int i = 0; i < cells.Length; i++)
			{
				if (cells[i] != null)
				{
					cells[i].IsDirty = true;
				}
			}
		}

		internal static bool SameColor(Color32 _a, Color32 _b)
		{
			return _a.r == _b.r && _a.g == _b.g && _a.b == _b.b && _a.a == _b.a;
		}
	}
}
