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
        Misc
    }

    internal static class Categories
    {
        internal static readonly Category[] All =
        {
            Category.All, Category.Weapons, Category.Armor, Category.Tools,
            Category.Consumables, Category.Materials, Category.Misc
        };

        internal static readonly string ReportPath = Path.Combine(Paths.ConfigPath, "WorkbenchSearchFilters.adrirf7.Categories.txt");

        private static readonly Dictionary<ItemDrop, Category> s_cache = new Dictionary<ItemDrop, Category>();
        private static Dictionary<string, Category> s_overrides;
        // Prefabs que se meten en el horno, la estufa o el fermentador (masa de pan, empanadas crudas, bases de hidromiel...).
        private static HashSet<string> s_foodIngredients;

        internal static string Token(Category category)
        {
            return "$wsf_cat_" + category.ToString().ToLowerInvariant();
        }

        /// <summary>Prefabs cuyo icono representa la categoría (se usa el primero que exista).</summary>
        internal static string[] IconPrefabs(Category category)
        {
            switch (category)
            {
                case Category.Weapons: return new[] { "SwordIron", "SwordBronze", "AxeBronze" };
                case Category.Armor: return new[] { "ArmorBronzeChest", "HelmetBronze", "ShieldWood" };
                case Category.Tools: return new[] { "Hammer", "PickaxeIron", "Hoe" };
                case Category.Consumables: return new[] { "MeadHealthMinor", "CookedMeat" };
                case Category.Materials: return new[] { "Bronze", "Iron", "Wood" };
                case Category.Misc: return new[] { "Coins", "Wishbone" };
                default: return new string[0];
            }
        }

        /// <summary>Olvida lo calculado (al entrar en un mundo o al cambiar la configuración).</summary>
        internal static void ResetCache()
        {
            s_cache.Clear();
            s_overrides = null;
            s_foodIngredients = null;
        }

        internal static Category Classify(ItemDrop item)
        {
            if (!s_cache.TryGetValue(item, out Category category))
            {
                category = ClassifyUncached(item);
                s_cache[item] = category;
            }
            return category;
        }

        private static Category ClassifyUncached(ItemDrop item)
        {
            if (GetOverrides().TryGetValue(item.name, out Category forced))
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
                        }
                    }
                }
            }
            s_foodIngredients = result;
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
                        Category = Classify(item),
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
                                string.Join(", ", All.Where(c => c != Category.All)) + ").");
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
