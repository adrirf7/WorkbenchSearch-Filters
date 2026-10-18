using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace WorkbenchSearchFilters
{
    /// <summary>
    /// Barra con buscador y botones de categoría encima de la lista de recetas del panel de fabricación.
    /// Se crea la primera vez que se muestra la lista y ocupa la parte superior de la lista (que se acorta).
    /// </summary>
    internal static class FilterBar
    {
        private const float SearchHeight = 26f;
        private const float ButtonsHeight = 28f;
        private const float Gap = 3f;
        private const float BarHeight = SearchHeight + Gap + ButtonsHeight + Gap;

        private static readonly Color s_buttonColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color s_selectedColor = new Color(0.85f, 0.6f, 0.2f, 0.9f);
        private const float EmptyIconAlpha = 0.3f;

        private sealed class CategoryButton
        {
            public Category Category;
            public Image Background;
            public Graphic Content;
            public Color ContentColor; // el tenedor de cada comida tiene su color
        }

        private static GameObject s_root;
        // Lo que se modificó al crear la barra, para deshacerlo en Teardown.
        private static InventoryGui s_gui;
        private static RectTransform s_list;
        private static RectTransform s_scrollbar;
        private static TMP_InputField s_search;
        private static TMP_Text s_allLabel;
        private static readonly List<CategoryButton> s_buttons = new List<CategoryButton>();

        // Categoría elegida por el jugador. Si la estación actual no tiene nada de ella se filtra por "Todo",
        // pero se recuerda para cuando vuelva a una estación que sí la tenga.
        internal static Category CurrentCategory = Category.All;
        private static Category s_activeCategory = Category.All;
        internal static string SearchText = "";
        // La lista actual es de una estación de comida (caldero, mesa de preparación): otros filtros.
        private static bool s_foodStation;
        private static bool? s_buttonsFoodStation;

        internal static bool IsSearchFocused => s_search != null && s_search.isFocused;

        internal static void EnsureCreated(InventoryGui gui)
        {
            if (s_root != null)
            {
                return;
            }
            try
            {
                // Nuevo InventoryGui = nuevo mundo: puede haber otros objetos u otros mods cargados.
                Categories.ResetCache();
                Create(gui);
                RefreshTexts();
                Categories.WriteReport();
            }
            catch (Exception e)
            {
                WorkbenchSearchFiltersPlugin.Log.LogError("Could not create the crafting filter bar: " + e);
                if (s_root != null)
                {
                    Object.Destroy(s_root);
                }
                s_root = null;
            }
        }

        /// <summary>
        /// Quita la barra y devuelve la lista de recetas a como la dejó el juego. Se llama al descargar el mod,
        /// después de quitar los parches, para que al recargarlo en caliente no se dupliquen ni se acorte otra vez la lista.
        /// </summary>
        internal static void Teardown()
        {
            if (s_search != null && s_search.isFocused)
            {
                s_search.DeactivateInputField();
            }
            foreach (Entry entry in s_entries)
            {
                if (entry.Pair.InterfaceElement != null && !entry.Pair.InterfaceElement.activeSelf)
                {
                    Object.Destroy(entry.Pair.InterfaceElement);
                }
            }
            s_entries.Clear();

            // Si la InventoryGui ya no existe (cambio de mundo, cierre del juego) no queda nada que restaurar.
            if (s_root != null && s_gui != null)
            {
                Object.Destroy(s_root);
                if (s_list != null)
                {
                    s_list.offsetMax += new Vector2(0f, BarHeight);
                }
                if (s_scrollbar != null)
                {
                    s_scrollbar.offsetMax += new Vector2(0f, BarHeight);
                }
                s_gui.m_recipeListBaseSize += BarHeight;
                if (InventoryGui.IsVisible() && Player.m_localPlayer != null)
                {
                    s_gui.UpdateCraftingPanel(false);
                }
            }
            s_root = null;
            s_gui = null;
            s_list = null;
            s_scrollbar = null;
            s_search = null;
            s_allLabel = null;
            s_buttons.Clear();
            s_buttonsFoodStation = null;
        }

        // ---------------------------------------------------------------- filtrado

        // Lista completa (ya ordenada) que montó el juego la última vez, con la categoría y el nombre ya calculados.
        // Al escribir o cambiar de categoría solo se ocultan/muestran sus filas: no se vuelve a montar la lista,
        // que es lo que causaba tirones (el juego destruye y crea de nuevo todas las filas).
        private struct Entry
        {
            public InventoryGui.RecipeDataPair Pair;
            public Category Category;
            public string Name;
            public float FoodValue; // vida/vigor/eitr que da, para ordenar dentro de cada filtro de comida
        }

        private static readonly List<Entry> s_entries = new List<Entry>();

        /// <summary>Antes de que el juego monte la lista: destruir las filas ocultas, que el juego no conoce.</summary>
        internal static void BeforeGameRebuild(InventoryGui gui)
        {
            foreach (Entry entry in s_entries)
            {
                if (entry.Pair.InterfaceElement != null && !entry.Pair.InterfaceElement.activeSelf)
                {
                    Object.Destroy(entry.Pair.InterfaceElement);
                }
            }
            s_entries.Clear();
        }

        /// <summary>Después de que el juego monte la lista: guardarla completa y aplicar el filtro.</summary>
        internal static void AfterGameRebuild(InventoryGui gui)
        {
            s_entries.Clear();
            s_foodStation = Categories.IsFoodStation(Player.m_localPlayer != null ? Player.m_localPlayer.GetCurrentCraftingStation() : null);
            foreach (InventoryGui.RecipeDataPair pair in gui.m_availableRecipes)
            {
                Category category = Categories.Classify(pair.Recipe.m_item, s_foodStation);
                s_entries.Add(new Entry
                {
                    Pair = pair,
                    Category = category,
                    Name = Localization.instance.Localize(pair.Recipe.m_item.m_itemData.m_shared.m_name),
                    FoodValue = Categories.FoodSortValue(pair.Recipe.m_item, category)
                });
            }
            ApplyFilter(gui);
        }

        /// <summary>
        /// Comprueba que las filas visibles guardadas siguen siendo la lista del juego. Si otro mod la ha
        /// montado de nuevo sin pasar por el filtro, las filas ocultas guardadas ya no son de esa lista:
        /// se destruyen (si no, al volver a mostrarlas se superponen a las nuevas) y se guarda la lista actual.
        /// </summary>
        private static void SyncWithGame(InventoryGui gui)
        {
            // Se comparan las filas, no su orden: los filtros de comida reordenan la lista.
            var recipes = gui.m_availableRecipes;
            var current = new HashSet<GameObject>();
            foreach (InventoryGui.RecipeDataPair pair in recipes)
            {
                if (pair.InterfaceElement != null)
                {
                    current.Add(pair.InterfaceElement);
                }
            }
            int visible = 0;
            bool inSync = current.Count == recipes.Count;
            foreach (Entry entry in s_entries)
            {
                GameObject element = entry.Pair.InterfaceElement;
                if (!inSync)
                {
                    break;
                }
                if (element == null || !element.activeSelf)
                {
                    continue;
                }
                visible++;
                inSync = current.Contains(element);
            }
            if (inSync && visible == recipes.Count)
            {
                return;
            }
            foreach (Entry entry in s_entries)
            {
                GameObject element = entry.Pair.InterfaceElement;
                if (element != null && !current.Contains(element))
                {
                    Object.Destroy(element);
                }
            }
            AfterGameRebuild(gui);
        }

        /// <summary>Deja en la lista del juego solo las recetas que pasan el filtro; el resto se oculta.</summary>
        private static void ApplyFilter(InventoryGui gui)
        {
            var recipes = gui.m_availableRecipes;
            recipes.Clear();
            string term = SearchText.Trim();

            // Recetas de cada categoría en esta estación (para quitar los botones vacíos) y las que pasan
            // la búsqueda (para atenuar los botones sin resultados).
            int[] available = new int[Categories.Count];
            int[] matches = new int[Categories.Count];
            bool[] entryMatches = new bool[s_entries.Count];
            for (int i = 0; i < s_entries.Count; i++)
            {
                Category category = s_entries[i].Category;
                available[(int)Category.All]++;
                available[(int)category]++;
                entryMatches[i] = term.Length == 0 || Contains(s_entries[i].Name, term);
                if (entryMatches[i])
                {
                    matches[(int)Category.All]++;
                    matches[(int)category]++;
                }
            }
            s_activeCategory = available[(int)CurrentCategory] > 0 ? CurrentCategory : Category.All;

            // En los filtros de comida, de más a menos vida/vigor/eitr; si no (o a igual valor), el orden del juego.
            int[] order = new int[s_entries.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }
            if (Categories.IsFoodType(s_activeCategory))
            {
                Array.Sort(order, (a, b) =>
                {
                    int byValue = s_entries[b].FoodValue.CompareTo(s_entries[a].FoodValue);
                    return byValue != 0 ? byValue : a.CompareTo(b);
                });
            }

            foreach (int i in order)
            {
                Entry entry = s_entries[i];
                bool visible = entryMatches[i] && (s_activeCategory == Category.All || entry.Category == s_activeCategory);
                GameObject element = entry.Pair.InterfaceElement;
                if (element == null)
                {
                    continue;
                }
                if (element.activeSelf != visible)
                {
                    element.SetActive(visible);
                }
                if (visible)
                {
                    ((RectTransform)element.transform).anchoredPosition = new Vector2(0f, recipes.Count * -gui.m_recipeListSpace);
                    recipes.Add(entry.Pair);
                }
            }

            float height = Mathf.Max(gui.m_recipeListBaseSize, recipes.Count * gui.m_recipeListSpace);
            gui.m_recipeListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            UpdateButtons(available, matches);
        }

        /// <summary>Búsqueda sin distinguir mayúsculas ni tildes ("espada" encuentra "Espada", "pocion" encuentra "Poción").</summary>
        private static bool Contains(string text, string term)
        {
            return CultureInfo.InvariantCulture.CompareInfo.IndexOf(text, term,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        }

        internal static void OnInventoryHidden()
        {
            if (WorkbenchSearchFiltersPlugin.ClearSearchOnClose.Value)
            {
                SearchText = "";
                if (s_search != null)
                {
                    s_search.SetTextWithoutNotify("");
                }
            }
            if (!WorkbenchSearchFiltersPlugin.RememberCategory.Value)
            {
                CurrentCategory = Category.All;
            }
            if (s_search != null && s_search.isFocused)
            {
                s_search.DeactivateInputField();
            }
        }

        /// <summary>Vuelve a filtrar la lista ya montada (sin que el juego la reconstruya) y mantiene la selección.</summary>
        private static void RefreshRecipes()
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || Player.m_localPlayer == null || !InventoryGui.IsVisible())
            {
                return;
            }
            SyncWithGame(gui);
            ApplyFilter(gui);
            if (gui.m_availableRecipes.Count == 0)
            {
                gui.SetRecipe(-1, center: false);
                return;
            }
            // GetSelectedRecipeIndex devuelve 0 si la receta seleccionada ha quedado oculta.
            gui.SetRecipe(gui.m_selectedRecipe.Recipe != null ? gui.GetSelectedRecipeIndex() : 0, center: true);
        }

        /// <summary>Textos que no pasan por el sistema de tooltips (este se traduce solo al mostrarse).</summary>
        internal static void RefreshTexts()
        {
            if (s_root == null)
            {
                return;
            }
            if (s_search.placeholder is TMP_Text placeholder)
            {
                placeholder.text = Localization.instance.Localize("$wsf_search");
            }
            if (s_allLabel != null)
            {
                s_allLabel.text = Localization.instance.Localize("$wsf_cat_all");
            }
        }

        /// <summary>
        /// Muestra solo los filtros de esta estación que tienen alguna receta, en el orden de la estación,
        /// y atenúa los que no tienen resultados para la búsqueda.
        /// </summary>
        private static void UpdateButtons(int[] available, int[] matches)
        {
            Category[] stationCategories = Categories.ForStation(s_foodStation);
            if (s_buttonsFoodStation != s_foodStation)
            {
                s_buttonsFoodStation = s_foodStation;
                foreach (Category category in stationCategories)
                {
                    CategoryButton button = s_buttons.Find(b => b.Category == category);
                    button?.Background.transform.SetAsLastSibling();
                }
            }

            foreach (CategoryButton button in s_buttons)
            {
                bool shown = button.Category == Category.All ||
                             (Array.IndexOf(stationCategories, button.Category) >= 0 && available[(int)button.Category] > 0);
                GameObject go = button.Background.gameObject;
                if (go.activeSelf != shown)
                {
                    go.SetActive(shown);
                }
                button.Background.color = button.Category == s_activeCategory ? s_selectedColor : s_buttonColor;
                Color color = button.ContentColor;
                if (matches[(int)button.Category] == 0)
                {
                    color.a *= EmptyIconAlpha;
                }
                button.Content.color = color;
            }
        }

        // ---------------------------------------------------------------- creación de la UI

        private static void Create(InventoryGui gui)
        {
            s_buttons.Clear();
            s_buttonsFoodStation = null;
            s_allLabel = null;

            // La lista de recetas está dentro de un ScrollRect: la barra ocupa su parte superior y el ScrollRect se acorta.
            ScrollRect scroll = gui.m_recipeListRoot.GetComponentInParent<ScrollRect>();
            RectTransform list = scroll != null ? (RectTransform)scroll.transform : (RectTransform)gui.m_recipeListRoot.parent;
            RectTransform parent = (RectTransform)list.parent;

            // Contenedor con la misma geometría que tenía la lista; la barra se ancla arriba dentro de él.
            s_root = new GameObject("WSF_FilterArea", typeof(RectTransform));
            RectTransform area = (RectTransform)s_root.transform;
            area.SetParent(parent, false);
            area.anchorMin = list.anchorMin;
            area.anchorMax = list.anchorMax;
            area.pivot = list.pivot;
            area.offsetMin = list.offsetMin;
            area.offsetMax = list.offsetMax;
            area.SetSiblingIndex(list.GetSiblingIndex() + 1);

            RectTransform bar = NewRect("WSF_FilterBar", area);
            SetTop(bar, 0f, BarHeight - Gap);

            // Acortar la lista (y su barra de scroll si está al lado y no dentro).
            list.offsetMax -= new Vector2(0f, BarHeight);
            s_gui = gui;
            s_list = list;
            s_scrollbar = null;
            RectTransform scrollbar = gui.m_recipeListScroll != null ? (RectTransform)gui.m_recipeListScroll.transform : null;
            if (scrollbar != null && scrollbar.parent == parent)
            {
                scrollbar.offsetMax -= new Vector2(0f, BarHeight);
                s_scrollbar = scrollbar;
            }
            gui.m_recipeListBaseSize -= BarHeight;

            TMP_Text referenceText = gui.m_recipeElementPrefab.transform.Find("name").GetComponent<TMP_Text>();
            CreateSearchField(bar, referenceText);
            CreateCategoryButtons(gui, bar, referenceText);
        }

        private static void CreateSearchField(RectTransform bar, TMP_Text referenceText)
        {
            // Preferir una copia del buscador del menú de construcción para que tenga el mismo aspecto que el juego.
            TMP_InputField source = Hud.instance != null && Hud.instance.m_buildUi != null ? Hud.instance.m_buildUi.m_searchField : null;
            GameObject go = null;
            if (source != null)
            {
                try
                {
                    go = Object.Instantiate(source.gameObject, bar, false);
                    go.name = "WSF_Search";
                    // Componentes que traducen el texto al activarse: pisarían el placeholder propio.
                    foreach (MonoBehaviour behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (behaviour != null && behaviour.GetType().Name == "Localize")
                        {
                            Object.DestroyImmediate(behaviour);
                        }
                    }
                    s_search = go.GetComponent<TMP_InputField>();
                    RemoveExtras(go, s_search);
                }
                catch (Exception e)
                {
                    WorkbenchSearchFiltersPlugin.Log.LogWarning("Could not copy the build menu search field, using a simple one: " + e.Message);
                    if (go != null)
                    {
                        Object.Destroy(go);
                    }
                    go = null;
                }
            }
            if (go == null)
            {
                go = CreateSimpleInputField(bar, referenceText);
                s_search = go.GetComponent<TMP_InputField>();
            }

            go.SetActive(true);
            LayoutElement layout = go.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.ignoreLayout = true;
            }
            SetTop((RectTransform)go.transform, 0f, SearchHeight);

            s_search.onValueChanged = new TMP_InputField.OnChangeEvent();
            s_search.onEndEdit = new TMP_InputField.SubmitEvent();
            s_search.onSubmit = new TMP_InputField.SubmitEvent();
            s_search.restoreOriginalTextOnEscape = false;
            s_search.characterLimit = 40;
            s_search.SetTextWithoutNotify(SearchText);
            s_search.onValueChanged.AddListener(text =>
            {
                SearchText = text ?? "";
                RefreshRecipes();
            });
        }

        /// <summary>
        /// Quita de la copia del buscador de construcción lo que no es el propio campo de texto, como el indicador
        /// de la tecla F (en el menú de construcción la F enfoca el buscador; aquí no hace nada).
        /// </summary>
        private static void RemoveExtras(GameObject go, TMP_InputField input)
        {
            var keep = new HashSet<Transform>();
            foreach (Component part in new Component[] { input.textViewport, input.textComponent, input.placeholder })
            {
                for (Transform t = part != null ? part.transform : null; t != null && t != go.transform; t = t.parent)
                {
                    keep.Add(t);
                }
            }
            var remove = new List<GameObject>();
            foreach (Transform child in go.transform)
            {
                if (!keep.Contains(child))
                {
                    remove.Add(child.gameObject);
                }
            }
            foreach (GameObject extra in remove)
            {
                Object.DestroyImmediate(extra);
            }
        }

        private static GameObject CreateSimpleInputField(RectTransform bar, TMP_Text referenceText)
        {
            RectTransform rect = NewRect("WSF_Search", bar);
            GameObject go = rect.gameObject;
            go.SetActive(false);
            Image background = go.AddComponent<Image>();
            background.color = s_buttonColor;

            RectTransform textArea = NewRect("Text Area", rect);
            Stretch(textArea, new Vector2(8f, 2f), new Vector2(-8f, -2f));
            textArea.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI placeholder = NewText("Placeholder", textArea, referenceText, 16f);
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(1f, 1f, 1f, 0.45f);

            TextMeshProUGUI text = NewText("Text", textArea, referenceText, 16f);

            TMP_InputField input = go.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.textViewport = textArea;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = referenceText.font;
            input.pointSize = 16f;
            return go;
        }

        private static void CreateCategoryButtons(InventoryGui gui, RectTransform bar, TMP_Text referenceText)
        {
            RectTransform row = NewRect("WSF_Categories", bar);
            SetTop(row, SearchHeight + Gap, ButtonsHeight);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 3f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            UITooltip tooltipSource = gui.m_craftButton != null ? gui.m_craftButton.GetComponent<UITooltip>() : null;

            Sprite foodIcon = FindFoodIcon(gui);

            foreach (Category category in Categories.Buttons)
            {
                RectTransform rect = NewRect("WSF_Cat_" + category, row);
                Image background = rect.gameObject.AddComponent<Image>();
                background.color = s_buttonColor;

                Button button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                ColorBlock colors = button.colors;
                colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                colors.selectedColor = Color.white;
                button.colors = colors;
                Category captured = category;
                button.onClick.AddListener(() => OnCategoryClicked(captured));

                LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
                element.flexibleWidth = category == Category.All ? 1.8f : 1f;

                Graphic content;
                Color? foodColor = Categories.FoodColor(category);
                Color contentColor = foodColor ?? Color.white;
                Sprite icon = category == Category.All ? null : foodColor.HasValue ? foodIcon : FindIcon(category);
                if (icon != null)
                {
                    RectTransform iconRect = NewRect("icon", rect);
                    Stretch(iconRect, new Vector2(2f, 2f), new Vector2(-2f, -2f));
                    Image image = iconRect.gameObject.AddComponent<Image>();
                    image.sprite = icon;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    content = image;
                }
                else
                {
                    TextMeshProUGUI label = NewText("label", rect, referenceText, 14f);
                    label.alignment = TextAlignmentOptions.Center;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 8f;
                    label.fontSizeMax = 14f;
                    label.text = Localization.instance.Localize(Categories.Token(category));
                    content = label;
                    if (category == Category.All)
                    {
                        s_allLabel = label;
                    }
                }

                if (tooltipSource != null && category != Category.All)
                {
                    UITooltip tooltip = rect.gameObject.AddComponent<UITooltip>();
                    tooltip.m_tooltipPrefab = tooltipSource.m_tooltipPrefab;
                    tooltip.m_text = Categories.Token(category); // el tooltip lo traduce al mostrarse
                }

                s_buttons.Add(new CategoryButton { Category = category, Background = background, Content = content, ContentColor = contentColor });
            }
        }

        /// <summary>Icono de cubiertos del HUD (junto a la barra de vida); si no, el indicador de comida del inventario.</summary>
        private static Sprite FindFoodIcon(InventoryGui gui)
        {
            if (Hud.instance != null && Hud.instance.m_foodIcon != null && Hud.instance.m_foodIcon.sprite != null)
            {
                return Hud.instance.m_foodIcon.sprite;
            }
            InventoryElement element = gui.m_playerGrid != null && gui.m_playerGrid.m_elementPrefab != null
                ? gui.m_playerGrid.m_elementPrefab.GetComponent<InventoryElement>()
                : null;
            return element != null && element.m_food != null ? element.m_food.sprite : null;
        }

        private static void OnCategoryClicked(Category category)
        {
            CurrentCategory = category;
            RefreshRecipes();
        }

        private static Sprite FindIcon(Category category)
        {
            if (ObjectDB.instance == null)
            {
                return null;
            }
            foreach (string iconPrefab in Categories.IconPrefabs(category))
            {
                string[] parts = iconPrefab.Split(':');
                GameObject prefab = ObjectDB.instance.GetItemPrefab(parts[0]);
                ItemDrop itemDrop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                Sprite[] icons = itemDrop != null ? itemDrop.m_itemData.m_shared.m_icons : null;
                if (icons == null || icons.Length == 0)
                {
                    continue;
                }
                int variant = parts.Length > 1 && int.TryParse(parts[1], out int parsed) && parsed >= 0 && parsed < icons.Length ? parsed : 0;
                return icons[variant];
            }
            return null;
        }

        // ---------------------------------------------------------------- utilidades de RectTransform

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Ancla el rect arriba del padre, a lo ancho, a "top" píxeles del borde superior y con la altura dada.</summary>
        private static void SetTop(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, -top);
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static TextMeshProUGUI NewText(string name, RectTransform parent, TMP_Text reference, float size)
        {
            RectTransform rect = NewRect(name, parent);
            Stretch(rect, Vector2.zero, Vector2.zero);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = reference.font;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }
    }
}
