# Changelog

## 1.1.0 - 2026-10-09

### Added

- Flight atmospheres from measured profiles: drag and engine back-pressure follow real density and pressure on Earth, Venus, Mars, Titan, Jupiter, Saturn, Uranus, Neptune, Triton and Pluto, and each atmosphere ends at its real height.
- Stratospheric hazes on Jupiter and Saturn.
- A Real Atmospheres page in the game's Mods menu, with [ModMenu](https://github.com/MrJeranimo/ModMenu): a switch for the procedural haze bands.
- Support for the Solar System + Interstellar system.

### Changed

- Requires KSA 2026.10.7 or later.
- Volumetric clouds stay in view from much higher up before the 2D layer takes over.
- Extended cloud self-shadowing.
- Earth: gas density and ozone layer from the US Standard Atmosphere 1976, and its cirrus no longer vanishes as the volumetric clouds take over.
- Mars: air density from Pathfinder and Phoenix, dust that thickens into the basins, and dust clouds that match the 2D dust layer.
- Venus: a single solid cloud deck at 48–72 km in its measured pale cream, opaque from orbit, with haze that thickens toward the cloud base and no blue shell above the clouds.
- The giants: measured gas density, scattering at its real strength, and volumetric clouds that keep their brightness from close up to orbit.
- Jupiter: bright zones stand above the dark belts, and the cloud deck rises and falls evenly.
- Saturn: clouds and haze sit higher, which removes the blue veil toward its limb.
- Saturn, Uranus and Neptune: denser, sharper-edged volumetric clouds.
- Uranus and Neptune: methane sits below the tropopause.
- Titan: haze from Huygens and Cassini measurements in its measured colour, a haze-to-gas transition from its measured density curve, detached layers at 290 and 380 km, and white methane clouds in two polar caps.
- Pluto: air and haze from New Horizons measurements.
- Triton: air and haze from Voyager measurements, and 2D clouds as bright and as thin as its volumetric ones.
- Haze bands on Pluto, Titan and Triton are stacked layers with soft, uneven tops, and cost less to draw.

### Fixed

- Haze bands turn about each body's own pole, carried by its winds.
- The giants' cloud maps flow with their winds again.
- Venus's clouds now circle the planet in about four days.
- Mars and other planets flickered from orbit under time warp whenever a small moon passed.
- Mars's 2D cirrus went dark well before the terminator.
- Triton's clouds darkened the ground when seen from orbit.
- The terminator on rugged ground with little or no air, most of all on Pluto, cut from lit to black along one line.

## 1.0.4 - 2026-09-26

### Fixed

- The game failed to start on Windows set to a number format with a decimal comma, such as German, French or Spanish.

## 1.0.3 - 2026-09-24

### Fixed

- Haze bands on Pluto, Titan and Triton missing on KSA v2026.9.22.5482.
- Saturn's ring shadows hidden by its clouds.
- Animated haze on Pluto, Titan and Triton follows simulation time: it freezes when paused and speeds up under time warp. The 1.0.2 fix for this did not take effect, and Titan's haze did not move at all.

## 1.0.2 - 2026-09-23

### Fixed

- Animated haze on Pluto, Titan and Triton follows simulation time again: it freezes when paused and speeds up under time warp.

## 1.0.1 - 2026-09-20

### Added

- Cloud height on Saturn, Uranus and Neptune follows each planet's map.
- Mod manager support.
- `LICENSE` and `CREDITS.md` in the release archive.

### Changed

- Mars cirrus masks rebuilt from our own SpaceEngine PRO export, for licensing. Appearance unchanged.
- Uranus and Neptune colour maps updated, and their cloud masks and flowmaps rebuilt from them.
- Volumetric clouds switch to 2D much further out.
- Four asset files renamed. When upgrading from 1.0.0, delete the old `assets` folder first.

### Fixed

- Uranus's clouds shearing apart up close.
- Neptune's brightest bands losing detail.
- Jupiter's lower cloud deck sitting partly below the surface.
- Jupiter's storms rendering as hard-edged slabs.

## 1.0.0 - 2026-09-15

### Added

- Atmospheres for Mars, Venus, Jupiter, Saturn, Uranus, Neptune, Titan, Pluto and Triton, derived from composition and particle optics.
- Mars dust optics and water-ice cirrus.
- Jupiter cloud decks at real altitudes.
- Volumetric clouds and 2048 px colour maps for Saturn, Uranus and Neptune.
- Stratified and detached haze on Titan.
- Forward-scattering haze and haze bands on Pluto and Triton, and nitrogen-ice clouds on Triton.
