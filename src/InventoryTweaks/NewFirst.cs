using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

namespace InventoryTweaks
{
	/// <summary>
	/// Feature 5. A fifth button in UL's sort row. Left-click: while on, never-seen items (feature
	/// 3's highlighted stacks with no +/- marker) are kept at the front of the backpack, after every
	/// UL sort, on every real open, and the moment it is switched on. Right-click: changed stacks
	/// (the +/- ones) form a second tier behind the new ones. Each tier keeps the order of the sort
	/// it follows; when no sort ran (an open with no locked order, switching on) it is ordered by
	/// most recent change. The rest of the bag keeps its order and locked slots are never touched,
	/// the same way UL's own sorter leaves them.
	///
	/// The button is added to the window XML in memory just before XUi parses it. A Config XML
	/// patch cannot do this: patches apply in mod folder order and "InventoryTweaks" sorts before
	/// "UndeadLegacy", so UL's window would not exist yet when ours ran.
	/// </summary>
	internal static class NewFirst
	{
		internal const string ButtonId = "btnSortNewFirst";

		private const string IconSprite = "symbol_stack_exclamation";

		private const string ToolTip = "New items first (right-click: changed items next)";

		/// <summary>What the XML injection did, as reported by <c>it info</c>.</summary>
		internal static string ButtonStatus = Patches.NotRunYet;

		/// <summary>Prefix on <c>XUiFromXml.loadWindows(XmlFile)</c>: adds the button after UL's Name button.</summary>
		internal static void BeforeLoadWindows(XmlFile _xmlFile)
		{
			try
			{
				XElement nameButton = _xmlFile?.XmlDoc?.Root?
					.Descendants("window")
					.Where(w => (string)w.Attribute("name") == BackpackAccess.WindowName)
					.Descendants("ulmSort")
					.FirstOrDefault(b => (string)b.Attribute("id") == "btnSortByName");
				if (nameButton == null)
				{
					ButtonStatus = "NOT ADDED - windowBackpack has no btnSortByName";
					Log.Warning(Patches.LogPrefix + "Could not find UL's sort buttons in windows.xml, so the "
						+ "new-items-first button will be missing; 'it newfirst' still works.");
					return;
				}
				if (nameButton.Parent.Elements("ulmSort").Any(b => (string)b.Attribute("id") == ButtonId))
				{
					ButtonStatus = "added - already present";
					return;
				}
				nameButton.AddAfterSelf(new XElement("ulmSort",
					new XAttribute("id", ButtonId),
					new XAttribute("sprite", IconSprite),
					new XAttribute("tooltip_key", ToolTip)));
				ButtonStatus = "added - after btnSortByName";
				Log.Out(Patches.LogPrefix + "Added the new-items-first button to windowBackpack.");
			}
			catch (Exception e)
			{
				ButtonStatus = "NOT ADDED - " + e.Message;
				Log.Warning(Patches.LogPrefix + "Could not add the new-items-first button: " + e.Message);
			}
		}

		/// <summary>From <see cref="SortLock.AfterInit"/>: wires the button once the window exists.</summary>
		internal static void AfterInit(XUiC_ULM_BackpackWindow _window)
		{
			XUiController button = _window.GetChildById(ButtonId);
			if (button == null)
			{
				return;
			}
			button.OnPress += OnPress;
			button.OnRightPress += OnRightPress;
			if (button.ViewComponent is XUiV_Button view)
			{
				view.ToolTip = ToolTip;
			}
		}

		private static bool Usable(XUiController _sender)
		{
			if (!Settings.Enabled || _sender?.ViewComponent == null)
			{
				return false;
			}
			// UL greys the sort row out under the Shuffled Backpack debuff.
			return !(_sender.ViewComponent is XUiV_Button view) || view.Enabled;
		}

		private static void OnPress(XUiController _sender, int _mouseButton)
		{
			if (Usable(_sender))
			{
				Toggle(BackpackAccess.Window(_sender.xui));
			}
		}

		private static void OnRightPress(XUiController _sender, int _mouseButton)
		{
			if (Usable(_sender))
			{
				ToggleChanged(BackpackAccess.Window(_sender.xui));
			}
		}

		/// <summary>Left-click: flips new-first, saves, refreshes the button and applies the order if now on.</summary>
		internal static void Toggle(XUiC_ULM_BackpackWindow _window)
		{
			Settings.NewFirst = !Settings.NewFirst;
			AfterToggle(_window);
		}

		/// <summary>
		/// Right-click: flips the changed tier. Switching it on while new-first is off switches
		/// new-first on too, since the tier only exists behind the new items.
		/// </summary>
		internal static void ToggleChanged(XUiC_ULM_BackpackWindow _window)
		{
			Settings.ChangedFirst = !Settings.ChangedFirst;
			if (Settings.ChangedFirst)
			{
				Settings.NewFirst = true;
			}
			AfterToggle(_window);
		}

		private static void AfterToggle(XUiC_ULM_BackpackWindow _window)
		{
			Config.Save();
			ApplyIndicator(_window);
			if (Settings.NewFirst && _window != null && _window.IsOpen && Apply(_window, _afterSort: false))
			{
				ScrollToTop(_window);
			}
		}

		/// <summary>
		/// Selected sprite while on. The icon is the lock's gold for new-first alone and the
		/// highlight colour once the changed tier is on as well.
		/// </summary>
		internal static void ApplyIndicator(XUiC_ULM_BackpackWindow _window)
		{
			XUiController button = _window?.GetChildById(ButtonId);
			if (button == null)
			{
				return;
			}
			bool on = Settings.Enabled && Settings.NewFirst;
			if (button.ViewComponent is XUiV_Button view)
			{
				view.Selected = on;
			}
			Color32 color = on && Settings.ChangedFirst ? Settings.HighlightColor : SortLock.LockedIconColor;
			SortLock.TintIcon(button, on, color);
		}

		/// <summary>Postfix on <c>ULM_StackSorter.OnBtnSort</c>: after UL sorted the backpack.</summary>
		internal static void AfterSort(ULM_StackSorter __instance)
		{
			if (__instance == null || __instance.id != "Player")
			{
				return;
			}
			Apply(BackpackAccess.Window(__instance.xui), _afterSort: true);
		}

		/// <summary>Postfix on <c>XUiC_ULM_BackpackWindow.BtnSort_OnPress</c>: the standard-controls sort.</summary>
		internal static void AfterStandardSort(XUiC_ULM_BackpackWindow __instance)
		{
			Apply(__instance, _afterSort: true);
		}

		/// <summary>
		/// A real open. If the locked sort ran, its postfix already applied the tiers in sorted
		/// order and there is nothing to do; otherwise apply by most recent change. Same guards as
		/// the locked sort so nothing moves under the cursor.
		/// </summary>
		internal static void OnWindowOpened(XUiC_ULM_BackpackWindow _window, bool _realOpen, bool _sortedAlready)
		{
			if (!_realOpen || _sortedAlready || !Settings.Enabled || !Settings.NewFirst)
			{
				return;
			}
			XUi xui = _window.xui;
			if (xui?.dragAndDrop != null && !xui.dragAndDrop.CurrentStack.IsEmpty())
			{
				return;
			}
			if (Apply(_window, _afterSort: false))
			{
				ScrollToTop(_window);
			}
		}

		/// <summary>
		/// Moves the new stacks, then (when the changed tier is on) the changed stacks, to the front
		/// of the movable slots, everything else following in its current order. Within a tier the
		/// current order is kept after a sort; otherwise the most recently changed comes first.
		/// Locked and attribute-locked slots keep their stack. Returns whether anything moved.
		/// </summary>
		internal static bool Apply(XUiC_ULM_BackpackWindow _window, bool _afterSort)
		{
			if (!Settings.Enabled || !Settings.NewFirst || _window == null)
			{
				return false;
			}
			XUiC_ULM_BackpackGrid grid = BackpackAccess.Grid(_window);
			if (grid == null || grid.isShuffledBackpack)
			{
				return false;
			}
			XUiC_ItemStack[] cells = BackpackAccess.Cells(_window);
			XUi xui = _window.xui;
			ItemStack[] stacks = xui?.PlayerInventory?.GetBackpackItemStacks();
			if (cells == null || stacks == null)
			{
				return false;
			}

			int n = Math.Min(stacks.Length, cells.Length);
			bool[] fixedSlot = new bool[n];
			for (int i = 0; i < n; i++)
			{
				fixedSlot[i] = cells[i] != null && (cells[i].UserLockedSlot || cells[i].AttributeLock);
			}

			// The movable stacks by tier: 0 new, 1 changed (or rest), 2 rest.
			List<int>[] tiers = { new List<int>(), new List<int>(), new List<int>() };
			for (int i = 0; i < n; i++)
			{
				if (fixedSlot[i] || stacks[i] == null || stacks[i].IsEmpty())
				{
					continue;
				}
				int t = NewItemTracker.IsNew(i) ? 0 : Settings.ChangedFirst && NewItemTracker.IsChanged(i) ? 1 : 2;
				tiers[t].Add(i);
			}
			if (!_afterSort)
			{
				// No sort order to follow: most recently changed first. Stable, so ties keep their place.
				for (int t = 0; t < 2; t++)
				{
					tiers[t] = tiers[t].OrderByDescending(NewItemTracker.LastChange).ToList();
				}
			}

			ItemStack[] result = new ItemStack[stacks.Length];
			bool changed = false;
			int tier = 0;
			int next = 0;
			for (int i = 0; i < stacks.Length; i++)
			{
				if (i >= n || fixedSlot[i])
				{
					result[i] = stacks[i];
					continue;
				}
				while (tier < tiers.Length && next >= tiers[tier].Count)
				{
					tier++;
					next = 0;
				}
				if (tier < tiers.Length)
				{
					result[i] = stacks[tiers[tier][next++]];
					if (!ReferenceEquals(result[i], stacks[i]))
					{
						changed = true;
					}
				}
				else
				{
					result[i] = ItemStack.Empty.Clone();
					if (stacks[i] != null && !stacks[i].IsEmpty())
					{
						changed = true;
					}
				}
			}
			if (!changed)
			{
				return false;
			}

			// The same housekeeping UL's sorter does around its own SetBackpackItemStacks.
			ItemStack assembling = xui.AssembleItem?.CurrentItemStackController?.ItemStack;
			xui.PlayerInventory.SetBackpackItemStacks(result);
			if (assembling != null)
			{
				grid.AssembleLockSingleStack(assembling);
			}
			Counters.NewFirstSorts++;
			return true;
		}

		private static void ScrollToTop(XUiC_ULM_BackpackWindow _window)
		{
			if (Settings.ScrollToTop && BackpackAccess.ScrollToTop(_window.scrollBar))
			{
				Counters.SortsScrolled++;
			}
		}
	}
}
