using System.Collections.Generic;
using UnityEngine;

namespace InventoryTweaks
{
	/// <summary>
	/// Feature 2. Right-clicking one of UL's four sort buttons locks that order: the backpack is
	/// sorted now and again every time the inventory window opens. Right-clicking the locked
	/// button unlocks. The locked button shows its selected sprite and a gold icon.
	///
	/// Right-click reaches <c>XUiController.OnRightPress</c>, a separate event from the
	/// <c>OnPress</c> UL's sorter listens on, so nothing of UL's has to be suppressed.
	/// </summary>
	internal static class SortLock
	{
		internal static readonly Color32 LockedIconColor = new Color32(222, 206, 163, byte.MaxValue);

		/// <summary>Each icon sprite's colour before we touched it, so unlocking restores it exactly.</summary>
		private static readonly Dictionary<XUiV_Sprite, Color> originalIconColors = new Dictionary<XUiV_Sprite, Color>();

		/// <summary>Postfix on <c>XUiC_ULM_BackpackWindow.Init()</c>.</summary>
		internal static void AfterInit(XUiC_ULM_BackpackWindow __instance)
		{
			for (int i = 0; i < SortModes.ButtonIds.Length; i++)
			{
				XUiController button = __instance.GetChildById(SortModes.ButtonIds[i]);
				if (button != null)
				{
					button.OnRightPress += OnRightPress;
				}
			}
			NewFirst.AfterInit(__instance);
			ApplyIndicator(__instance);
		}

		private static void OnRightPress(XUiController _sender, int _mouseButton)
		{
			if (!Settings.Enabled || _sender?.ViewComponent == null)
			{
				return;
			}
			if (_sender.ViewComponent is XUiV_Button view && !view.Enabled)
			{
				// UL greys the buttons out under the Shuffled Backpack debuff.
				return;
			}

			string mode = SortModes.FromButtonId(_sender.ViewComponent.ID);
			if (mode == null)
			{
				return;
			}

			XUiC_ULM_BackpackWindow window = BackpackAccess.Window(_sender.xui);
			if (Settings.LockedSort == mode)
			{
				Settings.LockedSort = "";
				Config.Save();
				ApplyIndicator(window);
				return;
			}

			Settings.LockedSort = mode;
			Config.Save();
			ApplyIndicator(window);
			RunLockedSort(window);
		}

		/// <summary>
		/// Sorts the backpack by the locked order through UL's own sorter, so locked slots, the
		/// assemble lock and the search filter are all honoured exactly as a left-click would.
		/// Feature 1's postfix then scrolls to the top. Returns whether a sort ran.
		/// </summary>
		internal static bool RunLockedSort(XUiC_ULM_BackpackWindow _window)
		{
			string buttonId = SortModes.ToButtonId(Settings.LockedSort);
			if (buttonId == null || _window == null)
			{
				return false;
			}
			ULM_StackSorter sorter = BackpackAccess.Sorter(_window);
			XUiController button = _window.GetChildById(buttonId);
			if (sorter == null || button == null)
			{
				return false;
			}
			sorter.OnBtnSort(button, -1);
			return true;
		}

		/// <summary>
		/// Sort on open, when it will not get in the way: a real open rather than a tab switch,
		/// nothing on the cursor, and the buttons not disabled by the shuffled-backpack debuff.
		/// Returns whether a sort ran, so feature 5 knows the bag is freshly ordered.
		/// </summary>
		internal static bool OnWindowOpened(XUiC_ULM_BackpackWindow _window, bool _realOpen)
		{
			ApplyIndicator(_window);
			if (!Settings.Enabled || !Settings.AutoSort || Settings.LockedSort.Length == 0)
			{
				return false;
			}
			if (!_realOpen)
			{
				Trace("skipped: treated as a tab switch (closed " + WindowLifecycle.SecondsSinceClose().ToString("0.00") + "s ago)");
				return false;
			}
			XUi xui = _window.xui;
			if (xui?.dragAndDrop != null && !xui.dragAndDrop.CurrentStack.IsEmpty())
			{
				Trace("skipped: an item is on the cursor");
				return false;
			}
			XUiC_ULM_BackpackGrid grid = BackpackAccess.Grid(_window);
			if (grid != null && grid.isShuffledBackpack)
			{
				Trace("skipped: shuffled backpack debuff");
				return false;
			}
			if (RunLockedSort(_window))
			{
				Counters.AutoSorts++;
				Trace("sorted by " + Settings.LockedSort);
				return true;
			}
			Trace("could not sort: sorter " + (BackpackAccess.Sorter(_window) == null ? "missing" : "ok")
				+ ", button " + (_window.GetChildById(SortModes.ToButtonId(Settings.LockedSort)) == null ? "missing" : "ok"));
			return false;
		}

		private static void Trace(string _what)
		{
			Log.Out(Patches.LogPrefix + "Sort on open: " + _what + ".");
		}

		/// <summary>Selected sprite plus gold icon on the locked button, defaults on the others.</summary>
		internal static void ApplyIndicator(XUiC_ULM_BackpackWindow _window)
		{
			if (_window == null)
			{
				return;
			}
			string lockedId = Settings.Enabled ? SortModes.ToButtonId(Settings.LockedSort) : null;
			for (int i = 0; i < SortModes.ButtonIds.Length; i++)
			{
				XUiController button = _window.GetChildById(SortModes.ButtonIds[i]);
				if (button == null)
				{
					continue;
				}
				bool locked = SortModes.ButtonIds[i] == lockedId;
				if (button.ViewComponent is XUiV_Button view)
				{
					view.Selected = locked;
				}
				TintIcon(button, locked, LockedIconColor);
			}
			NewFirst.ApplyIndicator(_window);
		}

		/// <summary>
		/// The ulmSort control is a rect holding the button and, beside it, the icon sprite. Tints
		/// the icon <paramref name="_color"/> when <paramref name="_on"/>, else restores its original.
		/// </summary>
		internal static void TintIcon(XUiController _button, bool _on, Color32 _color)
		{
			XUiController parent = _button.Parent;
			if (parent == null)
			{
				return;
			}
			List<XUiController> siblings = parent.Children;
			for (int i = 0; i < siblings.Count; i++)
			{
				if (siblings[i] == _button || !(siblings[i].ViewComponent is XUiV_Sprite icon))
				{
					continue;
				}
				if (!originalIconColors.TryGetValue(icon, out Color original))
				{
					original = icon.Color;
					originalIconColors[icon] = original;
				}
				icon.Color = _on ? (Color)_color : original;
			}
		}
	}
}
