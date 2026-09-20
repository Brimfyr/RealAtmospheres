# Changelog

## 1.0.1

The atmospheres are unchanged from 1.0.0. This release re-sources the Mars cloud map so every texture in
the archive is properly licensed, standardises the asset names, and adds mod manager support.

- Mars cirrus masks are now generated from our own SpaceEngine PRO export instead of a third-party
  re-upload we cannot redistribute. The look is preserved: mean opacity 0.0619 → 0.0613, identical median
  and p90, and 0.9998 correlation with the 1.0.0 masks (1.0000 for the volumetric one). The generator
  keeps a longitude roll and a tone-matching curve for future exports, both currently inactive because
  this one already matches.
- The giants' volumetric clouds gain vertical variety. They had a single cloud type, so every
  cloud on Saturn, Uranus and Neptune sat at one height with one density, which is why those decks
  looked uniform next to Jupiter's. There are now four types: the deck, ragged belt edges, storm
  anvils and vortex cores, each topping out lower than the last so storms read as depressions in
  the deck, the way Jupiter's do. Every type stops at or below the deck's own top, so the layer's
  top altitude, where the 2D billboard hangs, is exactly where 1.0.0 had it.
- The cloud masks now drive that choice. The shader picks a cloud type from the detail tile blended
  by the mask's red channel, and ours wrote a flat 1.0 there, pinning the whole planet to one tile
  and one type. The red channel now carries a storminess field derived from each planet's own map:
  the shear between its zonal jets, plus departures from the latitude mean that mark discrete
  features. Per-planet weighting keeps the ordering physical, so Neptune's Great Dark Spot reaches
  the vortex-core type, Saturn's belt edges reach storm anvils, and Uranus stays nearly uniform.
- The giants ship their own cloud detail tile. Borrowing Jupiter's put sparse spikes in the
  type channel, and because that texture tiles across the planet, each spike became a storm
  tower in the same spot on every tile: a regular field of bumps. Ours varies smoothly and
  never reaches the storm types on its own, so storms come from the mask, which does not tile.
- Jupiter's cloud noise is rescaled along with its decks. The rescale to real altitudes shrank
  them 5.7x and adjusted heights, densities, raymarch steps and light reach, but left NoiseScale
  at stock values, so the noise that erodes clouds into shape spanned 420 km across a 96 km deck
  and barely varied inside a 10 km vortex. Storms rendered as hard-edged slabs. Noise now follows
  the rescale (main deck 420 -> 73 km, lower deck 120 -> 22 km), back inside stock's ratios.
- Jupiter's vortex cores no longer read as black pits. Rescaling its decks to real altitudes
  closed the gap stock leaves between them (216 km against a 220 km base), so the lower deck's
  flat top sat exactly where the vortices bottom out, shadowed from above and lit by an
  atmosphere far thinner than the one those decks were authored for. The lower deck now sits at
  -32..+8 km, 7 km clear of the main deck, so descending passes through distinct layers as it
  does in stock. Light also reaches 35 km into the decks rather than 17.5, and the storm types
  scatter more brightly, which keeps the sunken vortices rather than flattening them.
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
- All bodies: smooth day/night fade on 2D cloud layers.
