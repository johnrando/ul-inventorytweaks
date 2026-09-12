namespace InventoryTweaks
{
	public class ModApi : IModApi
	{
		public void InitMod(Mod _modInstance)
		{
			// Settings first, so the startup log reports the player's lock rather than the default.
			Config.Load();
			Patches.Apply();
		}
	}
}
