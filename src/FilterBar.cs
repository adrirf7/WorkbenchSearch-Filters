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
        private static readonly Color s_iconColor = Color.white;
        private static readonly Color s_emptyIconColor = new Color(1f, 1f, 1f, 0.3f);

        private sealed class CategoryButton
        {
            public Category Category;
            public Image Background;
            public Graphic Content;
        }

        private static GameObject s_root;
        private static TMP_InputField s_search;
        private static TMP_Text s_allLabel;
        private static readonly List<CategoryButton> s_buttons = new List<CategoryButton>();

        internal static Category CurrentCategory = Category.All;
        internal static string SearchText = "";

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

        // ---------------------------------------------------------------- filtrado

        // Lista completa (ya ordenada) que montó el juego la última vez, con la categoría y el nombre ya calculados.
        // Al escribir o cambiar de categoría solo se ocultan/muestran sus filas: no se vuelve a montar la lista,
        // que es lo que causaba tirones (el juego destruye y crea de nuevo todas las filas).
        private struct Entry
        {
            public InventoryGui.RecipeDataPair Pair;
            public Category Category;
            public string Name;
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
            foreach (InventoryGui.RecipeDataPair pair in gui.m_availableRecipes)
            {
                s_entries.Add(new Entry
                {
                    Pair = pair,
                    Category = Categories.Classify(pair.Recipe.m_item),
                    Name = Localization.instance.Localize(pair.Recipe.m_item.m_itemData.m_shared.m_name)
                });
            }
            ApplyFilter(gui);
        }

        /// <summary>Deja en la lista del juego solo las recetas que pasan el filtro; el resto se oculta.</summary>
        private static void ApplyFilter(InventoryGui gui)
        {
            var recipes = gui.m_availableRecipes;
            recipes.Clear();
            int[] counts = new int[Categories.All.Length];
            string term = SearchText.Trim();

            foreach (Entry entry in s_entries)
            {
                bool matchesSearch = term.Length == 0 || Contains(entry.Name, term);
                if (matchesSearch)
                {
                    // Los contadores respetan la búsqueda para atenuar las categorías sin resultados.
                    counts[(int)Category.All]++;
                    counts[(int)entry.Category]++;
                }
                bool visible = matchesSearch && (CurrentCategory == Category.All || entry.Category == CurrentCategory);
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

            UpdateButtons(counts);
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

        private static void UpdateButtons(int[] counts)
        {
            foreach (CategoryButton button in s_buttons)
            {
                button.Background.color = button.Category == CurrentCategory ? s_selectedColor : s_buttonColor;
                button.Content.color = counts[(int)button.Category] > 0 ? s_iconColor : s_emptyIconColor;
            }
        }

        // ---------------------------------------------------------------- creación de la UI

        private static void Create(InventoryGui gui)
        {
            s_buttons.Clear();
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
            RectTransform scrollbar = gui.m_recipeListScroll != null ? (RectTransform)gui.m_recipeListScroll.transform : null;
            if (scrollbar != null && scrollbar.parent == parent)
            {
                scrollbar.offsetMax -= new Vector2(0f, BarHeight);
            }
            gui.m_recipeListBaseSize = Mathf.Max(0f, gui.m_recipeListBaseSize - BarHeight);

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

            foreach (Category category in Categories.All)
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
                Sprite icon = category == Category.All ? null : FindIcon(category);
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

                s_buttons.Add(new CategoryButton { Category = category, Background = background, Content = content });
            }
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
            foreach (string prefabName in Categories.IconPrefabs(category))
            {
                GameObject prefab = ObjectDB.instance.GetItemPrefab(prefabName);
                ItemDrop itemDrop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (itemDrop != null && itemDrop.m_itemData.m_shared.m_icons.Length > 0)
                {
                    return itemDrop.m_itemData.GetIcon();
                }
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
