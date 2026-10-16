using HarmonyLib;

namespace WorkbenchSearchFilters
{
    /// <summary>Después de que el juego monte la lista de recetas, se quitan las que no pasan el filtro.</summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipeList))]
    internal static class InventoryGui_UpdateRecipeList_Patch
    {
        private static void Prefix(InventoryGui __instance)
        {
            FilterBar.BeforeGameRebuild(__instance);
        }

        private static void Postfix(InventoryGui __instance)
        {
            FilterBar.EnsureCreated(__instance);
            FilterBar.AfterGameRebuild(__instance);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    internal static class InventoryGui_Hide_Patch
    {
        private static void Postfix()
        {
            FilterBar.OnInventoryHidden();
        }
    }

    /// <summary>
    /// Mientras se escribe en el buscador, el juego lo trata como si se escribiese en el chat:
    /// no se mueve el personaje y teclas como E o Tab no cierran el inventario.
    /// </summary>
    [HarmonyPatch(typeof(Chat), nameof(Chat.HasFocus))]
    internal static class Chat_HasFocus_Patch
    {
        private static void Postfix(ref bool __result)
        {
            if (!__result && FilterBar.IsSearchFocused)
            {
                __result = true;
            }
        }
    }
}
