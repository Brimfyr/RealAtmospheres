\[HEADING=1]Real Atmospheres\[/HEADING]

Atmospheres, hazes and clouds for Kitten Space Agency's Solar System, derived from each body's composition and particle optics. A \[URL=https://github.com/StarMapLoader/StarMap]StarMap\[/URL] mod.

No game files are modified. At launch the mod builds patched copies of the game's shaders and system XML and loads those instead. If a game update changes a patched part of the XML, that patch is skipped. A game update that changes the atmosphere shaders can stop them compiling, so use a release built for your KSA version.

\[HEADING=2]Download\[/HEADING]

\[URL='https://github.com/KSAModding/content-index/blob/main/listings/RealAtmospheres.toml']Borea\[/URL]

\[URL='https://spacedock.info/mod/4565/Real%20Atmospheres']SpaceDock\[/URL]

\[URL='https://github.com/Brimfyr/RealAtmospheres/']GitHub\[/URL]

\[HEADING=2]Features\[/HEADING]

\[HEADING=3]Mars\[/HEADING]

\[SPOILER="Show images"]

\[ATTACH type="full" alt="mars-orbit.jpg"]1979\[/ATTACH]

\[ATTACH type="full" alt="mars-north-pole.jpg"]1980\[/ATTACH]

\[ATTACH type="full" alt="mars-surface-dust.jpg"]1981\[/ATTACH]

\[ATTACH type="full" alt="mars-clouds.jpg"]1982\[/ATTACH]

\[ATTACH type="full" alt="mars-sunset.jpg"]1983\[/ATTACH]

\[/SPOILER]

\[LIST]

\[\*]Rayleigh scattering from a 95% CO₂ atmosphere at 6.1 mbar

\[\*]Dust scattering from Mie theory, with a 9 km scale height

\[\*]Exact Mie phase function for Martian dust, giving the blue sun halo and blue sunsets

\[\*]Double-exponential dust density profile

\[\*]Water-ice cirrus at 22–29 km, in 2D and volumetric

\[/LIST]

\[HEADING=3]Venus\[/HEADING]

\[SPOILER="Show images"]

\[ATTACH type="full" alt="venus-orbit.jpg"]1984\[/ATTACH]

\[ATTACH type="full" alt="venus-surface.jpg"]1985\[/ATTACH]

\[/SPOILER]

\[LIST]

\[\*]Dense CO₂ Rayleigh scattering, at 53× Earth's surface density

\[\*]Blue-absorbing haze below the clouds

\[/LIST]

\[HEADING=3]Jupiter\[/HEADING]

\[SPOILER="Show images"]

\[ATTACH type="full" alt="jupiter-low.jpg"]1986\[/ATTACH]

\[/SPOILER]

\[LIST]

\[\*]H₂/He Rayleigh scattering, with a 25 km scale height

\[\*]Cloud decks at real altitudes: lower deck −1 to 12 km, main deck from 15 km, storms up to 96 km

\[/LIST]

\[HEADING=3]Saturn\[/HEADING]

\[SPOILER="Show images"]

\[ATTACH type="full" alt="saturn-low.jpg"]2020\[/ATTACH]

\[/SPOILER]

\[LIST]

\[\*]Rayleigh scattering above a thin haze, with methane colouring and a 52 km scale height

\[\*]Volumetric clouds, with cloud height following the planet's map

\[\*]Colour map at 2048 px per cube face (stock: 1024)

\[/LIST]

\[HEADING=3]Uranus \& Neptune\[/HEADING]

\[SPOILER="Show images"]

\[ATTACH type="full" alt="uranus-low.jpg"]1988\[/ATTACH]

\[ATTACH type="full" alt="neptune-low.jpg"]1989\[/ATTACH]

\[/SPOILER]

\[LIST]

\[\*]Methane absorption, with a thin Rayleigh limb and haze

\[\*]Volumetric clouds, with cloud height following the planet's map

\[\*]Colour maps at 2048 px per cube face (stock: 512)

\[/LIST]

\[HEADING=3]Titan\[/HEADING]

\[SPOILER="Show images"]

\[ATTACH type="full" alt="titan-orbit.jpg"]1990\[/ATTACH]

\[ATTACH type="full" alt="titan-terminator.jpg"]1991\[/ATTACH]

\[ATTACH type="full" alt="titan-hazes.jpg"]1992\[/ATTACH]

\[/SPOILER]

\[LIST]

\[\*]Stratified upper haze, animated with simulation time

\[\*]Detached haze layers at 230 and 290 km

\[/LIST]

\[HEADING=3]Triton \& Pluto\[/HEADING]

\[SPOILER="Show images"]

\[ATTACH type="full" alt="triton-orbit.jpg"]1993\[/ATTACH]

\[ATTACH type="full" alt="pluto-hazes.jpg"]1994\[/ATTACH]

\[/SPOILER]

\[LIST]

\[\*]N₂ Rayleigh scattering from measured surface pressure and temperature

\[\*]Forward-scattering haze, visible as a ring when looking toward the Sun

\[\*]Stratified haze bands, animated with simulation time

\[\*]Nitrogen-ice clouds on Triton at 4–6.5 km

\[/LIST]

\[HEADING=2]Installation\[/HEADING]

Requires \[URL=https://github.com/StarMapLoader/StarMap]StarMap\[/URL]. Each release is named with the KSA build it was tested on.

\[B]With Borea:\[/B] install Real Atmospheres from the mod list.

\[B]Manually:\[/B]

\[LIST=1]

\[\*]Install StarMap and run the game once through \[ICODE]StarMap.Loader.exe\[/ICODE].

\[\*]Extract the release into \[ICODE]Documents\\My Games\\Kitten Space Agency\\mods\[/ICODE], giving \[ICODE]mods\\RealAtmospheres\\RealAtmospheres.dll\[/ICODE].

\[\*]Launch the game once and close it. KSA adds new mods to \[ICODE]manifest.toml\[/ICODE] disabled: set this mod's entry to \[ICODE]enabled = true\[/ICODE].

\[\*]Launch through StarMap.

\[/LIST]

To uninstall, delete \[ICODE]mods\\RealAtmospheres\[/ICODE] and \[ICODE]mods\_RealSharedShadow\[/ICODE].

\[HEADING=2]Building\[/HEADING]

\[ICODE]deploy.ps1\[/ICODE] builds the mod and installs it into your mods folder. \[ICODE]package.ps1 -Version x.y.z -GameBuild vYYYY.M.D.NNNN\[/ICODE] builds a release archive in \[ICODE]dist\[/ICODE]. Set \[ICODE]StarMapDir\[/ICODE] to your StarMap folder.

\[HEADING=2]Credits\[/HEADING]

\[LIST]

\[\*]Saturn map mixed by JCP-JohnCarlo, from work by HellcatF6F, MrSpace43-Celestia, FarGetaNik and Snowfall-The-Cat.

\[\*]Uranus map by Askaniy, \[URL=https://creativecommons.org/licenses/by-nc-sa/3.0/]CC BY-NC-SA 3.0\[/URL]. Our modified Uranus map, cloud mask and flowmap are released under the same licence.

\[\*]Neptune map by Askaniy, \[URL=https://creativecommons.org/licenses/by/3.0/]CC BY 3.0\[/URL], modified.

\[\*]Mars cirrus, Titan haze and Triton cloud masks: \[I]Produced in part with SpaceEngine PRO © Cosmographic Software LLC.\[/I]

\[/LIST]

The code is MIT. Because of the Uranus map, the release as a whole is for non-commercial use. \[URL=https://github.com/Brimfyr/RealAtmospheres/blob/main/CREDITS.md]CREDITS.md\[/URL] gives the source and terms of every shipped file.

