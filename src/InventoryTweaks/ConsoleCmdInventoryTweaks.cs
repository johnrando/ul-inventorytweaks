using System.Collections.Generic;
using UnityEngine;

namespace InventoryTweaks
{
	/// <summary>
	/// <c>it</c> (or <c>inventorytweaks</c>). The bare command prints the settings block and
	/// changes nothing; every line names the command that changes it, so it doubles as the menu.
	/// <c>it info</c> adds the diagnostics and counters that answer "is this thing working".
	/// </summary>
	public class ConsoleCmdInventoryTweaks : ConsoleCmdAbstract
	{
		public override bool IsExecuteOnClient => false;

		public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
		{
			string command = _params.Count > 0 ? _params[0].ToLower() : string.Empty;

			switch (command)
			{
			case "":
				OutputMenu("InventoryTweaks is " + OnOff(Settings.Enabled));
				return;

			case "on":
			case "off":
				SetEnabled(command == "on");
				return;

			case "scroll":
				Settings.ScrollToTop = !Settings.ScrollToTop;
				Config.Save();
				Output("Scroll to top on sort " + OnOff(Settings.ScrollToTop) + ".");
				return;

			case "containers":
				Settings.ScrollContainers = !Settings.ScrollContainers;
				Config.Save();
				Output("Scroll to top in loot and vehicle windows " + OnOff(Settings.ScrollContainers) + ".");
				return;

			case "lock":
				SetLock(_params);
				return;

			case "autosort":
				Settings.AutoSort = !Settings.AutoSort;
				Config.Save();
				Output("Sort on open while locked " + OnOff(Settings.AutoSort) + ".");
				return;

			case "highlight":
				Settings.Highlight = !Settings.Highlight;
				Config.Save();
				if (!Settings.Highlight)
				{
					NewItemTracker.Clear();
				}
				Output("New-item highlight " + OnOff(Settings.Highlight) + ".");
				return;

			case "color":
			case "colour":
				SetColor(_params);
				return;

			case "markers":
				Settings.Markers = !Settings.Markers;
				Config.Save();
				RefreshWindow();
				Output("Stack-count markers " + OnOff(Settings.Markers) + ".");
				return;

			case "clear":
				NewItemTracker.Clear();
				Output("Highlights cleared.");
				return;

			case "info":
				OutputInfo();
				return;

			case "reset":
				Counters.Reset();
				Output("Counters reset.");
				return;

			default:
				Output("Unknown option '" + _params[0]
					+ "'. Try: it [on|off|scroll|containers|lock|autosort|highlight|color|markers|clear|info|reset]");
				return;
			}
		}

		private static void OutputMenu(string _header)
		{
			Output(_header);
			Switch("it on|off", EnabledChoices(), "master switch");
			Switch("it scroll", OnOffChoices(Settings.ScrollToTop), "sorting scrolls the backpack to the top");
			Switch("it containers", OnOffChoices(Settings.ScrollContainers), "...and loot / vehicle windows too");
			Switch("it lock {mode}", LockChoices(), "locked sort - or right-click a sort button");
			Switch("it autosort", OnOffChoices(Settings.AutoSort), "re-sort on open while locked");
			Switch("it highlight", OnOffChoices(Settings.Highlight), "frame new or changed items until hovered");
			Line("it color {r,g,b,a}", Config.Color(Settings.HighlightColor));
			Switch("it markers", OnOffChoices(Settings.Markers), "+/- before a changed stack count");
		}

		private static void SetEnabled(bool _on)
		{
			bool changed = Settings.Enabled != _on;
			Settings.Enabled = _on;
			if (changed)
			{
				Config.Save();
				RefreshWindow();
			}
			OutputMenu("InventoryTweaks is " + (changed ? "now " : "already ") + OnOff(_on));
		}

		private static void SetLock(List<string> _params)
		{
			if (_params.Count != 2)
			{
				Output("Usage: it lock {none|weight|price|group|name} - currently: " + LockLine());
				return;
			}
			if (!Config.TryLock(_params[1]))
			{
				Output("'" + _params[1] + "' is not a sort order - none, weight, price, group or name.");
				return;
			}
			Config.Save();
			RefreshWindow();
			Output("Locked sort: " + LockLine()
				+ (Settings.LockedSort.Length > 0 ? " - the backpack will sort on every open." : "."));
		}

		private static void SetColor(List<string> _params)
		{
			if (_params.Count != 2)
			{
				Output("Usage: it color {r,g,b[,a]} - currently: " + Config.Color(Settings.HighlightColor));
				return;
			}
			if (!Config.TryColor(_params[1], out Color32 color))
			{
				Output("Colours are r,g,b or r,g,b,a with each channel 0-255, like 255,200,60,255.");
				return;
			}
			Settings.HighlightColor = color;
			Config.Save();
			RefreshWindow();
			Output("Highlight colour: " + Config.Color(Settings.HighlightColor));
		}

		/// <summary>Re-applies the lock indicator on an open window after a console change.</summary>
		private static void RefreshWindow()
		{
			if (!UndeadLegacyInfo.Present)
			{
				return;
			}
			try
			{
				RefreshWindowUl();
			}
			catch (System.Exception e)
			{
				Log.Warning(Patches.LogPrefix + "Could not refresh the inventory window: " + e.Message);
			}
		}

		/// <summary>Kept separate so this method is only JIT-compiled with UL present.</summary>
		private static void RefreshWindowUl()
		{
			XUiC_ULM_BackpackWindow window = BackpackAccess.Window(LocalPlayerUI.primaryUI?.xui);
			if (window == null)
			{
				return;
			}
			SortLock.ApplyIndicator(window);
			BackpackAccess.DirtyCells(window);
		}

		private static string OnOff(bool _on)
		{
			return _on ? "ON" : "OFF";
		}

		private static void OutputInfo()
		{
			OutputMenu("InventoryTweaks is " + OnOff(Settings.Enabled));
			Line("settings file", Config.Status);
			Line("Undead Legacy", UndeadLegacyInfo.Status);
			Line("sorter field", BackpackAccess.SorterStatus);
			Line("sort scroll", UlPatches.SortScrollStatus);
			Line("std sort scroll", UlPatches.StandardSortScrollStatus);
			Line("sort lock", UlPatches.SortLockStatus);
			Line("window open", UlPatches.WindowOpenStatus);
			Line("window close", UlPatches.WindowCloseStatus);
			Line("window update", UlPatches.WindowUpdateStatus);
			Line("bag changes", UlPatches.BagChangeStatus);
			Line("highlight draw", UlPatches.HighlightStatus);
			Line("count markers", UlPatches.MarkerStatus);
			Line("sorts scrolled", Counters.SortsScrolled + " (" + Counters.AutoSorts + " automatic)");
			Line("bag changes seen", Counters.BagChanges.ToString());
			Line("items flagged", Counters.ItemsFlagged + " flagged, " + Counters.ItemsSeen + " cleared by hover");
			Line("highlighted now", NewItemTracker.HighlightedCount().ToString());

			if (Counters.BagChanges == 0)
			{
				Output("Note: no backpack change has reached the tracker yet. Picking anything up should");
				Output("move 'bag changes seen' - if it stays at zero, the hook is not live.");
			}
		}

		private static void Line(string _label, string _value)
		{
			Output("  " + _label.PadRight(26) + ": " + _value);
		}

		private static void Switch(string _label, string _choices, string _note)
		{
			Line(_label, _choices.PadRight(44) + " - " + _note);
		}

		private static string Choices(params string[] _options)
		{
			return "[ " + string.Join(" | ", _options) + " ]";
		}

		private static string Mark(string _option, bool _live)
		{
			return _live ? ">" + _option + "<" : _option;
		}

		private static string EnabledChoices()
		{
			return Choices(Mark("on", Settings.Enabled), Mark("off", !Settings.Enabled));
		}

		private static string OnOffChoices(bool _on)
		{
			return Choices(Mark("on", _on), Mark("off", !_on));
		}

		private static string LockChoices()
		{
			string[] options = new string[SortModes.Names.Length + 1];
			options[0] = Mark("none", Settings.LockedSort.Length == 0);
			for (int i = 0; i < SortModes.Names.Length; i++)
			{
				options[i + 1] = Mark(SortModes.Names[i], Settings.LockedSort == SortModes.Names[i]);
			}
			return Choices(options);
		}

		private static string LockLine()
		{
			return Settings.LockedSort.Length == 0 ? "none" : Settings.LockedSort;
		}

		private static void Output(string _line)
		{
			SdtdConsole.Instance.Output(_line);
		}

		public override string[] getCommands()
		{
			return new string[2] { "it", "inventorytweaks" };
		}

		public override string getDescription()
		{
			return "Reports InventoryTweaks' settings; 'it on' and 'it off' switch it.";
		}

		public override string getHelp()
		{
			return "Usage: it [on|off|scroll|containers|lock {mode}|autosort|highlight|color {r,g,b,a}"
				+ "|markers|clear|info|reset]"
				+ "\r\n\r\nInventory quality-of-life for Undead Legacy's backpack. 'it' on its own prints "
				+ "the settings and changes nothing. Each line names the command that changes it, so "
				+ "the settings block is also the menu."
				+ "\r\n\r\n'it on' and 'it off' are the master switch; off, every hook returns immediately."
				+ "\r\n\r\n'it scroll' toggles whether clicking a sort button scrolls the backpack back to "
				+ "the top, so the sorted result is in view. 'it containers' extends that to the loot "
				+ "container and vehicle storage windows, which share UL's sorter."
				+ "\r\n\r\n'it lock {mode}' locks a sort order: none, weight, price, group or name. In game, "
				+ "right-click a sort button to lock it (the button lights up), right-click it again "
				+ "to unlock. While locked, 'it autosort' (on by default) re-sorts the backpack every "
				+ "time the inventory is opened - never while it is open, never while you are holding "
				+ "an item, and not on a plain tab switch between crafting, character and the like."
				+ "\r\n\r\n'it highlight' toggles the new-item frame. An item that arrives in the backpack, "
				+ "or a stack whose count changes, keeps a coloured frame until you hover its cell or "
				+ "close the inventory (a tab switch does not count). 'it markers' toggles the +/- "
				+ "before a changed stack count: one for a change of a single item, three once the "
				+ "stack has reached three times what you last saw (or a third of it), two in between. "
				+ "'it color {r,g,b,a}' sets the frame colour. 'it clear' drops every highlight. "
				+ "Highlights live in memory only and reset when the game restarts."
				+ "\r\n\r\nEvery setting is written straight to a settings file in the game's user data "
				+ "folder next to Saves, so it survives a restart and a mod update. 'it info' prints "
				+ "its path, the state of each patch and the counters; 'it reset' zeroes the counters. "
				+ "'inventorytweaks' is an alias for 'it'.";
		}
	}
}
