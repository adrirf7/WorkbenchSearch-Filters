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
            "wsf_cat_tools", "wsf_cat_consumables", "wsf_cat_materials", "wsf_cat_misc",
            "wsf_cat_foodstamina", "wsf_cat_foodhealth", "wsf_cat_foodeitr", "wsf_cat_foodbalanced", "wsf_cat_meads"
        };

        // idioma -> textos (mismo orden que s_keys)
        private static readonly Dictionary<string, string[]> s_languages = new Dictionary<string, string[]>
        {
            ["English"] = new[] { "Search...", "All", "Weapons", "Armor", "Tools", "Consumables", "Materials", "Misc", "Stamina food", "Health food", "Eitr food", "Balanced food", "Meads" },
            ["Spanish"] = new[] { "Buscar...", "Todo", "Armas", "Armaduras", "Herramientas", "Consumibles", "Materiales", "Otros", "Comida de vigor", "Comida de vida", "Comida de eitr", "Comida equilibrada", "Hidromieles" },
            ["German"] = new[] { "Suchen...", "Alle", "Waffen", "Rüstung", "Werkzeuge", "Verbrauchsgüter", "Materialien", "Sonstiges", "Ausdauer-Essen", "Lebens-Essen", "Eitr-Essen", "Ausgewogenes Essen", "Met" },
            ["French"] = new[] { "Rechercher...", "Tout", "Armes", "Armures", "Outils", "Consommables", "Matériaux", "Divers", "Nourriture d'endurance", "Nourriture de santé", "Nourriture d'eitr", "Nourriture équilibrée", "Hydromels" },
            ["Italian"] = new[] { "Cerca...", "Tutto", "Armi", "Armature", "Strumenti", "Consumabili", "Materiali", "Varie", "Cibo per la resistenza", "Cibo per la salute", "Cibo per l'eitr", "Cibo bilanciato", "Idromele" },
            ["Portuguese_Brazilian"] = new[] { "Pesquisar...", "Tudo", "Armas", "Armaduras", "Ferramentas", "Consumíveis", "Materiais", "Outros", "Comida de vigor", "Comida de vida", "Comida de eitr", "Comida equilibrada", "Hidroméis" },
            ["Portuguese_European"] = new[] { "Pesquisar...", "Tudo", "Armas", "Armaduras", "Ferramentas", "Consumíveis", "Materiais", "Outros", "Comida de vigor", "Comida de vida", "Comida de eitr", "Comida equilibrada", "Hidroméis" },
            ["Russian"] = new[] { "Поиск...", "Все", "Оружие", "Броня", "Инструменты", "Расходники", "Материалы", "Прочее", "Еда на выносливость", "Еда на здоровье", "Еда на эйтр", "Сбалансированная еда", "Медовуха" },
            ["Ukrainian"] = new[] { "Пошук...", "Усе", "Зброя", "Броня", "Інструменти", "Витратні", "Матеріали", "Інше", "Їжа на витривалість", "Їжа на здоров'я", "Їжа на ейтр", "Збалансована їжа", "Медовуха" },
            ["Polish"] = new[] { "Szukaj...", "Wszystko", "Broń", "Pancerz", "Narzędzia", "Konsumpcyjne", "Materiały", "Inne", "Jedzenie na wytrzymałość", "Jedzenie na zdrowie", "Jedzenie na eitr", "Zbilansowane jedzenie", "Miody pitne" },
            ["Czech"] = new[] { "Hledat...", "Vše", "Zbraně", "Zbroj", "Nástroje", "Spotřební", "Materiály", "Ostatní", "Jídlo na výdrž", "Jídlo na zdraví", "Jídlo na eitr", "Vyvážené jídlo", "Medoviny" },
            ["Dutch"] = new[] { "Zoeken...", "Alles", "Wapens", "Pantser", "Gereedschap", "Verbruiksartikelen", "Materialen", "Overig", "Uithoudingsvoedsel", "Gezondheidsvoedsel", "Eitr-voedsel", "Gebalanceerd voedsel", "Mede" },
            ["Swedish"] = new[] { "Sök...", "Alla", "Vapen", "Rustning", "Verktyg", "Förbrukningsvaror", "Material", "Övrigt", "Uthållighetsmat", "Hälsomat", "Eitr-mat", "Balanserad mat", "Mjöd" },
            ["Norwegian"] = new[] { "Søk...", "Alle", "Våpen", "Rustning", "Verktøy", "Forbruksvarer", "Materialer", "Diverse", "Utholdenhetsmat", "Helsemat", "Eitr-mat", "Balansert mat", "Mjød" },
            ["Danish"] = new[] { "Søg...", "Alle", "Våben", "Rustning", "Værktøj", "Forbrugsvarer", "Materialer", "Diverse", "Udholdenhedsmad", "Helbredsmad", "Eitr-mad", "Balanceret mad", "Mjød" },
            ["Finnish"] = new[] { "Hae...", "Kaikki", "Aseet", "Panssarit", "Työkalut", "Kulutustavarat", "Materiaalit", "Muut", "Kestävyysruoka", "Terveysruoka", "Eitr-ruoka", "Tasapainoinen ruoka", "Simat" },
            ["Turkish"] = new[] { "Ara...", "Tümü", "Silahlar", "Zırhlar", "Aletler", "Sarf Malzemeleri", "Malzemeler", "Diğer", "Dayanıklılık yemeği", "Sağlık yemeği", "Eitr yemeği", "Dengeli yemek", "Bal şarapları" },
            ["Hungarian"] = new[] { "Keresés...", "Mind", "Fegyverek", "Páncélok", "Eszközök", "Fogyóeszközök", "Nyersanyagok", "Egyéb", "Állóképesség-étel", "Életerő-étel", "Eitr-étel", "Kiegyensúlyozott étel", "Mézborok" },
            ["Chinese"] = new[] { "搜索...", "全部", "武器", "护甲", "工具", "消耗品", "材料", "其他", "耐力食物", "生命食物", "艾特食物", "均衡食物", "蜂蜜酒" },
            ["Chinese_Trad"] = new[] { "搜尋...", "全部", "武器", "護甲", "工具", "消耗品", "材料", "其他", "耐力食物", "生命食物", "艾特食物", "均衡食物", "蜂蜜酒" },
            ["Japanese"] = new[] { "検索...", "すべて", "武器", "防具", "道具", "消耗品", "素材", "その他", "スタミナ料理", "体力料理", "エイトル料理", "バランス料理", "ミード" },
            ["Korean"] = new[] { "검색...", "전체", "무기", "방어구", "도구", "소모품", "재료", "기타", "기력 음식", "체력 음식", "에이트르 음식", "균형 음식", "미드" },
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
