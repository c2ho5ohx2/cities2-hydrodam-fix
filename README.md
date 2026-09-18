# HydroDam Fix 1.6.2f1

A compatibility fix for hydroelectric dams broken in Cities: Skylines II 1.6.2f1.

## What it fixes

After the 1.6.2f1 update, hydroelectric dams can incorrectly:

- produce 0 MW
- show -100% Low Water Level despite sufficient water
- show 0 W during placement
- fail to automatically orient according to river flow

This mod restores the affected CPU water-flow readback behavior.

## Technical summary

Cities: Skylines II 1.6.2f1 changed the CPU velocity readback format from:

`R32G32B32A32_SFloat`

to:

`R16G16B16A16_SFloat`

while the receiving CPU staging path still uses `Unity.Mathematics.float4`.

HydroDam Fix detects this exact affected combination and restores the readback format to:

`R32G32B32A32_SFloat`

The mod does not change:

- the water simulation itself
- the hydroelectric production formula
- save-game data
- GPU water simulation textures

The release build is guarded: if the game implementation no longer matches the known 1.6.2f1 regression, the mod makes no change.

## Compatibility

Target game version:

**Cities: Skylines II 1.6.2f1**

Initial testing was performed under Steam/Proton on Linux.

The mod is designed to become a no-op if the game is officially fixed or the affected implementation changes.

## Installation

The recommended installation method will be through Paradox Mods.

Manual development installation is not recommended for normal users.

## Source

This repository contains only the mod's own source code.

It does not contain Cities: Skylines II game binaries, decompiled game source, or other copyrighted game assets.

## License

MIT License.

## Disclaimer

This is an unofficial community mod and is not affiliated with or endorsed by Paradox Interactive, Iceflake Studios, or Colossal Order.
