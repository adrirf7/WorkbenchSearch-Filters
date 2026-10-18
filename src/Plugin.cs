using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace WorkbenchSearchFilters
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public class WorkbenchSearchFiltersPlugin : BaseUnityPlugin
    {
        public const string ModGuid = "adrirf7.WorkbenchSearchFilters";
        public const string ModName = "WorkbenchSearchFilters";
        public const string ModVersion = "1.1.0";

        internal static ManualLogSource Log;

        private Harmony _harmony;

        internal static ConfigEntry<bool> ClearSearchOnClose;
        internal static ConfigEntry<bool> RememberCategory;
        internal static ConfigEntry<string> CategoryOverrides;

        private void Awake()
        {
            Log = Logger;

            ConfigFile config = new ConfigFile(Path.Combine(Paths.ConfigPath, "WorkbenchSearchFilters.adrirf7.cfg"), true, Info.Metadata);
            ClearSearchOnClose = config.Bind("General", "ClearSearchOnClose", true,
                "true = the search text is cleared every time the inventory is closed.");
            RememberCategory = config.Bind("General", "RememberCategory", true,
                "true = the selected category is kept when the inventory is closed and opened again; " +
                "false = it goes back to 'All' every time.");
            CategoryOverrides = config.Bind("Categories", "CategoryOverrides",
                "Tankard:Misc,TankardOdin:Misc,Tankard_dvergr:Misc,TankardAnniversary:Misc",
                "Force the category of specific items, as Prefab:Category separated by commas. " +
                "Categories: Weapons, Armor, Tools, Consumables, Materials, Misc; at the cauldron and the food preparation table: " +
                "FoodStamina, FoodHealth, FoodEitr, FoodBalanced, Meads, Misc. " +
                "The prefab name and current category of every craftable item are listed in " +
                "WorkbenchSearchFilters.adrirf7.Categories.txt (generated when entering a world).");
            CategoryOverrides.SettingChanged += (sender, args) => Categories.ResetCache();

            _harmony = new Harmony(ModGuid);
            _harmony.PatchAll();
            // Por si el idioma ya se cargó antes que el mod.
            Translations.Register(Localization.instance, Localization.instance.GetSelectedLanguage());
            Log.LogInfo(ModName + " " + ModVersion + " loaded.");        }

        /// <summary>Al descargar el mod (recarga en caliente con ScriptEngine) se deshace todo lo que añadió.</summary>
        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            FilterBar.Teardown();
        }
    }
}
