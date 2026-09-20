# Changelog

## 1.0.1

The atmospheres themselves are unchanged from 1.0.0. This release re-sources the Mars cloud map so every
texture in the archive is properly licensed, works on the giants' cloud decks, which were flat and sheared,
refreshes Uranus's and Neptune's colour maps, cleans up what Jupiter's rescale left behind, standardises
the asset names and adds mod manager support.

- Mars cirrus masks are now generated from our own SpaceEngine PRO export instead of a third-party
  re-upload we cannot redistribute. The look is preserved: mean opacity 0.0619 → 0.0613, identical median
  and p90, and 0.9998 correlation with the 1.0.0 masks (1.0000 for the volumetric one). The generator
  keeps a longitude roll and a tone-matching curve for future exports, both currently inactive because
  this one already matches.
- The giants' cloud decks gain vertical relief, and it follows the planet's own map. Every cloud on
  Saturn, Uranus and Neptune sat at one height with one density, so the decks read as flat slabs
  next to Jupiter's. They now carry a second, lower cloud type, and which type a column takes is
  read off the map's brightness, because bright cloud on a giant is fresh ice carried up above the
  deck: Neptune's white wisps ride about 26 km over its dark lanes, Saturn's bands 37 km. The
  coverage that decides how far up that type a column fills spans a wider range with a finer drift
  on top of it. Jupiter's middle types are lowered the same way, and its own artwork, which towers
  over its darker regions instead, is untouched. No type reaches above the deck, so the layer's top
  altitude, where the 2D billboard hangs, is where 1.0.0 had it.
- The giants ship their own cloud detail tile, carrying just the two ends of that blend, so nothing
  about the deck's height comes from a texture that tiles. Borrowing Jupiter's put sparse spikes in
  its type channel, and since that texture tiles across the planet, every spike raised a tower in
  the same place on every tile: a regular field of bumps.
- Uranus's clouds no longer shear apart up close. Flow displacement had been set from each
  planet's wind speed alone, which drove it to 146x the cloud noise scale on Uranus (Jupiter runs
  about 10x). At that ratio the two advection phases decorrelate and comb the deck. Since speed is
  displacement divided by loop duration, displacement is now pinned near 10x the noise on all
  three and the loop carries the real peak jets instead: Saturn and Neptune 450 m/s, Uranus 200.
  Eddy strengths are also normalised so every planet drifts them about the same distance.
- Neptune's brightest bands keep their structure. Coverage came from the map's luminance
  normalised between its 2nd and 98th percentiles, so every band brighter than the 98th clamped
  to the ceiling and sat perfectly flat, with nothing for the noise to carve. The window widens
  to the 1st and 99.5th, the range moves down off the ceiling where a column fills completely,
  and a faint drift keeps any region from being uniform.
- Volumetric clouds fade into their 2D billboards much further out. The rescale had pulled
  Jupiter's fade band in from 5000-9000 km to 700-1300, which made the swap obvious; stock's band
  is restored, and the giants now use the same fractions of their own radii (Saturn 4200-7500 km,
  Uranus and Neptune 1800-3300). Storm holes at the vortex centres are stock behaviour, present
  with the mod disabled, so nothing here chases them.
- Jupiter's lower deck sits above the planet mesh again. Stock keeps it almost entirely above the
  mesh at the 1-bar level (-4..216 km), filling everything beneath the main deck; 1.0.0's rescale
  to real altitudes shifted that deck down instead of scaling its height, burying 62% of it and
  leaving only the fading top of its density curve visible. It now spans -1..+12 km, 3 km clear of
  the main deck, so a descent still passes through distinct layers, with density raised to hold the
  optical depth across the reduced thickness.
- Jupiter's cloud noise is rescaled along with its decks. The rescale shrank them 5.7x and adjusted
  heights, densities, raymarch steps and light reach, but left NoiseScale at stock values, so the
  noise that erodes clouds into shape spanned 420 km across a 96 km deck and barely varied inside a
  10 km vortex, rendering storms as hard-edged slabs. Noise now follows the rescale (main deck
  420 -> 73 km, lower deck 120 -> 22 km), back inside stock's ratios.
- Uranus's and Neptune's colour maps are updated to better match the planets as observed. Their cloud
  masks and flowmaps are derived from those maps, so both are rebuilt with them.
- Asset names follow `<Body><Role>[2D|Volumetric].<ext>`, with pipeline inputs suffixed `Source`. Four
  shipped files changed name: `Saturn/Uranus/NeptuneDiffuse.ktx2` and `MarsDustPhaseLut.glsl`. Nothing in
  the game refers to these names.
- Mod manager support: the README covers managers and manual installs, and `deploy.ps1` / `package.ps1`
  follow the active instance rather than assuming `Documents\My Games\Kitten Space Agency`.
- `LICENSE` and `CREDITS.md` ship inside the archive, as the Creative Commons licences require.
- The build resolves StarMap from a mod manager's loader folder, falling back to a standalone launcher
  install, and a fresh clone builds without the sibling `Shared\` folder.

## 1.0.0

First release. Physically derived atmospheres for Mars, Venus, Jupiter, Saturn, Uranus, Neptune, Titan,
Triton and Pluto, applied as a StarMap plugin that shadows the game's shaders and system XML rather than
modifying any game file.

- Mars: real CO₂ Rayleigh, Mie-theory dust optics with an exact dust phase function (blue sun halo, blue
  sunsets), a double-exponential dust profile, and high-altitude water-ice cirrus.
- Venus: composition-true dense CO₂ scattering with a low absorber for the murky descent to a Venera-dark
  surface.
- Jupiter: thin H₂/He blue limb at the true scale height, cloud decks rescaled to real altitudes.
- Saturn, Uranus, Neptune: derived atmospheres (methane-driven cyan and blue for the ice giants), new
  volumetric clouds with per-planet flow, and 2048/face colour maps.
- Titan: stratified drifting upper haze and two detached haze laminae.
- Pluto and Triton: real N₂ atmospheres, forward-scattering tholin haze, animated haze bands, and Triton's
  condensate clouds.
