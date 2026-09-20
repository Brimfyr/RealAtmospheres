# Changelog

## 1.0.1

The atmospheres are unchanged from 1.0.0. This release re-sources the Mars cloud map so every texture in
the archive is properly licensed, standardises the asset names, and adds mod manager support.

- Mars cirrus masks are now generated from our own SpaceEngine PRO export instead of a third-party
  re-upload we cannot redistribute. The look is preserved: mean opacity 0.0619 → 0.0623, identical median,
  p90 and peak opacity, 0.94 spatial correlation with the 1.0.0 masks. Two corrections make that hold —
  the export sits 89.1° east of the old map, and its background is lifted (median luminance 0.42 against
  0.07), so the generator carries a quantile mapping onto the shipped tone curve.
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
