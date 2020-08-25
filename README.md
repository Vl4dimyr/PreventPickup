# PreventPickup

## Description

This mod prevents picking up items you don't want and gives you scrap or a random item of the same rarity instead (configurable).

## Config

The config file (`\BepInEx\config\de.userstorm.preventpickup.cfg`) will be crated automatically when the mod is loaded.
You need to restart the game for changes to apply in game.

With the following config **example** picking up a `Soldier's Syringe` would have a 33% chance of giving a random white item, otherwise [White Scrap](https://riskofrain2.gamepedia.com/Item_Scrap,_White). Picking up `Tougher Times` would **not** be prevented.

```ini
[Balance]

## Chance to get a random white item when a white item pick up is prevented. (0.00 to 1.00, 0.00 == 0%, 1.0 == 100%)
# Setting type: Single
# Default value: 0.05
WhiteRandomItemChance = 0.33

...

[PreventPickup]

## Item index: 0 | Name: Soldier's Syringe | Tier: Tier1
# Setting type: Boolean
# Default value: false
Syringe = true

## Item index: 1 | Name: Tougher Times | Tier: Tier1
# Setting type: Boolean
# Default value: false
Bear = false

...
```

> If you want to play without shields set `PersonalShield`, `ShieldOnly` and `HeadHunter` pickup prevention to `true`

## Manual Install

- Install [BepInEx](https://thunderstore.io/package/bbepis/BepInExPack/) and [R2API](https://thunderstore.io/package/tristanmcpherson/R2API/)
- Download the latest `PreventPickup_x.y.z.zip` [here](https://thunderstore.io/package/Vl4dimyr/PreventPickup/)
- Extract and move the `PreventPickup.dll` into the `\BepInEx\plugins` folder

## Changelog

The [Changelog](https://github.com/Vl4dimyr/PreventPickup/blob/master/CHANGELOG.md) can be found on GitHub.

## Bugs/Feedback

For bugs or feedback please use [GitHub Issues](https://github.com/Vl4dimyr/PreventPickup/issues).
