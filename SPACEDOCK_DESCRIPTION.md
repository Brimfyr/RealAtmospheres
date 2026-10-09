# Real Atmospheres

Atmospheres, hazes and clouds for Kitten Space Agency's Solar System, derived from each body's composition and particle optics, with flight atmospheres from measured profiles. A [StarMap](https://github.com/StarMapLoader/StarMap) mod.

No game files are modified: the mod loads patched copies of the game's shaders and system data at launch. Use the release built for your KSA version.

## Download

[Borea](https://github.com/KSAModding/content-index/blob/main/listings/RealAtmospheres.toml)

[GitHub](https://github.com/Brimfyr/RealAtmospheres/)

## Features

### Clouds

* Volumetric clouds stay in view from much higher up before the 2D layer takes over
* Extended cloud self-shadowing

### Flight atmospheres

* Drag and engine back-pressure follow measured density and pressure profiles on Earth, Venus, Mars, Titan, Jupiter, Saturn, Uranus, Neptune, Triton and Pluto
* Each atmosphere ends at its real height

### Earth

![Earth's terminator from low orbit](https://raw.githubusercontent.com/Brimfyr/RealAtmospheres/main/content/earth-low.jpg)

* Gas density from the US Standard Atmosphere 1976
* Ozone layer shaped after the same standard

### Mars

![Mars from orbit](https://raw.githubusercontent.com/Brimfyr/RealAtmospheres/main/content/mars-orbit.jpg)
![Mars's dusty sky from the surface](https://raw.githubusercontent.com/Brimfyr/RealAtmospheres/main/content/mars-surface.jpg)
![Mars's blue sunset](https://raw.githubusercontent.com/Brimfyr/RealAtmospheres/main/content/mars-sunset.jpg)

* CO₂ Rayleigh scattering, with density from Pathfinder and Phoenix
* Dust scattering from Mie theory, giving the blue sun halo and blue sunsets
* Dust that thickens into the basins
* Water-ice cirrus at 22–29 km

### Venus

![Venus from orbit](https://raw.githubusercontent.com/Brimfyr/RealAtmospheres/main/content/venus-orbit.jpg)
![Venus's surface under its haze](https://raw.githubusercontent.com/Brimfyr/RealAtmospheres/main/content/venus-surface.jpg)

* Dense CO₂ atmosphere, with density from the Venus International Reference Atmosphere
* A single solid cloud deck at 48–72 km, in Venus's measured pale cream
* Opaque clouds from orbit, with subtle darker markings
* Clouds that circle the planet in about four days
* Blue-absorbing haze that thickens toward the cloud base

### The giants

* H₂/He Rayleigh scattering at its real strength, from each planet's composition and measured gas density
* Volumetric cloud decks that keep their brightness from close up to orbit
* Cloud maps that flow with each planet's winds

### Jupiter

![Jupiter's clouds](https://raw.githubusercontent.com/Brimfyr/RealAtmospheres/main/content/jupiter-low.jpg)

* Bright zones high and dark belts low in the volumetric clouds, with deep vortices
* A thin stratospheric haze

### Saturn

![Saturn's clouds and rings](https://raw.githubusercontent.com/Brimfyr/RealAtmospheres/main/content/saturn-low.jpg)

* Volumetric clouds, with cloud height following the planet's map
* A thin stratospheric haze
* Methane colouring
* 2048 px colour map

### Uranus and Neptune

![Uranus's clouds](https://raw.githubusercontent.com/Brimfyr/RealAtmospheres/main/content/uranus-low.jpg)
![Neptune's clouds](https://raw.githubusercontent.com/Brimfyr/RealAtmospheres/main/content/neptune-low.jpg)

* Volumetric clouds, with cloud height following each planet's map
* Methane absorption and a thin haze
* 2048 px colour maps

### Titan

![Titan from orbit](https://raw.githubusercontent.com/Brimfyr/RealAtmospheres/main/content/titan-orbit.jpg)
![Titan's terminator](https://raw.githubusercontent.com/Brimfyr/RealAtmospheres/main/content/titan-terminator.jpg)
![Titan's haze layers, with Saturn beyond](https://raw.githubusercontent.com/Brimfyr/RealAtmospheres/main/content/titan-hazes.jpg)

* Golden haze from Huygens and Cassini measurements
* Haze-to-gas transition from Titan's measured density curve
* Disc colour matched to Titan's measured spectrum
* Stratified upper haze with detached layers, animated with simulation time
* White methane clouds in two polar caps

### Pluto and Triton

![Triton and Neptune](https://raw.githubusercontent.com/Brimfyr/RealAtmospheres/main/content/triton-orbit.jpg)
![Pluto's haze lit from behind](https://raw.githubusercontent.com/Brimfyr/RealAtmospheres/main/content/pluto-backlit.jpg)

* N₂ atmospheres and hazes from New Horizons and Voyager measurements
* Hazes that show as a bright ring when lit from behind
* Stratified haze bands, animated with simulation time
* Nitrogen-ice clouds on Triton

### Terrain and moons

* A softer terminator on rugged ground with little or no air
* A planet keeps its full detail while a small moon passes the camera

## Settings

With [ModMenu](https://github.com/MrJeranimo/ModMenu) installed, the game's Mods menu has a Real Atmospheres page:

* **Procedural haze bands**: the animated bands in the haze of Pluto, Triton and Titan. Unticked, their haze is smooth.

Settings are saved in `RealAtmospheres\settings.toml` in the game's documents folder.

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

* Saturn map mixed by JCP-JohnCarlo, from work by HellcatF6F, MrSpace43-Celestia, FarGetaNik and Snowfall-The-Cat.
* Uranus map by Askaniy, [CC BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/). Our modified Uranus map, cloud mask and flowmap are released under the same licence.
* Neptune map by Askaniy, [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/), modified.
* Mars cirrus, Titan haze and cloud, and Triton cloud masks: *Produced in part with SpaceEngine PRO © Cosmographic Software LLC.*
* Mars dust scattering after S. Schneegans, T. Meyran, I. Ginkel, G. Zachmann and A. Gerndt, "Physically Based Real-Time Rendering of Atmospheres using Mie Theory", Computer Graphics Forum 43(2), 2024.

The code is MIT. Because of the Uranus map, the release as a whole is for non-commercial use. [CREDITS.md](https://github.com/Brimfyr/RealAtmospheres/blob/main/CREDITS.md) gives the source and terms of every shipped file.
