namespace InventoryTweaks
{
	/// <summary>
	/// The four sort orders Undead Legacy offers, by the short name the console and the settings
	/// file use and by the XUi id of the button that triggers each. UL has no enum for these: its
	/// sorter switches on the button's id string, so that string is the mode.
	/// </summary>
	internal static class SortModes
	{
		internal static readonly string[] Names = { "weight", "price", "group", "name" };

		internal static readonly string[] ButtonIds =
		{
			"btnSortByWeight", "btnSortByPrice", "btnSortByGroup", "btnSortByName"
		};

		/// <summary>The button id for a mode name, or null.</summary>
		internal static string ToButtonId(string _name)
		{
			for (int i = 0; i < Names.Length; i++)
			{
				if (Names[i] == _name)
				{
					return ButtonIds[i];
				}
			}
			return null;
		}

		/// <summary>The mode name for a button id, or null.</summary>
		internal static string FromButtonId(string _id)
		{
			for (int i = 0; i < ButtonIds.Length; i++)
			{
				if (ButtonIds[i] == _id)
				{
					return Names[i];
				}
			}
			return null;
		}

		internal static bool IsName(string _name)
		{
			return ToButtonId(_name) != null;
		}
	}
}
