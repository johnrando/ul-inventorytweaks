using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace InventoryTweaks
{
	/// <summary>
	/// Detects Undead Legacy and its build. Unlike the sibling mods this one *requires* UL: every
	/// patch targets a UL class, so with UL absent nothing is installed and the mod is inert.
	/// The version range is advisory: UL's 2.7.x branch takes new patch numbers routinely, and
	/// each patch already checks that the member it targets still exists.
	///
	/// Only the <c>[BepInPlugin]</c> attribute and the <c>pluginVersion</c> literal on
	/// <c>H_UndeadLegacy</c> are trustworthy. UL's assembly version is hardcoded 1.0.0.0 and its
	/// ModInfo.xml lags reality.
	/// </summary>
	internal static class UndeadLegacyInfo
	{
		private const string AssemblyName = "UndeadLegacy";

		/// <summary>Oldest Undead Legacy build this mod was run against.</summary>
		internal const string TestedFrom = "2.7.30";

		/// <summary>Newest build tested. Bump after re-checking the UL members listed in Patches.</summary>
		internal const string TestedTo = "2.7.30";

		private static string TestedRange =>
			TestedFrom == TestedTo ? TestedFrom : TestedFrom + " - " + TestedTo;

		internal static bool Present;

		internal static string Version = "not detected";

		internal static string DetectedSource = "none";

		/// <summary>One-line summary for the <c>it</c> console command.</summary>
		internal static string Status = "not checked";

		internal static void Report()
		{
			Assembly assembly = FindAssembly(AssemblyName);
			Present = assembly != null;
			if (!Present)
			{
				Status = "NOT INSTALLED - this mod does nothing without it";
				Log.Warning(Patches.LogPrefix + "Undead Legacy is not installed. This mod patches UL's "
					+ "inventory window, so nothing has been installed and every feature is inert.");
				return;
			}

			Type plugin = assembly.GetType("H_UndeadLegacy", false);
			string raw = plugin == null
				? null
				: ReadFromBepInPluginAttribute(plugin) ?? ReadFromVersionConstant(plugin);

			if (raw == null)
			{
				Version = "unknown";
				Status = "installed, version unknown - only tested under " + TestedRange;
				Log.Warning(Patches.LogPrefix + "Undead Legacy is installed but its version could not be "
					+ "read. Only tested under " + TestedRange + "; each patch logs if its target is missing.");
				return;
			}

			Version = raw;
			System.Version detected = Parse(raw);
			System.Version from = Parse(TestedFrom);
			System.Version to = Parse(TestedTo);
			if (detected != null && from != null && to != null && detected >= from && detected <= to)
			{
				Status = raw + " - tested";
				Log.Out(Patches.LogPrefix + "Undead Legacy " + raw + " detected (from " + DetectedSource
					+ "), within the tested range (" + TestedRange + ").");
				return;
			}

			Status = raw + " - UNTESTED, only tested under " + TestedRange;
			Log.Warning(Patches.LogPrefix + "Undead Legacy " + raw + " detected (from " + DetectedSource
				+ "), but this mod has only been tested under " + TestedRange + ". It will still "
				+ "install; each patch logs if the member it targets no longer exists, and 'it info' "
				+ "shows the outcome.");
		}

		private static string ReadFromBepInPluginAttribute(Type _plugin)
		{
			try
			{
				foreach (CustomAttributeData attribute in CustomAttributeData.GetCustomAttributes(_plugin))
				{
					if (attribute.Constructor?.DeclaringType?.Name != "BepInPlugin")
					{
						continue;
					}
					IList<CustomAttributeTypedArgument> args = attribute.ConstructorArguments;
					if (args.Count >= 3 && args[2].Value is string version && version.Length > 0)
					{
						DetectedSource = "[BepInPlugin] attribute";
						return version;
					}
				}
			}
			catch (Exception e)
			{
				Log.Warning(Patches.LogPrefix + "Could not read Undead Legacy's [BepInPlugin] attribute: "
					+ e.Message);
			}
			return null;
		}

		private static string ReadFromVersionConstant(Type _plugin)
		{
			try
			{
				FieldInfo field = AccessTools.Field(_plugin, "pluginVersion");
				if (field != null && field.IsLiteral && field.GetRawConstantValue() is string version
					&& version.Length > 0)
				{
					DetectedSource = "pluginVersion constant";
					return version;
				}
			}
			catch (Exception e)
			{
				Log.Warning(Patches.LogPrefix + "Could not read Undead Legacy's pluginVersion constant: "
					+ e.Message);
			}
			return null;
		}

		/// <summary>
		/// Tolerant parse normalised to major.minor.patch: trims a leading 'v' and any suffix, so
		/// "v2.7.15-beta" is 2.7.15, "2.7.x" is 2.7.0 and "2.7.15.0" equals "2.7.15".
		/// </summary>
		private static System.Version Parse(string _raw)
		{
			if (string.IsNullOrEmpty(_raw))
			{
				return null;
			}
			string text = _raw.Trim();
			if (text.Length > 0 && (text[0] == 'v' || text[0] == 'V'))
			{
				text = text.Substring(1);
			}
			int end = 0;
			while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '.'))
			{
				end++;
			}
			text = text.Substring(0, end).Trim('.');
			if (text.Length == 0)
			{
				return null;
			}
			if (text.IndexOf('.') < 0)
			{
				text += ".0";
			}
			if (!System.Version.TryParse(text, out System.Version parsed))
			{
				return null;
			}
			return new System.Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
		}

		internal static Assembly FindAssembly(string _simpleName)
		{
			Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
			for (int i = 0; i < loaded.Length; i++)
			{
				if (string.Equals(loaded[i].GetName().Name, _simpleName, StringComparison.OrdinalIgnoreCase))
				{
					return loaded[i];
				}
			}
			return null;
		}
	}
}
