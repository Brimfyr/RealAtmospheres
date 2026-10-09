\[HEADING=1]Real Atmospheres\[/HEADING]

Atmospheres, hazes and clouds for Kitten Space Agency's Solar System, derived from each body's composition and particle optics, with flight atmospheres from measured profiles. A \[URL=https://github.com/StarMapLoader/StarMap]StarMap\[/URL] mod.

No game files are modified: the mod loads patched copies of the game's shaders and system data at launch. Use the release built for your KSA version.

\[HEADING=2]Download\[/HEADING]

\[URL='https://github.com/KSAModding/content-index/blob/main/listings/RealAtmospheres.toml']Borea\[/URL]

\[URL='https://spacedock.info/mod/4565/Real%20Atmospheres']SpaceDock\[/URL]

\[URL='https://github.com/Brimfyr/RealAtmospheres/']GitHub\[/URL]

\[HEADING=2]Features\[/HEADING]

\[HEADING=3]Clouds\[/HEADING]

\[LIST]

\[\*]Volumetric clouds stay in view from much higher up before the 2D layer takes over

\[\*]Extended cloud self-shadowing

\[/LIST]

\[HEADING=3]Flight atmospheres\[/HEADING]

\[LIST]

\[\*]Drag and engine back-pressure follow measured density and pressure profiles on Earth, Venus, Mars, Titan, Jupiter, Saturn, Uranus, Neptune, Triton and Pluto

\[\*]Each atmosphere ends at its real height

\[/LIST]

\[HEADING=3]Earth\[/HEADING]

\[SPOILER="Show images"]

\[ATTACH type="full"]2102\[/ATTACH]

\[/SPOILER]

\[LIST]

\[\*]Gas density from the US Standard Atmosphere 1976

\[\*]Ozone layer shaped after the same standard

\[/LIST]

\[HEADING=3]Mars\[/HEADING]

\[SPOILER="Show images"]

\[ATTACH type="full"]2104\[/ATTACH]

\[ATTACH type="full"]2106\[/ATTACH]

\[ATTACH type="full"]2105\[/ATTACH]

\[/SPOILER]

\[LIST]

\[\*]CO₂ Rayleigh scattering, with density from Pathfinder and Phoenix

\[\*]Dust scattering from Mie theory, giving the blue sun halo and blue sunsets

\[\*]Dust that thickens into the basins

\[\*]Water-ice cirrus at 22–29 km

\[/LIST]

\[HEADING=3]Venus\[/HEADING]

\[SPOILER="Show images"]

\[ATTACH type="full"]2115\[/ATTACH]

\[ATTACH type="full"]2116\[/ATTACH]

\[/SPOILER]

\[LIST]

\[\*]Dense CO₂ atmosphere, with density from the Venus International Reference Atmosphere

\[\*]A single solid cloud deck at 48–72 km, in Venus's measured pale cream

\[\*]Opaque clouds from orbit, with subtle darker markings

\[\*]Clouds that circle the planet in about four days

\[\*]Blue-absorbing haze that thickens toward the cloud base

\[/LIST]

\[HEADING=3]The giants\[/HEADING]

\[LIST]

\[\*]H₂/He Rayleigh scattering at its real strength, from each planet's composition and measured gas density

\[\*]Volumetric cloud decks that keep their brightness from close up to orbit

\[\*]Cloud maps that flow with each planet's winds

\[/LIST]

\[HEADING=3]Jupiter\[/HEADING]

\[SPOILER="Show images"]

\[ATTACH type="full"]2103\[/ATTACH]

\[/SPOILER]

\[LIST]

\[\*]Bright zones high and dark belts low in the volumetric clouds, with deep vortices

\[\*]A thin stratospheric haze

\[/LIST]

\[HEADING=3]Saturn\[/HEADING]

\[SPOILER="Show images"]

\[ATTACH type="full"]2109\[/ATTACH]

\[/SPOILER]

\[LIST]

\[\*]Volumetric clouds, with cloud height following the planet's map

\[\*]A thin stratospheric haze

\[\*]Methane colouring

\[\*]2048 px colour map

\[/LIST]

\[HEADING=3]Uranus and Neptune\[/HEADING]

\[SPOILER="Show images"]

\[ATTACH type="full"]2114\[/ATTACH]

\[ATTACH type="full"]2107\[/ATTACH]

\[/SPOILER]

\[LIST]

\[\*]Volumetric clouds, with cloud height following each planet's map

\[\*]Methane absorption and a thin haze

\[\*]2048 px colour maps

\[/LIST]

\[HEADING=3]Titan\[/HEADING]

\[SPOILER="Show images"]

\[ATTACH type="full"]2111\[/ATTACH]

\[ATTACH type="full"]2112\[/ATTACH]

\[ATTACH type="full"]2110\[/ATTACH]

\[/SPOILER]

\[LIST]

\[\*]Golden haze from Huygens and Cassini measurements

\[\*]Haze-to-gas transition from Titan's measured density curve

\[\*]Disc colour matched to Titan's measured spectrum

\[\*]Stratified upper haze with detached layers, animated with simulation time

\[\*]White methane clouds in two polar caps

\[/LIST]

\[HEADING=3]Pluto and Triton\[/HEADING]

\[SPOILER="Show images"]

\[ATTACH type="full"]2108\[/ATTACH]

\[ATTACH type="full"]2113\[/ATTACH]

\[/SPOILER]

\[LIST]

\[\*]N₂ atmospheres and hazes from New Horizons and Voyager measurements

\[\*]Hazes that show as a bright ring when lit from behind

\[\*]Stratified haze bands, animated with simulation time

\[\*]Nitrogen-ice clouds on Triton

\[/LIST]

\[HEADING=3]Terrain and moons\[/HEADING]

\[LIST]

\[\*]A softer terminator on rugged ground with little or no air

\[\*]A planet keeps its full detail while a small moon passes the camera

\[/LIST]

\[HEADING=2]Settings\[/HEADING]

With \[URL=https://github.com/MrJeranimo/ModMenu]ModMenu\[/URL] installed, the game's Mods menu has a Real Atmospheres page:

\[LIST]

\[\*]\[B]Procedural haze bands\[/B]: the animated bands in the haze of Pluto, Triton and Titan. Unticked, their haze is smooth.

\[/LIST]

Settings are saved in \[ICODE]RealAtmospheres\\settings.toml\[/ICODE] in the game's documents folder.

\[HEADING=2]Installation\[/HEADING]

Requires \[URL=https://github.com/StarMapLoader/StarMap]StarMap\[/URL]. Each release is named with the KSA build it was tested on.

\[B]With Borea:\[/B] install Real Atmospheres from the mod list.

\[B]Manually:\[/B]

\[LIST=1]

\[\*]Install StarMap and run the game once through \[ICODE]StarMap.Loader.exe\[/ICODE].

\[\*]Extract the release into \[ICODE]Documents\\My Games\\Kitten Space Agency\\mods\\\[/ICODE], giving \[ICODE]mods\\RealAtmospheres\\RealAtmospheres.dll\[/ICODE].

\[\*]Launch the game once and close it. KSA adds new mods to \[ICODE]manifest.toml\[/ICODE] disabled: set this mod's entry to \[ICODE]enabled = true\[/ICODE].

\[\*]Launch through StarMap.

\[/LIST]

To uninstall, delete \[ICODE]mods\\RealAtmospheres\[/ICODE] and \[ICODE]mods\\\_RealSharedShadow\[/ICODE].

\[HEADING=2]Building\[/HEADING]

\[ICODE]deploy.ps1\[/ICODE] builds the mod and installs it into your mods folder. \[ICODE]package.ps1 -Version x.y.z -GameBuild vYYYY.M.D.NNNN\[/ICODE] builds a release archive in \[ICODE]dist\\\[/ICODE]. Set \[ICODE]StarMapDir\[/ICODE] to your StarMap folder.

\[HEADING=2]Credits\[/HEADING]

\[LIST]

\[\*]Saturn map mixed by JCP-JohnCarlo, from work by HellcatF6F, MrSpace43-Celestia, FarGetaNik and Snowfall-The-Cat.

\[\*]Uranus map by Askaniy, \[URL=https://creativecommons.org/licenses/by-nc-sa/3.0/]CC BY-NC-SA 3.0\[/URL]. Our modified Uranus map, cloud mask and flowmap are released under the same licence.

\[\*]Neptune map by Askaniy, \[URL=https://creativecommons.org/licenses/by/3.0/]CC BY 3.0\[/URL], modified.

\[\*]Mars cirrus, Titan haze and cloud, and Triton cloud masks: \[I]Produced in part with SpaceEngine PRO © Cosmographic Software LLC.\[/I]

\[\*]Mars dust scattering after S. Schneegans, T. Meyran, I. Ginkel, G. Zachmann and A. Gerndt, "Physically Based Real-Time Rendering of Atmospheres using Mie Theory", Computer Graphics Forum 43(2), 2024.

\[/LIST]

The code is MIT. Because of the Uranus map, the release as a whole is for non-commercial use. \[URL=https://github.com/Brimfyr/RealAtmospheres/blob/main/CREDITS.md]CREDITS.md\[/URL] gives the source and terms of every shipped file.

