# WorkbenchSearchFilters

Tired of scrolling through dozens of recipes at the forge? **WorkbenchSearchFilters** adds a **search box** and **category filters** to the recipe list of every crafting station: workbench, forge, black forge, cauldron, artisan table, galdr table... and hand crafting too.

![Category filters at the black forge](https://raw.githubusercontent.com/adrirf7/WorkbenchSearchFilters/main/docs/crafting_list.png)

![Searching for "black" at the forge](https://raw.githubusercontent.com/adrirf7/WorkbenchSearchFilters/main/docs/search.png)

## Features

- **Search box:** filters as you type by the item's name in the current game language. Case and accents are ignored.
- **Category buttons:** All, Weapons, Armor, Tools, Consumables, Materials, Misc. Hover a button to see its name. Categories with no recipes at the current station (or for the current search) are dimmed.
- **Typing doesn't control the game:** while the search box has focus, your character doesn't move and E or Tab don't close the inventory.
- **Works in both Craft and Upgrade tabs**, at every station, including modded ones.
- **Translated:** follows the game language, also when changed in Settings. 22 languages included; any other language falls back to English.
- **Client-side only:** nothing is needed on the server, and other players don't need the mod.

## Categories

Items are sorted by their own game data, so items added by other mods are categorised automatically:

| Category | What goes in it |
|---|---|
| Weapons | Swords, axes, maces, spears, atgeirs, knives, bows, crossbows, staffs... |
| Armor | Helmets, chest and leg armor, capes, shields, belts, trinkets |
| Tools | Hammer, hoe, cultivator, pickaxes, fishing rod, torches |
| Consumables | Food, meads, arrows, bolts, bait, bombs, and oven / cooking station / fermenter ingredients (bread dough, uncooked pies, mead bases) |
| Materials | Crafting materials |
| Misc | Everything else (tankards, etc.) |

Not happy with where an item ended up? Every time you enter a world, `BepInEx/config/WorkbenchSearchFilters.adrirf7.Categories.txt` lists every craftable item with its prefab name and category. Add `Prefab:Category` to `CategoryOverrides` in the config to move it.

## Configuration

`BepInEx/config/WorkbenchSearchFilters.adrirf7.cfg`:

- `ClearSearchOnClose` (default `true`): clear the search text when the inventory is closed.
- `RememberCategory` (default `true`): keep the selected category between openings.
- `CategoryOverrides`: force the category of specific items, as `Prefab:Category` separated by commas (`Weapons`, `Armor`, `Tools`, `Consumables`, `Materials`, `Misc`).

## Languages

English, Spanish, German, French, Italian, Portuguese (Brazil and Portugal), Russian, Ukrainian, Polish, Czech, Dutch, Swedish, Norwegian, Danish, Finnish, Turkish, Hungarian, Chinese (simplified and traditional), Japanese and Korean.

## Changelog

- **1.0.0:** first release.

## Source code

[github.com/adrirf7/WorkbenchSearchFilters](https://github.com/adrirf7/WorkbenchSearchFilters)
