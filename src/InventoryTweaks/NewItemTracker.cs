using UnityEngine;

namespace InventoryTweaks
{
	/// <summary>
	/// Feature 3, the state. Every backpack slot remembers the count the player last acknowledged
	/// (<c>Base</c>) and whether it is highlighted. A slot becomes highlighted when an item arrives
	/// in it or its count changes; the highlight clears when the player hovers the cell or closes
	/// the inventory (a real close, not UL's close-and-reopen on a tab switch). For stackables the
	/// difference between the current count and <c>Base</c> is shown as <c>+</c>/<c>-</c> markers.
	///
	/// The bag reports "something changed" with no payload, and UL's cells hold clones, so items
	/// are followed by fingerprint: a diff of the whole bag against the previous snapshot on every
	/// change. Permutations (sorting, dragging) carry a slot's state to wherever the item went;
	/// merges add their bases together and splits divide them, so only stock that came from - or
	/// went - outside the backpack shows as a change.
	/// </summary>
	internal static class NewItemTracker
	{
		private struct Print
		{
			internal int Type;

			internal ushort Quality;

			internal int Seed;

			internal int Count;

			internal bool IsEmpty => Type == 0 || Count <= 0;

			internal bool SameItem(Print _other)
			{
				return Type == _other.Type && Quality == _other.Quality && Seed == _other.Seed;
			}

			internal static Print Of(ItemStack _stack)
			{
				if (_stack == null || _stack.IsEmpty() || _stack.itemValue == null)
				{
					return default;
				}
				return new Print
				{
					Type = _stack.itemValue.type,
					Quality = _stack.itemValue.Quality,
					Seed = _stack.itemValue.Seed,
					Count = _stack.count
				};
			}
		}

		private struct Slot
		{
			internal Print Print;

			/// <summary>The count the player last acknowledged; markers are relative to this.</summary>
			internal int Base;

			internal bool Highlighted;

			/// <summary>Whether the marker can be shown: false for a stack the player never had before.</summary>
			internal bool HasBase;

			/// <summary>Was highlighted when the inventory closed; cleared if that close turns out to be real.</summary>
			internal bool SeenAtClose;
		}

		private static Slot[] slots;

		/// <summary>
		/// Set at spawn. Bag changes before it expires re-baseline rather than flag: the player's
		/// saved bag can land a moment after the spawn event, and that must not read as loot.
		/// </summary>
		private static float baselineGraceUntil = -1f;

		private static bool lifecycleRegistered;

		internal static bool IsHighlighted(int _slot)
		{
			return slots != null && _slot >= 0 && _slot < slots.Length && slots[_slot].Highlighted;
		}

		/// <summary>
		/// The marker for a slot: "" when the count is unchanged, else one to three <c>+</c> or
		/// <c>-</c>. One for a change of a single item, three once the count has reached three
		/// times the base (or fallen to a third of it), two for anything in between.
		/// </summary>
		internal static string Marker(int _slot)
		{
			if (!IsHighlighted(_slot) || !slots[_slot].HasBase)
			{
				return "";
			}
			int count = slots[_slot].Print.Count;
			int baseCount = slots[_slot].Base;
			if (count == baseCount || baseCount <= 0)
			{
				return "";
			}
			if (count > baseCount)
			{
				if (count - baseCount == 1)
				{
					return "+";
				}
				return count >= baseCount * 3 ? "+++" : "++";
			}
			if (baseCount - count == 1)
			{
				return "-";
			}
			return count * 3 <= baseCount ? "---" : "--";
		}

		/// <summary>How many slots currently carry a highlight, for <c>it info</c>.</summary>
		internal static int HighlightedCount()
		{
			int count = 0;
			if (slots != null)
			{
				for (int i = 0; i < slots.Length; i++)
				{
					if (slots[i].Highlighted)
					{
						count++;
					}
				}
			}
			return count;
		}

		internal static void RegisterLifecycle()
		{
			if (lifecycleRegistered)
			{
				return;
			}
			lifecycleRegistered = true;
			ModEvents.PlayerSpawnedInWorld.RegisterHandler(OnPlayerSpawned);
			ModEvents.GameShutdown.RegisterHandler(OnGameShutdown);
		}

		private static void OnPlayerSpawned(ref ModEvents.SPlayerSpawnedInWorldData _data)
		{
			if (!_data.IsLocalPlayer)
			{
				return;
			}
			slots = null;
			baselineGraceUntil = Time.unscaledTime + 3f;
			Bag bag = LocalBag();
			if (bag != null)
			{
				Snapshot(bag);
			}
		}

		private static void OnGameShutdown(ref ModEvents.SGameShutdownData _data)
		{
			slots = null;
			baselineGraceUntil = -1f;
		}

		private static Bag LocalBag()
		{
			EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
			return player?.bag;
		}

		/// <summary>Drop every highlight and marker (the <c>it clear</c> command, and a real close).</summary>
		internal static void Clear()
		{
			if (slots == null)
			{
				return;
			}
			for (int i = 0; i < slots.Length; i++)
			{
				Acknowledge(ref slots[i]);
			}
			DirtyOpenWindow();
		}

		/// <summary>The player has seen this slot as it is now.</summary>
		private static void Acknowledge(ref Slot _slot)
		{
			_slot.Highlighted = false;
			_slot.Base = _slot.Print.Count;
			_slot.HasBase = !_slot.Print.IsEmpty;
		}

		/// <summary>Record the bag as it is, nothing highlighted.</summary>
		private static void Snapshot(Bag _bag)
		{
			ItemStack[] stacks = _bag.GetSlots();
			if (stacks == null)
			{
				return;
			}
			slots = new Slot[stacks.Length];
			for (int i = 0; i < stacks.Length; i++)
			{
				slots[i].Print = Print.Of(stacks[i]);
				Acknowledge(ref slots[i]);
			}
		}

		/// <summary>Postfix on <c>Bag.onBackpackChanged()</c>: fires for every mutation of any bag.</summary>
		internal static void AfterBagChanged(Bag __instance)
		{
			Bag bag = LocalBag();
			if (bag == null || __instance != bag)
			{
				return;
			}
			Counters.BagChanges++;

			if (slots == null || Time.unscaledTime < baselineGraceUntil
				|| !Settings.Enabled || !Settings.Highlight)
			{
				// Nothing to compare against, still settling after spawn, or switched off: just keep
				// the snapshot current so switching on later does not flag the whole bag.
				Snapshot(bag);
				return;
			}

			ItemStack[] stacks = bag.GetSlots();
			if (stacks == null)
			{
				return;
			}

			Slot[] prev = slots;
			Slot[] next = new Slot[stacks.Length];
			bool[] prevMatched = new bool[prev.Length];
			bool[] nextResolved = new bool[stacks.Length];

			for (int i = 0; i < stacks.Length; i++)
			{
				next[i].Print = Print.Of(stacks[i]);
				if (next[i].Print.IsEmpty)
				{
					nextResolved[i] = true;
				}
			}

			// Pass 1: the same item still in the same slot.
			for (int i = 0; i < stacks.Length; i++)
			{
				if (nextResolved[i] || i >= prev.Length || prev[i].Print.IsEmpty
					|| !prev[i].Print.SameItem(next[i].Print))
				{
					continue;
				}
				Carry(prev, i, ref next[i], prevMatched);
				prevMatched[i] = true;
				nextResolved[i] = true;
			}

			// Pass 2: the same item moved to another slot (sort, drag, shift-click).
			for (int i = 0; i < stacks.Length; i++)
			{
				if (nextResolved[i])
				{
					continue;
				}
				int from = FindUnmatched(prev, prevMatched, next[i].Print);
				if (from < 0)
				{
					continue;
				}
				Carry(prev, from, ref next[i], prevMatched);
				prevMatched[from] = true;
				nextResolved[i] = true;
			}

			// Pass 3: a stack split off an existing one takes its share of that stack's base;
			// anything else is a stack the player never had, highlighted with no marker.
			for (int i = 0; i < stacks.Length; i++)
			{
				if (nextResolved[i])
				{
					continue;
				}
				int source = FindSplitSource(prev, next, next[i].Print);
				if (source >= 0)
				{
					int share = System.Math.Min(next[i].Print.Count, System.Math.Max(next[source].Base, 0));
					next[i].Base = share;
					next[i].HasBase = next[source].HasBase;
					next[i].Highlighted = next[source].Highlighted;
					next[i].SeenAtClose = next[source].SeenAtClose;
					next[source].Base -= share;
				}
				else
				{
					next[i].Base = next[i].Print.Count;
					next[i].HasBase = false;
					next[i].Highlighted = true;
					Counters.ItemsFlagged++;
				}
				nextResolved[i] = true;
			}

			// Anything whose count differs from what the player last saw is highlighted.
			bool changed = false;
			for (int i = 0; i < next.Length; i++)
			{
				if (next[i].Print.IsEmpty)
				{
					next[i].Highlighted = false;
					next[i].HasBase = false;
					next[i].Base = 0;
				}
				else if (next[i].HasBase && next[i].Print.Count != next[i].Base && !next[i].Highlighted)
				{
					next[i].Highlighted = true;
					next[i].SeenAtClose = false;
					Counters.ItemsFlagged++;
				}
				if (i >= prev.Length || next[i].Highlighted != prev[i].Highlighted
					|| next[i].Print.Count != prev[i].Print.Count || next[i].Base != prev[i].Base
					|| !next[i].Print.SameItem(prev[i].Print))
				{
					changed = true;
				}
			}

			slots = next;
			if (changed)
			{
				DirtyOpenWindow();
			}
		}

		/// <summary>
		/// The item in prev[_from] is now the next slot _target. Carry its state. If the count grew,
		/// absorb other unmatched stacks of the same item first (a merge, whose bases add up);
		/// whatever difference is left is stock that came from or went outside.
		/// </summary>
		private static void Carry(Slot[] _prev, int _from, ref Slot _target, bool[] _prevMatched)
		{
			_target.Highlighted = _prev[_from].Highlighted;
			_target.HasBase = _prev[_from].HasBase;
			_target.Base = _prev[_from].Base;
			_target.SeenAtClose = _prev[_from].SeenAtClose;

			int accounted = _prev[_from].Print.Count;
			while (_target.Print.Count > accounted)
			{
				int other = FindUnmatched(_prev, _prevMatched, _target.Print, _from);
				if (other < 0)
				{
					break;
				}
				_prevMatched[other] = true;
				accounted += _prev[other].Print.Count;
				_target.Base += _prev[other].Base;
				_target.Highlighted |= _prev[other].Highlighted;
				_target.HasBase |= _prev[other].HasBase;
				_target.SeenAtClose &= _prev[other].SeenAtClose;
			}
			if (_target.Print.Count != accounted)
			{
				// Stock arrived or left while the inventory was closed: not something the player saw.
				_target.SeenAtClose = false;
			}
		}

		/// <summary>An unmatched previous slot holding the same item, preferring an equal count.</summary>
		private static int FindUnmatched(Slot[] _prev, bool[] _matched, Print _print, int _exclude = -1)
		{
			int anyCount = -1;
			for (int j = 0; j < _prev.Length; j++)
			{
				if (j == _exclude || _matched[j] || _prev[j].Print.IsEmpty || !_prev[j].Print.SameItem(_print))
				{
					continue;
				}
				if (_prev[j].Print.Count == _print.Count)
				{
					return j;
				}
				if (anyCount < 0)
				{
					anyCount = j;
				}
			}
			return anyCount;
		}

		/// <summary>A slot of the same item whose stack shrank this round: a split source.</summary>
		private static int FindSplitSource(Slot[] _prev, Slot[] _next, Print _print)
		{
			for (int j = 0; j < _prev.Length && j < _next.Length; j++)
			{
				if (_prev[j].Print.IsEmpty || !_prev[j].Print.SameItem(_print))
				{
					continue;
				}
				if (_next[j].Print.SameItem(_print) && _next[j].Print.Count < _prev[j].Print.Count)
				{
					return j;
				}
			}
			return -1;
		}

		/// <summary>Per frame while the window is drawn: hovering a highlighted cell acknowledges it.</summary>
		internal static void Tick(XUiC_ULM_BackpackWindow _window)
		{
			if (slots == null || !Settings.Enabled || !Settings.Highlight || !BackpackAccess.IsShowing(_window))
			{
				return;
			}
			XUiC_ItemStack[] cells = BackpackAccess.Cells(_window);
			if (cells == null)
			{
				return;
			}
			for (int i = 0; i < cells.Length; i++)
			{
				XUiC_ItemStack cell = cells[i];
				if (cell == null || !cell.isOver)
				{
					continue;
				}
				int slot = cell.SlotNumber;
				if (slot < 0 || slot >= slots.Length || !slots[slot].Highlighted)
				{
					continue;
				}
				Acknowledge(ref slots[slot]);
				cell.IsDirty = true;
				Counters.ItemsSeen++;
			}
		}

		/// <summary>
		/// The inventory opened. A real open (not a tab switch) follows a real close, at which
		/// everything the player had in view is taken as seen; changes that arrived in between
		/// were flagged after the close, so they stay highlighted.
		/// </summary>
		internal static void OnWindowOpened(XUiC_ULM_BackpackWindow _window, bool _realOpen)
		{
			if (slots == null || !_realOpen)
			{
				return;
			}
			bool changed = false;
			for (int i = 0; i < slots.Length; i++)
			{
				if (slots[i].SeenAtClose && slots[i].Highlighted)
				{
					Acknowledge(ref slots[i]);
					changed = true;
				}
				slots[i].SeenAtClose = false;
			}
			if (changed)
			{
				BackpackAccess.DirtyCells(_window);
			}
		}

		/// <summary>The inventory closed: mark what was highlighted, to be cleared if this turns out to be a real close.</summary>
		internal static void OnWindowClosed()
		{
			if (slots == null)
			{
				return;
			}
			for (int i = 0; i < slots.Length; i++)
			{
				slots[i].SeenAtClose = slots[i].Highlighted;
			}
		}

		private static void DirtyOpenWindow()
		{
			LocalPlayerUI ui = LocalPlayerUI.primaryUI;
			XUiC_ULM_BackpackWindow window = BackpackAccess.Window(ui?.xui);
			if (window != null && window.IsOpen)
			{
				BackpackAccess.DirtyCells(window);
			}
		}
	}
}
