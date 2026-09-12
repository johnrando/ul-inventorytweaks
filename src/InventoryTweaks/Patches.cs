using System;
using HarmonyLib;

namespace InventoryTweaks
{
	/// <summary>
	/// Entry point for patching. Everything that mentions an Undead Legacy type lives in
	/// <see cref="UlPatches"/>, which is only JIT-compiled once <see cref="UndeadLegacyInfo.Report"/>
	/// has confirmed the UL assembly is loaded - so with UL absent this class logs a warning and
	/// the mod stays inert instead of throwing a TypeLoadException at init.
	///
	/// No load order needs declaring: UL applies its patches as a BepInEx plugin before any
	/// IModApi.InitMod runs, and ModManager loads every mod assembly before calling any InitMod.
	/// </summary>
	internal static class Patches
	{
		internal const string LogPrefix = "[InventoryTweaks] ";

		internal const string HarmonyId = "InventoryTweaks";

		internal const string NotRunYet = "not applied - mod init has not run";

		private static bool applied;

		internal static void Apply()
		{
			if (applied)
			{
				return;
			}
			applied = true;
			try
			{
				UndeadLegacyInfo.Report();
				if (!UndeadLegacyInfo.Present)
				{
					UlPatches.MarkAllSkipped("NOT APPLIED - Undead Legacy is not installed");
					return;
				}
				UlPatches.Install(new Harmony(HarmonyId));
			}
			catch (Exception e)
			{
				Log.Error(LogPrefix + "Failed to apply patches; the inventory tweaks will do nothing.");
				Log.Exception(e);
			}
		}
	}
}
