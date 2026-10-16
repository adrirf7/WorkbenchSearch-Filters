using System.Collections.Generic;
using HarmonyLib;

namespace WorkbenchSearchFilters
{
    /// <summary>
    /// Textos del mod. Se registran en el sistema de localización de Valheim, así que siguen el idioma
    /// elegido en el juego (también al cambiarlo en Ajustes). Inglés para cualquier idioma sin traducción.
    /// </summary>
    internal static class Translations
    {
        private const string DefaultLanguage = "English";

        // Orden de las claves; cada idioma da sus textos en este mismo orden.
        private static readonly string[] s_keys =
        {
            "wsf_search", "wsf_cat_all", "wsf_cat_weapons", "wsf_cat_armor",
            "wsf_cat_tools", "wsf_cat_consumables", "wsf_cat_materials", "wsf_cat_misc"
        };

        // idioma -> textos (mismo orden que s_keys)
        private static readonly Dictionary<string, string[]> s_languages = new Dictionary<string, string[]>
        {
            ["English"] = new[] { "Search...", "All", "Weapons", "Armor", "Tools", "Consumables", "Materials", "Misc" },
            ["Spanish"] = new[] { "Buscar...", "Todo", "Armas", "Armaduras", "Herramientas", "Consumibles", "Materiales", "Otros" },
            ["German"] = new[] { "Suchen...", "Alle", "Waffen", "Rüstung", "Werkzeuge", "Verbrauchsgüter", "Materialien", "Sonstiges" },
            ["French"] = new[] { "Rechercher...", "Tout", "Armes", "Armures", "Outils", "Consommables", "Matériaux", "Divers" },
            ["Italian"] = new[] { "Cerca...", "Tutto", "Armi", "Armature", "Strumenti", "Consumabili", "Materiali", "Varie" },
            ["Portuguese_Brazilian"] = new[] { "Pesquisar...", "Tudo", "Armas", "Armaduras", "Ferramentas", "Consumíveis", "Materiais", "Outros" },
            ["Portuguese_European"] = new[] { "Pesquisar...", "Tudo", "Armas", "Armaduras", "Ferramentas", "Consumíveis", "Materiais", "Outros" },
            ["Russian"] = new[] { "Поиск...", "Все", "Оружие", "Броня", "Инструменты", "Расходники", "Материалы", "Прочее" },
            ["Ukrainian"] = new[] { "Пошук...", "Усе", "Зброя", "Броня", "Інструменти", "Витратні", "Матеріали", "Інше" },
            ["Polish"] = new[] { "Szukaj...", "Wszystko", "Broń", "Pancerz", "Narzędzia", "Konsumpcyjne", "Materiały", "Inne" },
            ["Czech"] = new[] { "Hledat...", "Vše", "Zbraně", "Zbroj", "Nástroje", "Spotřební", "Materiály", "Ostatní" },
            ["Dutch"] = new[] { "Zoeken...", "Alles", "Wapens", "Pantser", "Gereedschap", "Verbruiksartikelen", "Materialen", "Overig" },
            ["Swedish"] = new[] { "Sök...", "Alla", "Vapen", "Rustning", "Verktyg", "Förbrukningsvaror", "Material", "Övrigt" },
            ["Norwegian"] = new[] { "Søk...", "Alle", "Våpen", "Rustning", "Verktøy", "Forbruksvarer", "Materialer", "Diverse" },
            ["Danish"] = new[] { "Søg...", "Alle", "Våben", "Rustning", "Værktøj", "Forbrugsvarer", "Materialer", "Diverse" },
            ["Finnish"] = new[] { "Hae...", "Kaikki", "Aseet", "Panssarit", "Työkalut", "Kulutustavarat", "Materiaalit", "Muut" },
            ["Turkish"] = new[] { "Ara...", "Tümü", "Silahlar", "Zırhlar", "Aletler", "Sarf Malzemeleri", "Malzemeler", "Diğer" },
            ["Hungarian"] = new[] { "Keresés...", "Mind", "Fegyverek", "Páncélok", "Eszközök", "Fogyóeszközök", "Nyersanyagok", "Egyéb" },
            ["Chinese"] = new[] { "搜索...", "全部", "武器", "护甲", "工具", "消耗品", "材料", "其他" },
            ["Chinese_Trad"] = new[] { "搜尋...", "全部", "武器", "護甲", "工具", "消耗品", "材料", "其他" },
            ["Japanese"] = new[] { "検索...", "すべて", "武器", "防具", "道具", "消耗品", "素材", "その他" },
            ["Korean"] = new[] { "검색...", "전체", "무기", "방어구", "도구", "소모품", "재료", "기타" },
        };

        internal static void Register(Localization localization, string language)
        {
            if (!s_languages.TryGetValue(language, out string[] texts))
            {
                texts = s_languages[DefaultLanguage];
            }
            for (int i = 0; i < s_keys.Length; i++)
            {
                localization.AddWord(s_keys[i], texts[i]);
            }
        }
    }

    [HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
    internal static class Localization_SetupLanguage_Patch
    {
        private static void Postfix(Localization __instance, string language)
        {
            Translations.Register(__instance, language);
            // Si el inventario ya existe, actualizar los textos fijos (placeholder, "Todo").
            FilterBar.RefreshTexts();
        }
    }
}
