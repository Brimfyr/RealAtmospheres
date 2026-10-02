# Real Atmospheres

Atmospheres, hazes and clouds for Kitten Space Agency's Solar System, derived from each body's composition and particle optics, and flight atmospheres from measured profiles. A [StarMap](https://github.com/StarMapLoader/StarMap) mod.

No game files are modified. At launch the mod builds patched copies of the game's shaders and system XML and loads those instead, and replaces the game's flight atmosphere in memory. If a game update changes a patched part of the XML, that patch is skipped. A game update that changes the atmosphere shaders can stop them compiling, so use a release built for your KSA version.

## Features

### Flight atmospheres

- Drag and engine back-pressure follow measured density and pressure profiles instead of the game's single exponential: Earth (US Standard Atmosphere 1976), Venus (VIRA), Mars (Pathfinder and Phoenix entry data), Titan (Huygens), Jupiter (Galileo probe), Saturn, Uranus and Neptune (Voyager and Cassini occultations), Triton (Voyager) and Pluto (New Horizons)
- The edge of each atmosphere, where drag starts and time warp is limited, moves to where the real one thins out: Titan 996 km (stock 480), Venus 143 km (stock 455), Uranus 2,023 km (stock 167)

### Earth

- Gas density from the US Standard Atmosphere 1976 instead of a single exponential, so the blue limb thins out at the right height

### Mars

- Rayleigh scattering from a 95% CO₂ atmosphere at 6.1 mbar, thinning out as Pathfinder and Phoenix measured
- Dust scattering from Mie theory, with a 9 km scale height
- Exact Mie phase function for Martian dust, giving the blue sun halo and blue sunsets
- Double-exponential dust density profile from the mean surface, thicker in the basins
- Water-ice cirrus at 22–29 km, in 2D and volumetric

### Venus

- Dense CO₂ Rayleigh scattering, at 53× Earth's surface density
- Gas density from the Venus International Reference Atmosphere, falling off fast above the clouds
- One solid cloud deck at 48–72 km, densest at its base and solid to about 70 km, where Venus's cloud tops are measured, with the game's cloud patterns as low lumps on top (stock: separate cumulus columns at 10–45 km, some towering 13 km, with holes between them), in Venus's measured pale cream white
- Blue-absorbing haze below the clouds
- A lower haze that thickens toward the cloud base, as the Venera probes measured, so the clear air near the ground turns gradually into the clouds

### The giants

- H₂/He Rayleigh scattering at the gas's real strength, from each planet's composition at 1 bar, with the gas density measured by the Galileo probe (Jupiter), Voyager and Cassini

### Jupiter

- Cloud decks from −1 to 12 km and 15 to 68 km, the upper one reaching Jupiter's visible edge, where its measured haze turns opaque to a grazing view (about 30 mbar)
- A thin stratospheric haze above the clouds, as Cassini measured it

### Saturn

- Clouds that reach where Cassini found the top of Saturn's haze, about 50 mbar
- A thin stratospheric haze above them, as Hubble and Cassini measured it, which turns the gas's blue limb into a paler rim
- Methane colouring
- Volumetric clouds, with cloud height following the planet's map
- Colour map at 2048 px per cube face (stock: 1024)

### Uranus and Neptune

- Methane absorption, held below the tropopause where the real methane is, and a thin haze
- Volumetric clouds, with cloud height following the planet's map
- Colour maps at 2048 px per cube face (stock: 512)

### Titan

- Golden haze from Huygens and Cassini measurements: optical depth 7, scale height 65 km below 140 km and 45 km above, with the nitrogen at its measured density; the atmosphere is visible out to its edge near 1,000 km
- A short gold-to-blue transition at the top of the main haze, around 280 km
- The disc's colour matches Titan's measured full-disc spectrum; the upper haze scatters blue like the gas
- Stratified upper haze, animated with simulation time
- Haze layers at 290 km and 380 km, where Cassini saw the detached haze around the 2009–10 equinox: soft bands 30–40 km thick among the stratified haze
- White methane clouds in two polar caps, with clear tropics, instead of the game's brown Venus cloud pattern

### Pluto and Triton

- N₂ Rayleigh scattering from measured surface pressure and temperature
- Pluto's air and haze as New Horizons measured them: the haze thins out with the nitrogen and is as bright as the spacecraft saw it from every angle, a faint blue rim in sunlight and a bright ring when lit from behind
- Triton's air and haze as Voyager measured them: one even haze layer close to the ground, visible as a ring when looking toward the Sun
- Stratified haze bands on Pluto, animated with simulation time
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
- Mars cirrus, Titan haze and cloud, and Triton cloud masks: *Produced in part with SpaceEngine PRO © Cosmographic Software LLC.*

The code is MIT. Because of the Uranus map, the release as a whole is for non-commercial use. [CREDITS.md](CREDITS.md) gives the source and terms of every shipped file.
