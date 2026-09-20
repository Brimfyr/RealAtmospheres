# Changelog

## 1.0.1

The atmospheres themselves are unchanged. This release re-sources the Mars cloud map so every texture in
the archive is properly licensed, works on the giants' cloud decks, refreshes Uranus's and Neptune's
colour maps, cleans up what Jupiter's rescale left behind, and adds mod manager support.

- Mars cirrus is generated from our own SpaceEngine PRO export instead of a third-party re-upload we
  cannot redistribute. The look is preserved: mean opacity 0.0619 → 0.0613, identical median and p90,
  0.9998 correlation with the 1.0.0 masks.
- The giants' cloud decks have vertical relief, read off the planet's own map: bright cloud is fresh ice
  carried up, so Neptune's white wisps ride ~26 km above its dark lanes and Saturn's bands ~37 km. A
  second, lower cloud type and a wider coverage range carry it. No type reaches above the deck, so the
  layer top, where the 2D billboard hangs, is where 1.0.0 had it. Jupiter's own artwork, which towers over
  its darker regions instead, is untouched.
- The giants ship their own cloud detail tile, holding just the two ends of that blend. Borrowing
  Jupiter's put spikes in its type channel, and since the tile repeats across the planet, so did the
  towers it raised.
- Uranus's clouds no longer shear apart up close. Flow displacement ran at 146x the cloud noise scale
  there (Jupiter runs ~10x), which combs the deck. It is pinned near that ratio on all three now, with
  loop duration carrying each planet's real peak jets instead: Saturn and Neptune 450 m/s, Uranus 200.
- Neptune's brightest bands keep their structure. Coverage normalised between the map's 2nd and 98th
  percentiles clamped everything brighter flat; the window widens to the 1st and 99.5th and drops off the
  ceiling where a column fills completely.
- Volumetric clouds fade into their 2D billboards much further out: stock's 5000-9000 km band is restored
  on Jupiter, with the same fractions of radius for the giants. The holes at Jupiter's vortex centres are
  stock behaviour, present with the mod disabled, so nothing here chases them.
- Jupiter's lower deck sits above the planet mesh again. The rescale to real altitudes shifted it down
  rather than scaling its height, burying 62% of it; it now spans -1..+12 km, 3 km clear of the main deck,
  at a density that holds the optical depth.
- Jupiter's cloud noise follows that rescale too (main deck 420 -> 73 km, lower deck 120 -> 22 km). At
  stock's 420 km it spanned the whole 96 km deck, rendering storms as hard-edged slabs.
- Uranus's and Neptune's colour maps better match the planets as observed. Their cloud masks and flowmaps
  are derived from those maps, so both are rebuilt.
- Asset names follow `<Body><Role>[2D|Volumetric].<ext>`, with pipeline inputs suffixed `Source`. Four
  shipped files changed name; nothing in the game refers to these names.
- Mod manager support: the README covers managers and manual installs, `deploy.ps1` / `package.ps1` follow
  the active instance, and the build resolves StarMap from a manager's loader folder.
- `LICENSE` and `CREDITS.md` ship inside the archive, as the Creative Commons licences require.

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
