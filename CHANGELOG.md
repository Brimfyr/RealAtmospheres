# Changelog

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
