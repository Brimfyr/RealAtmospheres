# Real Atmospheres

The atmosphere realism overhaul for Kitten Space Agency, packaged as a
[StarMap](https://github.com/StarMapLoader/StarMap) mod. No game files are
modified and no admin rights are needed: at launch the mod builds patched
shadow copies of the relevant Core content in the mods folder and uses Harmony
to redirect the game's file reads into them. The shadow is rebuilt from the
current game files at every launch, and if an update changes an XML anchor the
affected patch is skipped gracefully (stock look until the mod is updated).
Shader patches are more fragile: a game update that reworks the atmosphere
shaders can stop them compiling, so check the release's tested game build.

## What it changes

### Mars

- **Rayleigh scattering** derived from real composition (95% CO₂ at 6.1 mbar):
  faint, blue, λ⁻⁴.
- **Dust as the colour engine**: bhmie-derived Mie coefficients (red-heavy
  scattering spectrum), 9 km well-mixed scale height — the sky colour now comes
  from real dust optics, not a painted tint.
- **Mie-theory dust phase function** (Level 2 of Schneegans et al. 2024): the
  *exact* per-angle Mie phase function from a bhmie solution for
  feldspar+hematite Martian dust, baked as a 361-sample lookup table in
  `AtmosphereFunctions.glsl` (no Henyey–Greenstein approximation) and selected by
  a sentinel asymmetry value. Produces the physically-exact blue sun halo.
- **Double-exponential dust density**: a compact, capped dust layer (Schneegans)
  across all the atmosphere shaders, so the sunset forward-glow is controlled by
  the real dust vertical profile rather than an artificial limiter — the
  Curiosity-style blue sunset, tamed but brilliant.
- **MarsCirrus layer**: high-altitude (22–29 km) water-ice clouds — polar hoods
  and equatorial wisps, 2D + volumetric, from an 8k source map.

### Venus

- **Composition-true dense CO₂ Rayleigh** (×53 Earth surface density), plus a
  blue-selective absorber low in the atmosphere that drives a progressively
  murkier yellow descent to a Venera-matching dark surface — the "fog" emerges
  from real multiple scattering and aerial perspective, no fake fog term. (The
  famous dark UV markings come from Venus's *real* "unknown UV absorber", which
  actually sits high at the cloud tops — those are carried by the cloud texture,
  not this low-altitude atmospheric term, which models the sub-cloud murk.)

### Jupiter

- **Composition-derived H₂/He Rayleigh** (×0.31 Earth) at the true 25 km scale
  height — a razor-thin blue limb over the cloud tops, Juno-style.
- **Cloud decks rescaled to reality**: lower deck −25 to +15 km, main deck from
  15 km with storm structures 10–96 km tall, densities and raymarching rescaled
  to preserve opacity and detail. Orbit-transition altitudes rescaled to match.

### Saturn

- **Above-haze Rayleigh**: a faint λ⁻⁴ blue limb (the deep atmosphere is hidden
  beneath the high haze), a thin gentle haze layer, and a pale-gold methane
  chromophore. Physically-derived 52 km scale height (Saturn's low gravity
  makes it puffy).
- **Volumetric clouds** (Jupiter-style, stock Saturn had none): 3D banded cloud
  structure with towers, coverage taken from the planet's own banding and colour
  sampled from its diffuse map, so the clouds match the surface.
- **Higher-resolution surface map** — 2048/face, up from stock 1024.

### Uranus & Neptune

- **Methane red-absorber** (in the ozone slot) is the colour engine: moderate
  for Uranus's pale cyan — its thick photochemical haze mutes it — and stronger
  for Neptune's deep blue, whose clearer atmosphere lets the colour show. A thin
  above-haze λ⁻⁴ Rayleigh limb and a gentle haze sit on top.
- **Volumetric clouds** (Jupiter-style, neither had any in stock): 3D cloud
  structure with towers, coverage from the planet's banding and colour sampled
  from its diffuse map. Subtler on featureless Uranus, more defined on Neptune.
- **Higher-resolution surface maps** — 2048/face, up from stock 512.

### Titan

- **Stratified haze bands**: the layered upper hazes are modulated into the
  actual scattering medium (Rayleigh density warped into denser/thinner shells),
  not painted on — so limb glow, phase and transmittance are all physically
  correct. A per-pixel limb march gives them organic, broken, undulating,
  region-varying structure — the horizontal detail the symmetric atmosphere LUT
  can't hold — and they slowly rotate around Titan's spin axis and reshape over
  time (sim-linked: pausing freezes them, time-warp speeds them up). Confined to
  high altitude above the tholin deck, over a smooth thick lower haze, matching
  Cassini limb imagery closely enough to be hard to tell from a photo.
- **Detached haze laminae**: two barely-visible stratified layers (290 km and
  230 km, each 2 km thin — Cassini resolves individual haze layers at km
  scale), from organic SpaceEngine-generated cloud maps. The subtle broken
  limb strips that make the real Titan look organic rather than a clean
  airbrushed ball.

### Triton & Pluto

- **Derived N₂ Rayleigh** (Triton: Voyager 1.5 Pa @ 38 K, H = 14.8 km; Pluto:
  New Horizons 1.0 Pa @ 40 K, H = 19.2 km). Physically honest — and honestly
  near-invisible at the game's fixed exposure, like the real thing.
- **Forward-scattering tholin haze** (the visible component of both
  atmospheres): Pluto's fractal aggregates at g = 0.85 give ~1900× more
  brightness forward than back — invisible at full phase, then the iconic
  New Horizons blue ring ignites as you swing behind the planet. Triton's
  smaller particles (g = 0.55, ~40×) do the same more subtly, matching
  Voyager's forward-geometry haze detections.
- **Stratified haze bands**: the New Horizons gravity-wave layers, modulated
  into the haze density and rendered by a per-pixel march so the forward-
  scattered crescent shows stacked, broken, drifting layers — hash-salted for
  irregular spacing, dropped bands and varied widths, and animated (slow
  rotation + cloud-like formation/dissipation, sim-linked). Triton gets the same
  treatment on its thinner forward-scattering haze.
- **Triton's condensate clouds**: patchy N₂-ice wisps at 4–6.5 km.

### All bodies

- **2D cloud terminator fade**: smooth day/night rolloff for unshaded 2D cloud
  layers instead of the knife-edge transmittance-LUT cutoff.

## Install

Requires [StarMap](https://github.com/StarMapLoader/StarMap). Each release zip is
named with the KSA build it was tested on (`RealAtmospheres-v<version>-ksa<build>.zip`).

### With a mod manager (easiest)

In [Borea](https://github.com/KSAModding), install Real Atmospheres from the mod
list. It installs the StarMap loader for you, unpacks the mod into the active
instance and enables it, so there is nothing to place by hand. Note that a manager
keeps the game's user data (mods, `manifest.toml`, `settings.toml`, saves, logs) in
its own instance folder rather than `Documents\My Games\Kitten Space Agency`.

### By hand

1. Install StarMap and run the game once via `StarMap.Loader.exe`.
2. Extract the zip into `Documents\My Games\Kitten Space Agency\mods\`, so you
   end up with `mods\RealAtmospheres\RealAtmospheres.dll`. The folder must be
   named exactly `RealAtmospheres` (KSA uses the folder name as the mod id).
3. **Enable it.** On the next launch KSA finds the folder and adds it to
   `Documents\My Games\Kitten Space Agency\manifest.toml`, but always
   **disabled**. Close the game and change its entry to:

   ```toml
   [[mods]]
   id = "RealAtmospheres"
   enabled = true
   ```

4. Launch through StarMap. The log lists `found mod 'RealAtmospheres'` when it
   is active. To uninstall, delete the `RealAtmospheres` folder and the
   `_RealSharedShadow` folder the mod creates next to it; KSA removes the
   manifest entry itself.

## Building from source

`deploy.ps1` builds the .NET 10 class library and installs it into your mods
folder; `package.ps1 [-Version x.y.z] [-GameBuild vYYYY.M.D.NNNN]` builds the
player zip in `dist\`. Both ship exactly the assets listed in `release-files.txt`
(everything else in `assets\` is a converter source or stale output). The csproj only references `StarMap.API.dll` and `0Harmony.dll`
(set `StarMapDir` to your StarMap folder); all game access is via reflection.

## How it works

- `[StarMapBeforeMain]` runs before the game's `Main` and applies anchored text
  transforms (idempotent — a hand-patched install passes through unchanged) to
  fresh copies of the stock files, rebuilt at every launch:
  `Content\Core\Shaders\**` into `ShadowContent\Core\Shaders\` in the mod folder,
  and `Astronomicals.xml`, `SolSystem.xml` and `SolSystemDense.xml` into
  `mods\_RealSharedShadow\Core\` (the leading underscore makes KSA's mod loader
  skip that folder).
- Harmony prefixes on `KSA.Mod.LoadAssetBundles`/`LoadEditorTagDefinitions`
  swap the Core mod's `Astronomicals.xml` assets entry for the shadow copy's
  absolute path, and prefixes on `LoadSystems`/`PrepareSystems` do the same for
  the Sol system templates. (The generic `XmlLoader.Load<T>` is deliberately left alone:
  patching a shared generic instantiation hijacks every other `T`.) Cirrus
  textures are referenced by absolute path into the mod's `assets\` folder
  (`Mod.GetPath` is `Path.Combine`, which passes rooted paths through).
- A Harmony prefix on `RenderCore.ShaderModuleUtils.FromFile` remaps any
  `Content\Core\Shaders\*` path into the shadow tree. shaderc `#include`s
  resolve relative to the requesting file, so the whole tree follows.

## Credits

The shipped textures are derived from these sources: resized into cubemaps, or turned into cloud masks and
flowmaps by the scripts in this repo.

| Source | By | Shipped files derived from it |
|---|---|---|
| Mars cloud map | HMSMaidNelson | `MarsCirrusMaskVolumetric.dds`, `MarsCirrusMask2D.png` |
| Saturn texture map | JCP-JohnCarlo | `SaturnDiffuse.ktx2`, `SaturnCloudsMask.dds`, `SaturnFlowmap.dds` |
| Uranus texture map | Askaniy | `UranusDiffuse.ktx2`, `UranusCloudsMask.dds`, `UranusFlowmap.dds` |
| Neptune texture map | Askaniy | `NeptuneDiffuse.ktx2`, `NeptuneCloudsMask.dds`, `NeptuneFlowmap.dds` |
| Titan haze maps | SpaceEngine | `TitanUpperHazeMask*`, `TitanLowerHazeMask*` |
| Triton cloud map | SpaceEngine | `TritonCloudsMask*` |

`MarsDustPhaseLut.glsl` is computed by `make_mars_phase_lut.py` (Mie scattering), not derived from any
image. Jupiter uses the game's own textures.
