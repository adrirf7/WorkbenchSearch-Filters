using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;

namespace WorkbenchSearchFilters
{
    internal enum Category
    {
        All,
        Weapons,
        Armor,
        Tools,
        Consumables,
        Materials,
        Misc,
        // Solo en las estaciones de comida (caldero y mesa de preparación).
        FoodStamina,
        FoodHealth,
        FoodEitr,
        FoodBalanced,
        Meads
    }

    internal static class Categories
    {
        internal static readonly int Count = Enum.GetValues(typeof(Category)).Length;

        /// <summary>Filtros de las estaciones normales (banco de trabajo, forja...), en el orden de los botones.</summary>
        internal static readonly Category[] Crafting =
        {
            Category.All, Category.Weapons, Category.Armor, Category.Tools,
            Category.Consumables, Category.Materials, Category.Misc
        };

        /// <summary>Filtros del caldero y la mesa de preparación, en el orden de los botones.</summary>
        internal static readonly Category[] Food =
        {
            Category.All, Category.FoodHealth, Category.FoodStamina, Category.FoodEitr,
            Category.FoodBalanced, Category.Meads, Category.Misc
        };

        /// <summary>Todas las categorías con botón (cada botón se crea una sola vez y se muestra según la estación).</summary>
        internal static readonly Category[] Buttons = Crafting.Concat(Food).Distinct().ToArray();

        internal static Category[] ForStation(bool foodStation)
        {
            return foodStation ? Food : Crafting;
        }

        internal static readonly string ReportPath = Path.Combine(Paths.ConfigPath, "WorkbenchSearchFilters.adrirf7.Categories.txt");

        private static readonly Dictionary<ItemDrop, Category> s_cache = new Dictionary<ItemDrop, Category>();
        private static readonly Dictionary<ItemDrop, Category> s_foodCache = new Dictionary<ItemDrop, Category>();
        private static Dictionary<string, Category> s_overrides;
        // Prefabs que se meten en el horno, la estufa o el fermentador (masa de pan, empanadas crudas, bases de hidromiel...).
        private static HashSet<string> s_foodIngredients;
        // Lo que se mete en el horno o la estufa -> lo que sale (para clasificar la comida cruda por la comida que da).
        private static Dictionary<string, ItemDrop> s_cookedResults;
        private static HashSet<string> s_fermenterInputs;

        /// <summary>El caldero y la mesa de preparación de alimentos usan los filtros de comida.</summary>
        internal static bool IsFoodStation(CraftingStation station)
        {
            if (station == null)
            {
                return false;
            }
            string name = (station.m_name + " " + station.gameObject.name).ToLowerInvariant();
            return name.Contains("cauldron") || name.Contains("preptable");
        }

        internal static string Token(Category category)
        {
            return "$wsf_cat_" + category.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Prefabs cuyo icono representa la categoría (se usa el primero que exista). "Prefab:N" usa la variante N
        /// del icono (los escudos tienen varias pinturas).
        /// </summary>
        internal static string[] IconPrefabs(Category category)
        {
            switch (category)
            {
                case Category.Weapons: return new[] { "SwordIron", "SwordBronze", "AxeBronze" };
                case Category.Armor: return new[] { "ShieldBanded:1", "ArmorBronzeChest", "HelmetBronze" }; // escudo con bandas rojo y blanco
                case Category.Tools: return new[] { "Hammer", "PickaxeIron", "Hoe" };
                case Category.Consumables: return new[] { "MeadHealthMinor", "CookedMeat" };
                case Category.Materials: return new[] { "Bronze", "Iron", "Wood" };
                case Category.Misc: return new[] { "Coins", "Wishbone" };
                case Category.Meads: return new[] { "MeadBaseHealthMinor", "MeadHealthMinor" };
                default: return new string[0];
            }
        }

        /// <summary>Color del tenedor de cada tipo de comida; null para las categorías con icono de objeto.</summary>
        internal static Color? FoodColor(Category category)
        {
            switch (category)
            {
                case Category.FoodStamina: return new Color(1f, 0.85f, 0.25f, 1f);
                case Category.FoodHealth: return new Color(1f, 0.35f, 0.35f, 1f);
                case Category.FoodEitr: return new Color(0.75f, 0.45f, 1f, 1f);
                case Category.FoodBalanced: return Color.white;
                default: return null;
            }
        }

        /// <summary>Olvida lo calculado (al entrar en un mundo o al cambiar la configuración).</summary>
        internal static void ResetCache()
        {
            s_cache.Clear();
            s_foodCache.Clear();
            s_overrides = null;
            s_foodIngredients = null;
            s_cookedResults = null;
            s_fermenterInputs = null;
        }

        internal static Category Classify(ItemDrop item, bool foodStation)
        {
            Dictionary<ItemDrop, Category> cache = foodStation ? s_foodCache : s_cache;
            if (!cache.TryGetValue(item, out Category category))
            {
                category = foodStation ? ClassifyFood(item) : ClassifyUncached(item);
                cache[item] = category;
            }
            return category;
        }

        /// <summary>
        /// Tipo de comida con el mismo criterio que el indicador de color del inventario del juego: eitr si el eitr
        /// es más del doble que la salud y el vigor, vida o vigor si uno es más del doble que el otro, y si no equilibrada.
        /// La comida cruda (masa de pan, pasteles sin hornear...) se clasifica por la comida que da al cocinarla.
        /// </summary>
        private static Category ClassifyFood(ItemDrop item)
        {
            if (GetOverrides().TryGetValue(item.name, out Category forced) && Food.Contains(forced))
            {
                return forced;
            }

            ItemDrop.ItemData.SharedData shared = item.m_itemData.m_shared;
            ItemDrop.ItemData.SharedData food = FoodStats(item);
            if (food.m_food > 0f || food.m_foodStamina > 0f || food.m_foodEitr > 0f)
            {
                if (food.m_food < food.m_foodEitr / 2f && food.m_foodStamina < food.m_foodEitr / 2f)
                {
                    return Category.FoodEitr;
                }
                if (food.m_foodStamina < food.m_food / 2f)
                {
                    return Category.FoodHealth;
                }
                if (food.m_food < food.m_foodStamina / 2f)
                {
                    return Category.FoodStamina;
                }
                return Category.FoodBalanced;
            }
            if ((s_fermenterInputs != null && s_fermenterInputs.Contains(item.name)) || shared.m_isDrink ||
                shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable)
            {
                return Category.Meads;
            }
            return Category.Misc;
        }

        /// <summary>Datos de comida del objeto, o de la comida que da al cocinarlo si es comida cruda.</summary>
        private static ItemDrop.ItemData.SharedData FoodStats(ItemDrop item)
        {
            GetFoodIngredients();
            if (s_cookedResults != null && s_cookedResults.TryGetValue(item.name, out ItemDrop cooked) && cooked != null)
            {
                return cooked.m_itemData.m_shared;
            }
            return item.m_itemData.m_shared;
        }

        /// <summary>
        /// Valor por el que se ordena (de mayor a menor) dentro de cada filtro de comida: la vida, el vigor o el eitr
        /// que da; en las equilibradas, vida + vigor. 0 en el resto de categorías (se queda el orden del juego).
        /// </summary>
        internal static float FoodSortValue(ItemDrop item, Category category)
        {
            ItemDrop.ItemData.SharedData food;
            switch (category)
            {
                case Category.FoodHealth: return FoodStats(item).m_food;
                case Category.FoodStamina: return FoodStats(item).m_foodStamina;
                case Category.FoodEitr: return FoodStats(item).m_foodEitr;
                case Category.FoodBalanced:
                    food = FoodStats(item);
                    return food.m_food + food.m_foodStamina;
                default: return 0f;
            }
        }

        internal static bool IsFoodType(Category category)
        {
            return category == Category.FoodHealth || category == Category.FoodStamina ||
                   category == Category.FoodEitr || category == Category.FoodBalanced;
        }

        private static Category ClassifyUncached(ItemDrop item)
        {
            if (GetOverrides().TryGetValue(item.name, out Category forced) && Crafting.Contains(forced))
            {
                return forced;
            }

            ItemDrop.ItemData.SharedData shared = item.m_itemData.m_shared;
            ItemDrop.ItemData.ItemType type = shared.m_itemType;

            // Lo que se gasta al usarlo: flechas, virotes, cebos, bombas, comida, pociones e ingredientes de cocina.
            if (type == ItemDrop.ItemData.ItemType.Ammo || type == ItemDrop.ItemData.ItemType.AmmoNonEquipable)
            {
                return Category.Consumables;
            }
            if (shared.m_attack != null && shared.m_attack.m_consumeItem)
            {
                return Category.Consumables;
            }
            if (type == ItemDrop.ItemData.ItemType.Consumable || shared.m_isDrink ||
                shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f)
            {
                return Category.Consumables;
            }
            if (GetFoodIngredients().Contains(item.name))
            {
                return Category.Consumables;
            }

            switch (type)
            {
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Attach_Atgeir:
                    // Picos y cañas de pescar son armas para el juego, pero se usan como herramientas.
                    if (shared.m_skillType == Skills.SkillType.Pickaxes || shared.m_skillType == Skills.SkillType.Fishing)
                    {
                        return Category.Tools;
                    }
                    // Objetos que se empuñan pero no hacen daño (jarras...).
                    if (type != ItemDrop.ItemData.ItemType.Bow && shared.m_damages.GetTotalDamage() <= 0f)
                    {
                        return Category.Misc;
                    }
                    return Category.Weapons;
                case ItemDrop.ItemData.ItemType.Shield:
                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs:
                case ItemDrop.ItemData.ItemType.Hands:
                case ItemDrop.ItemData.ItemType.Shoulder:
                case ItemDrop.ItemData.ItemType.Utility:
                case ItemDrop.ItemData.ItemType.Trinket:
                    return Category.Armor;
                case ItemDrop.ItemData.ItemType.Tool:
                case ItemDrop.ItemData.ItemType.Torch:
                    return Category.Tools;
                case ItemDrop.ItemData.ItemType.Material:
                    return Category.Materials;
                default:
                    return Category.Misc;
            }
        }

        private static Dictionary<string, Category> GetOverrides()
        {
            if (s_overrides != null)
            {
                return s_overrides;
            }
            s_overrides = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
            foreach (string part in WorkbenchSearchFiltersPlugin.CategoryOverrides.Value.Split(','))
            {
                string[] pair = part.Split(':');
                if (pair.Length != 2)
                {
                    continue;
                }
                string prefab = pair[0].Trim();
                if (prefab.Length > 0 && Enum.TryParse(pair[1].Trim(), true, out Category category) && category != Category.All)
                {
                    s_overrides[prefab] = category;
                }
                else
                {
                    WorkbenchSearchFiltersPlugin.Log.LogWarning("Invalid category override: '" + part.Trim() + "'");
                }
            }
            return s_overrides;
        }

        private static HashSet<string> GetFoodIngredients()
        {
            if (s_foodIngredients != null)
            {
                return s_foodIngredients;
            }
            HashSet<string> result = new HashSet<string>();
            if (ZNetScene.instance == null)
            {
                return result; // sin guardar en caché: se vuelve a intentar cuando exista la escena
            }
            var cookedResults = new Dictionary<string, ItemDrop>();
            var fermenterInputs = new HashSet<string>();
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                if (prefab == null)
                {
                    continue;
                }
                CookingStation cooking = prefab.GetComponent<CookingStation>();
                if (cooking != null)
                {
                    foreach (CookingStation.ItemConversion conversion in cooking.m_conversion)
                    {
                        if (conversion?.m_from != null)
                        {
                            result.Add(conversion.m_from.name);
                            if (conversion.m_to != null)
                            {
                                cookedResults[conversion.m_from.name] = conversion.m_to;
                            }
                        }
                    }
                }
                Fermenter fermenter = prefab.GetComponent<Fermenter>();
                if (fermenter != null)
                {
                    foreach (Fermenter.ItemConversion conversion in fermenter.m_conversion)
                    {
                        if (conversion?.m_from != null)
                        {
                            result.Add(conversion.m_from.name);
                            fermenterInputs.Add(conversion.m_from.name);
                        }
                    }
                }
            }
            s_foodIngredients = result;
            s_cookedResults = cookedResults;
            s_fermenterInputs = fermenterInputs;
            return result;
        }

        /// <summary>
        /// Escribe en BepInEx/config la categoría de cada objeto fabricable, para poder revisarla y corregirla
        /// con CategoryOverrides.
        /// </summary>
        internal static void WriteReport()
        {
            if (ObjectDB.instance == null)
            {
                return;
            }
            try
            {
                var rows = ObjectDB.instance.m_recipes
                    .Where(recipe => recipe != null && recipe.m_item != null && recipe.m_enabled)
                    .Select(recipe => recipe.m_item)
                    .Distinct()
                    .Select(item => new
                    {
                        Category = Classify(item, foodStation: false),
                        Prefab = item.name,
                        Name = Localization.instance.Localize(item.m_itemData.m_shared.m_name),
                        Type = item.m_itemData.m_shared.m_itemType
                    })
                    .OrderBy(row => row.Category).ThenBy(row => row.Name)
                    .ToList();

                StringBuilder text = new StringBuilder();
                text.AppendLine("# Category of every craftable item, as shown by " + WorkbenchSearchFiltersPlugin.ModName + ".");
                text.AppendLine("# Regenerated every time you enter a world. To move an item, add 'Prefab:Category' to");
                text.AppendLine("# CategoryOverrides in WorkbenchSearchFilters.adrirf7.cfg (categories: " +
                                string.Join(", ", Crafting.Where(c => c != Category.All)) + ";");
                text.AppendLine("# at the cauldron and the food preparation table: " +
                                string.Join(", ", Food.Where(c => c != Category.All)) + ").");
                Category? current = null;
                foreach (var row in rows)
                {
                    if (current != row.Category)
                    {
                        current = row.Category;
                        text.AppendLine();
                        text.AppendLine("[" + row.Category + "]");
                    }
                    text.AppendLine(row.Prefab.PadRight(32) + " " + row.Name.PadRight(32) + " (" + row.Type + ")");
                }
                File.WriteAllText(ReportPath, text.ToString(), Encoding.UTF8);
            }
            catch (Exception e)
            {
                WorkbenchSearchFiltersPlugin.Log.LogWarning("Could not write " + ReportPath + ": " + e.Message);
            }
        }
    }
}
