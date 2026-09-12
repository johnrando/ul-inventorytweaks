using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace InventoryTweaks
{
	/// <summary>
	/// Reads <see cref="Settings"/> back at startup and writes it out whenever an <c>it</c> command
	/// (or a right-click on a sort button) changes something. The file lives in the game's user
	/// data folder rather than in the mod folder, so it survives a mod update. Plain
	/// <c>key = value</c> text; every line names the console command that writes it. Nothing here
	/// can stop the mod working: any failure degrades to a log line and the defaults.
	/// </summary>
	internal static class Config
	{
		private const string FolderName = "InventoryTweaks";

		private const string FileName = "settings.txt";

		/// <summary>What the last load or save did, as reported by <c>it info</c>.</summary>
		internal static string Status = "not loaded - mod init has not run";

		private static string filePath;

		internal static void Load()
		{
			if (!Resolve())
			{
				return;
			}

			if (!File.Exists(filePath))
			{
				Save();
				return;
			}

			try
			{
				int applied = 0;
				int rejected = 0;
				foreach (string line in File.ReadAllLines(filePath))
				{
					switch (Parse(line))
					{
					case LineResult.Applied:
						applied++;
						break;
					case LineResult.Rejected:
						rejected++;
						break;
					}
				}

				Status = applied + " settings loaded"
					+ (rejected > 0 ? ", " + rejected + " line(s) ignored" : "")
					+ " - " + filePath;
				Log.Out(Patches.LogPrefix + "Settings loaded from " + filePath + ".");
			}
			catch (Exception e)
			{
				Status = "NOT LOADED - " + e.Message;
				Log.Warning(Patches.LogPrefix + "Could not read " + filePath + ", so the built-in "
					+ "defaults are in force: " + e.Message);
			}
		}

		/// <summary>Writes the whole file, which is what keeps the comments and ordering intact.</summary>
		internal static void Save()
		{
			if (!Resolve())
			{
				return;
			}

			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(filePath));
				File.WriteAllText(filePath, Compose());
				Status = "saved - " + filePath;
			}
			catch (Exception e)
			{
				Status = "NOT SAVED - " + e.Message;
				Log.Warning(Patches.LogPrefix + "Could not write " + filePath + ", so this change "
					+ "will not survive a restart: " + e.Message);
			}
		}

		private static bool Resolve()
		{
			if (filePath != null)
			{
				return true;
			}

			try
			{
				string dir = GameIO.GetUserGameDataDir();
				if (string.IsNullOrEmpty(dir))
				{
					Status = "unavailable - the game reported no user data folder";
					return false;
				}
				filePath = Path.Combine(Path.Combine(dir, FolderName), FileName);
				return true;
			}
			catch (Exception e)
			{
				Status = "unavailable - " + e.Message;
				Log.Warning(Patches.LogPrefix + "Could not work out where to keep settings, so they "
					+ "will not persist: " + e.Message);
				return false;
			}
		}

		private static string Compose()
		{
			StringBuilder text = new StringBuilder();
			text.AppendLine("# InventoryTweaks settings.");
			text.AppendLine("#");
			text.AppendLine("# Read once when the game starts and rewritten whenever an 'it' command (or a");
			text.AppendLine("# right-click on a sort button) changes something, so edit this with the game");
			text.AppendLine("# closed. Every line names the command that sets it; anything after a '#' is a");
			text.AppendLine("# comment, and a line that will not parse is ignored rather than fatal.");
			text.AppendLine();
			Setting(text, "enabled", OnOff(Settings.Enabled), "it on|off");
			Setting(text, "scroll", OnOff(Settings.ScrollToTop), "it scroll");
			Setting(text, "containers", OnOff(Settings.ScrollContainers), "it containers");
			Setting(text, "lock", Settings.LockedSort.Length == 0 ? "none" : Settings.LockedSort,
				"it lock {none|weight|price|group|name} - or right-click a sort button");
			Setting(text, "autosort", OnOff(Settings.AutoSort), "it autosort");
			Setting(text, "highlight", OnOff(Settings.Highlight), "it highlight");
			Setting(text, "color", Color(Settings.HighlightColor), "it color {r,g,b,a}");
			Setting(text, "markers", OnOff(Settings.Markers), "it markers");
			return text.ToString();
		}

		private static void Setting(StringBuilder _text, string _key, string _value, string _command)
		{
			_text.AppendLine(_key.PadRight(14) + "= " + _value.PadRight(16) + " # " + _command);
		}

		private enum LineResult
		{
			Skipped,

			Applied,

			Rejected
		}

		private static LineResult Parse(string _line)
		{
			int comment = _line.IndexOf('#');
			string text = (comment >= 0 ? _line.Substring(0, comment) : _line).Trim();
			if (text.Length == 0)
			{
				return LineResult.Skipped;
			}

			int split = text.IndexOf('=');
			if (split <= 0)
			{
				Log.Warning(Patches.LogPrefix + "Ignoring a settings line that is not 'key = value': "
					+ _line.Trim());
				return LineResult.Rejected;
			}

			string key = text.Substring(0, split).Trim().ToLowerInvariant();
			string value = text.Substring(split + 1).Trim();
			if (Apply(key, value))
			{
				return LineResult.Applied;
			}

			Log.Warning(Patches.LogPrefix + "Ignoring settings line '" + _line.Trim()
				+ "' - unknown setting or unusable value.");
			return LineResult.Rejected;
		}

		private static bool Apply(string _key, string _value)
		{
			switch (_key)
			{
			case "enabled":
				return TryBool(_value, ref Settings.Enabled);
			case "scroll":
				return TryBool(_value, ref Settings.ScrollToTop);
			case "containers":
				return TryBool(_value, ref Settings.ScrollContainers);
			case "lock":
				return TryLock(_value);
			case "autosort":
				return TryBool(_value, ref Settings.AutoSort);
			case "highlight":
				return TryBool(_value, ref Settings.Highlight);
			case "color":
				return LoadColor(_value, ref Settings.HighlightColor);
			case "markers":
				return TryBool(_value, ref Settings.Markers);
			default:
				return false;
			}
		}

		private static bool TryBool(string _value, ref bool _target)
		{
			switch (_value.ToLowerInvariant())
			{
			case "on":
			case "true":
			case "yes":
			case "1":
				_target = true;
				return true;
			case "off":
			case "false":
			case "no":
			case "0":
				_target = false;
				return true;
			default:
				return false;
			}
		}

		/// <summary>A sort mode name, or "none". Shared with the console command.</summary>
		internal static bool TryLock(string _value)
		{
			string mode = _value.ToLowerInvariant();
			if (mode == "none" || mode == "off" || mode.Length == 0)
			{
				Settings.LockedSort = "";
				return true;
			}
			if (!SortModes.IsName(mode))
			{
				return false;
			}
			Settings.LockedSort = mode;
			return true;
		}

		/// <summary>"r,g,b" or "r,g,b,a", each 0-255. Shared with the console command.</summary>
		internal static bool TryColor(string _value, out Color32 _parsed)
		{
			_parsed = default;
			string[] parts = _value.Split(',');
			if (parts.Length != 3 && parts.Length != 4)
			{
				return false;
			}
			byte[] channels = { 0, 0, 0, byte.MaxValue };
			for (int i = 0; i < parts.Length; i++)
			{
				if (!byte.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
					out channels[i]))
				{
					return false;
				}
			}
			_parsed = new Color32(channels[0], channels[1], channels[2], channels[3]);
			return true;
		}

		private static bool LoadColor(string _value, ref Color32 _target)
		{
			if (!TryColor(_value, out Color32 parsed))
			{
				return false;
			}
			_target = parsed;
			return true;
		}

		private static string OnOff(bool _on)
		{
			return _on ? "on" : "off";
		}

		internal static string Color(Color32 _color)
		{
			return _color.r + "," + _color.g + "," + _color.b + "," + _color.a;
		}
	}
}
