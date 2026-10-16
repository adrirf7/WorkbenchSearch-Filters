# WorkbenchSearchFilters

A BepInEx mod for Valheim that adds a search box and category filters (weapons, armor, tools, consumables, materials, misc) to the recipe list of every crafting station: workbench, forge, black forge, cauldron, artisan table, galdr table... and hand crafting too.

[Versión en español](README.es.md)

![Category filters at the black forge](docs/crafting_list.png)

![Searching for "black" at the forge](docs/search.png)

## Features

- **Search box** above the recipe list: filters as you type by the item's name in the current game language. Case and accents are ignored ("pocion" finds "Poción").
- **Category buttons:** All, Weapons, Armor, Tools, Consumables, Materials, Misc. Each button shows its name on hover, and categories with no recipes at the current station (or for the current search) are dimmed.
- **Typing doesn't control the game:** while the search box has focus, your character doesn't move and keys like E or Tab don't close the inventory.
- **Works in both Craft and Upgrade tabs**, at every station, including modded ones.
- **Translated:** follows the game language, also when changed in Settings. 22 languages: English, Spanish, German, French, Italian, Portuguese (Brazil and Portugal), Russian, Ukrainian, Polish, Czech, Dutch, Swedish, Norwegian, Danish, Finnish, Turkish, Hungarian, Chinese (simplified and traditional), Japanese and Korean. Any other language falls back to English.
- **Client-side only:** nothing is needed on the server, and other players don't need the mod.

## Categories

Items are sorted by their own game data, so items added by other mods are categorised automatically:

| Category | What goes in it |
|---|---|
| Weapons | Swords, axes, maces, spears, atgeirs, knives, bows, crossbows, staffs... |
| Armor | Helmets, chest and leg armor, capes, shields, belts, trinkets |
| Tools | Hammer, hoe, cultivator, pickaxes, fishing rod, torches |
| Consumables | Food, meads, arrows, bolts, bait, bombs, and ingredients for the oven, cooking station or fermenter (bread dough, uncooked pies, mead bases) |
| Materials | Crafting materials |
| Misc | Everything else (tankards, etc.) |

Every time you enter a world, `BepInEx/config/WorkbenchSearchFilters.adrirf7.Categories.txt` lists every craftable item with its prefab name and category. To move an item, add `Prefab:Category` to `CategoryOverrides` in the config file.

## Configuration

`BepInEx/config/WorkbenchSearchFilters.adrirf7.cfg`, created the first time the mod loads:

- `ClearSearchOnClose` (default `true`): clear the search text when the inventory is closed.
- `RememberCategory` (default `true`): keep the selected category between openings.
- `CategoryOverrides`: force the category of specific items, as `Prefab:Category` separated by commas. Categories: `Weapons`, `Armor`, `Tools`, `Consumables`, `Materials`, `Misc`.

## How it works

The code only uses Harmony patches and Unity UI components. It does not access the network or any files outside the game process, except for writing its own config file and `WorkbenchSearchFilters.adrirf7.Categories.txt` in `BepInEx/config`.

- `src/FilterBar.cs`: builds the search box (a copy of the build menu's search field) and the category buttons on top of the recipe list, and filters the list. The game builds the list once; searching or changing category only hides and shows existing rows.
- `src/Categories.cs`: decides each item's category from its item type, food stats, attack data and the game's cooking stations, and writes the category report.
- `src/Patches.cs`: Harmony patches on `InventoryGui.UpdateRecipeList` (filtering), `InventoryGui.Hide` (reset on close) and `Chat.HasFocus` (blocks game input while typing).
- `src/Translations.cs`: registers the mod's text with Valheim's localization.
- `src/Plugin.cs`: BepInEx entry point and configuration.

## Building

Requirements:

- [.NET SDK 8](https://dotnet.microsoft.com/download) or newer
- Valheim with [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) installed (the project references the game's and BepInEx's assemblies from the install folder)

Steps:

```
git clone https://github.com/adrirf7/WorkbenchSearchFilters
cd WorkbenchSearchFilters
dotnet build -c Release -p:ValheimDir="C:\Program Files (x86)\Steam\steamapps\common\Valheim" -p:DeployToGame=false
```

The compiled mod is `bin/Release/WorkbenchSearchFilters.dll`. `ValheimDir` defaults to the Steam path above. Without `-p:DeployToGame=false`, the build also copies the DLL to `BepInEx/plugins/WorkbenchSearchFilters/` in that Valheim install.

The NuGet package `BepInEx.AssemblyPublicizer.MSBuild` is restored automatically. It lets the code access non-public members of `assembly_valheim.dll` at compile time, which is standard practice for Valheim mods.

## Installation

1. Install BepInExPack Valheim.
2. Put `WorkbenchSearchFilters.dll` in `BepInEx/plugins/WorkbenchSearchFilters/`.

## Changelog

- **1.0.0:** first release.
