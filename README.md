# Real Atmospheres

Atmospheres, hazes and clouds for Kitten Space Agency's Solar System, derived from each body's composition and particle optics. A [StarMap](https://github.com/StarMapLoader/StarMap) mod.

No game files are modified. At launch the mod builds patched copies of the game's shaders and system XML and loads those instead. If a game update changes a patched part of the XML, that patch is skipped. A game update that changes the atmosphere shaders can stop them compiling, so use a release built for your KSA version.

## Features

### Mars

- Rayleigh scattering from a 95% CO₂ atmosphere at 6.1 mbar
- Dust scattering from Mie theory, with a 9 km scale height
- Exact Mie phase function for Martian dust, giving the blue sun halo and blue sunsets
- Double-exponential dust density profile
- Water-ice cirrus at 22–29 km, in 2D and volumetric

### Venus

- Dense CO₂ Rayleigh scattering, at 53× Earth's surface density
- Blue-absorbing haze below the clouds

### Jupiter

- H₂/He Rayleigh scattering, with a 25 km scale height
- Cloud decks at real altitudes: lower deck −1 to 12 km, main deck from 15 km, storms up to 96 km

### Saturn

- Rayleigh scattering above a thin haze, with methane colouring and a 52 km scale height
- Volumetric clouds, with cloud height following the planet's map
- Colour map at 2048 px per cube face (stock: 1024)

### Uranus and Neptune

- Methane absorption, with a thin Rayleigh limb and haze
- Volumetric clouds, with cloud height following the planet's map
- Colour maps at 2048 px per cube face (stock: 512)

### Titan

- Stratified upper haze, animated with simulation time
- Detached haze layers at 230 and 290 km

### Pluto and Triton

- N₂ Rayleigh scattering from measured surface pressure and temperature
- Forward-scattering haze, visible as a ring when looking toward the Sun
- Stratified haze bands, animated with simulation time
- Nitrogen-ice clouds on Triton at 4–6.5 km

## Installation

Requires [StarMap](https://github.com/StarMapLoader/StarMap). Each release is named with the KSA build it was tested on.

**With Borea:** install Real Atmospheres from the mod list.

**Manually:**

1. Install StarMap and run the game once through `StarMap.Loader.exe`.
2. Extract the release into `Documents\My Games\Kitten Space Agency\mods\`, giving `mods\RealAtmospheres\RealAtmospheres.dll`.
3. Launch the game once and close it. KSA adds new mods to `manifest.toml` disabled: set this mod's entry to `enabled = true`.
4. Launch through StarMap.

To uninstall, delete `mods\RealAtmospheres` and `mods\_RealSharedShadow`.

## Building

`deploy.ps1` builds the mod and installs it into your mods folder. `package.ps1 -Version x.y.z -GameBuild vYYYY.M.D.NNNN` builds a release archive in `dist\`. Set `StarMapDir` to your StarMap folder.

## Credits

- Saturn map mixed by JCP-JohnCarlo, from work by HellcatF6F, MrSpace43-Celestia, FarGetaNik and Snowfall-The-Cat.
- Uranus map by Askaniy, [CC BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/). Our modified Uranus map, cloud mask and flowmap are released under the same licence.
- Neptune map by Askaniy, [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/), modified.
- Mars cirrus, Titan haze and Triton cloud masks: *Produced in part with SpaceEngine PRO © Cosmographic Software LLC.*

The code is MIT. Because of the Uranus map, the release as a whole is for non-commercial use. [CREDITS.md](CREDITS.md) gives the source and terms of every shipped file.
