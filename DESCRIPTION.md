[HEADING=1]Real Atmospheres[/HEADING]

An atmosphere realism overhaul for Kitten Space Agency, packaged as a [URL=https://github.com/StarMapLoader/StarMap]StarMap[/URL] mod. It replaces atmospheric tints with composition-derived scattering, dust optics, methane absorption and layered hazes, and adds clouds to several worlds.

No game files are modified and no admin rights are needed. At launch, the mod builds patched shadow copies of Core content in the mods folder and redirects the game's reads into them. These copies are rebuilt from the current game files every launch. If an update changes an XML anchor, that patch is skipped, leaving the stock appearance. Shader changes can break compilation, so check the release's tested KSA build.

[HEADING=2]What it changes[/HEADING]

[HEADING=3]Mars[/HEADING]

[LIST]
[*][B]Composition-derived Rayleigh scattering:[/B] faint blue scattering based on Mars's 95% CO₂ atmosphere at 6.1 mbar.
[*][B]Dust-driven sky colour:[/B] Mie coefficients for Martian dust provide the red-heavy scattering spectrum, with a 9 km well-mixed scale height.
[*][B]Blue sunsets from dust optics:[/B] an exact per-angle Mie phase function for feldspar and hematite dust, following Level 2 of Schneegans et al. (2024), replaces the Henyey–Greenstein approximation. A 361-sample lookup table and a capped, double-exponential dust profile produce the blue sun halo and control the sunset glow.
[*][B]Water-ice cirrus:[/B] polar hoods and equatorial wisps at 22–29 km, rendered in 2D and volumetrically from an 8k source map.
[/LIST]

[HEADING=3]Venus[/HEADING]

[LIST]
[*][B]Dense CO₂ Rayleigh scattering[/B] at 53 times Earth's surface density, plus a low-altitude blue-selective absorber, creates an increasingly murky yellow descent to a dark, Venera-like surface through multiple scattering and aerial perspective.
[*]The low-altitude absorber models sub-cloud murk. Venus's separate, real unknown UV absorber at the cloud tops is represented by the cloud texture's dark markings.
[/LIST]

[HEADING=3]Jupiter[/HEADING]

[LIST]
[*][B]Hydrogen/helium Rayleigh scattering[/B] at 0.31 times Earth's strength and a 25 km scale height gives a thin, Juno-style blue limb above the clouds.
[*][B]Rescaled cloud decks:[/B] the lower deck spans −25 to +15 km; the main deck starts at 15 km with storm structures 10–96 km tall. Density, raymarching and orbit transitions are adjusted to preserve opacity and detail.
[/LIST]

[HEADING=3]Saturn[/HEADING]

[LIST]
[*][B]A faint blue limb, thin haze and pale-gold methane chromophore[/B], with a physically derived 52 km scale height.
[*][B]Volumetric banded clouds:[/B] coverage and colour come from Saturn's own map. Brighter regions build taller clouds, placing pale bands about 37 km above dark ones.
[*][B]Sharper surface map:[/B] 2048 pixels per cubemap face, up from stock 1024.
[/LIST]

[HEADING=3]Uranus & Neptune[/HEADING]

[LIST]
[*][B]Methane absorption[/B] drives Uranus's pale cyan and Neptune's deeper blue. Uranus's thicker photochemical haze mutes the colour; Neptune's clearer atmosphere lets it show. Both gain a thin blue Rayleigh limb and gentle haze.
[*][B]Volumetric clouds:[/B] banding, colour and cloud height follow each planet's map. Neptune's white wisps rise roughly 26 km above dark lanes; Uranus's structure is subtler.
[*][B]Sharper surface maps:[/B] 2048 pixels per cubemap face, up from stock 512.
[/LIST]

[HEADING=3]Titan[/HEADING]

[LIST]
[*][B]Stratified upper haze:[/B] layers are built into the scattering medium, so they affect limb glow and transmittance. A per-pixel march adds broken, undulating, region-varying detail above the smooth lower tholin haze.
[*][B]Animated layers:[/B] haze slowly rotates around Titan's spin axis and reshapes over time. Animation follows simulation time, freezing when paused and speeding up with time warp.
[*][B]Detached haze:[/B] two subtle, 2 km-thick layers at 290 km and 230 km use SpaceEngine-generated maps to create irregular limb strips inspired by Cassini imagery.
[/LIST]

[HEADING=3]Triton & Pluto[/HEADING]

[LIST]
[*][B]Composition-derived nitrogen Rayleigh scattering:[/B] Triton uses Voyager's 1.5 Pa at 38 K and a 14.8 km scale height; Pluto uses New Horizons' 1.0 Pa at 40 K and a 19.2 km scale height. Both remain nearly invisible at the game's fixed exposure.
[*][B]Forward-scattering tholin haze:[/B] Pluto's particles produce roughly 1900 times more brightness forward than backward, revealing the New Horizons-style blue ring when viewed from behind the planet. Triton's smaller particles give a subtler effect, roughly 40 times brighter forward than backward.
[*][B]Stratified haze bands:[/B] irregularly spaced, broken layers drift, form and dissipate with simulation time, visible in the forward-scattered crescent.
[*][B]Triton condensate clouds:[/B] patchy nitrogen-ice wisps at 4–6.5 km.
[/LIST]

[HEADING=2]Install[/HEADING]

Requires [URL=https://github.com/StarMapLoader/StarMap]StarMap[/URL]. Release archives include the tested game build in their names: [ICODE]RealAtmospheres-v<version>-ksa<build>.zip[/ICODE].

[HEADING=3]With a mod manager[/HEADING]

In [URL=https://github.com/KSAModding]Borea[/URL], install Real Atmospheres from the mod list. Borea installs StarMap, unpacks the mod into the active instance and enables it. Managers keep mods, [ICODE]manifest.toml[/ICODE], [ICODE]settings.toml[/ICODE], saves and logs in their own instance folders rather than the default Documents location.

[HEADING=3]By hand[/HEADING]

[LIST=1]
[*]Install StarMap and run the game once through [ICODE]StarMap.Loader.exe[/ICODE].
[*]Extract the release into [ICODE]Documents\My Games\Kitten Space Agency\mods\[/ICODE], giving you [ICODE]mods\RealAtmospheres\RealAtmospheres.dll[/ICODE]. The folder must be named exactly [ICODE]RealAtmospheres[/ICODE]: KSA uses the folder name as the mod ID.
[*]Launch once so KSA discovers the mod, then close the game. New mods are added to [ICODE]Documents\My Games\Kitten Space Agency\manifest.toml[/ICODE] [B]disabled[/B]. Set its entry to:

[CODE]
[[mods]]
id = "RealAtmospheres"
enabled = true
[/CODE]

[*]Launch through StarMap. The log shows [ICODE]found mod 'RealAtmospheres'[/ICODE] when the mod is active.
[/LIST]

To uninstall, delete the [ICODE]RealAtmospheres[/ICODE] folder and the [ICODE]_RealSharedShadow[/ICODE] folder beside it. KSA removes the manifest entry automatically.

[HEADING=2]How it works[/HEADING]

Before the game's [ICODE]Main[/ICODE], the mod applies anchored text transforms to fresh copies of stock shaders and XML. Shader copies live under the mod's [ICODE]ShadowContent\Core\Shaders\[/ICODE]; astronomical and Sol system XML copies live in [ICODE]mods\_RealSharedShadow\Core\[/ICODE]. The transforms are idempotent, allowing existing hand patches to pass through unchanged.

Harmony redirects asset, system and shader reads to these copies. Shader includes follow the shadow tree, while mod textures use absolute paths into [ICODE]assets\[/ICODE]. Game access is through reflection.

[HEADING=2]Building from source[/HEADING]

[ICODE]deploy.ps1[/ICODE] builds the .NET 10 class library and installs it into your mods folder. [ICODE]package.ps1 [-Version x.y.z] [-GameBuild vYYYY.M.D.NNNN][/ICODE] builds a release zip in [ICODE]dist\[/ICODE]. Both ship only the assets in [ICODE]release-files.txt[/ICODE]. Set [ICODE]StarMapDir[/ICODE] to your StarMap folder; the project references [ICODE]StarMap.API.dll[/ICODE] and [ICODE]0Harmony.dll[/ICODE].

[HEADING=2]Credits[/HEADING]

Shipped textures are derived from the following sources, converted into cubemaps, cloud masks or flowmaps by this repository's scripts:

[LIST]
[*][B]SpaceEngine:[/B] Mars cirrus, Titan upper/lower haze and Triton cloud maps.
[*][B]JCP-JohnCarlo:[/B] Saturn's texture map, used for its diffuse map, cloud mask and flowmap.
[*][B]Askaniy:[/B] Uranus and Neptune texture maps, used for their diffuse maps, cloud masks and flowmaps.
[*][B]Kitten Space Agency:[/B] Jupiter uses the game's own textures.
[/LIST]

[ICODE]MarsDustPhaseLut.glsl[/ICODE] is computed by [ICODE]make_mars_phase_lut.py[/ICODE] using Mie scattering, rather than derived from an image.

The Mars, Titan and Triton masks come from our own SpaceEngine PRO exports: [I]Produced in part with SpaceEngine PRO © Cosmographic Software LLC[/I]. See [URL=https://github.com/Brimfyr/RealAtmospheres/blob/main/CREDITS.md]CREDITS.md[/URL] for the origin and terms of every shipped file.
