# Changelog

## Unreleased

### Added

- Flight atmospheres from measured profiles. Drag and engine back-pressure on Earth, Venus, Mars, Titan, Jupiter, Saturn, Uranus, Neptune, Triton and Pluto now follow real density and pressure instead of the game's single exponential, and each atmosphere's edge moves to where the real one thins out (Titan 996 km instead of 480, Venus 143 km instead of 455). Uranus and Neptune had the game's Earth values.

### Changed

- Earth's sky uses the density of the US Standard Atmosphere 1976, so its blue limb thins out at the right height.
- Jupiter's, Saturn's, Uranus's and Neptune's skies use their measured gas density.
- The giants' gas scatters at its real strength, worked out from each planet's composition at 1 bar, instead of a fraction picked by eye: Jupiter's was 0.56 of it, Saturn's 0.03, Uranus's 0.02 and Neptune's 0.03. Their limbs show the gas's blue.
- Jupiter and Saturn have their thin stratospheric hazes above the clouds, as Hubble and Cassini measured them. Faint from above, along the limb they whiten the gas's blue.
- Jupiter's main cloud deck sits at its measured height, topping out at 39 km, the top of Jupiter's haze, instead of 111 km, high in the stratosphere. Its storms and vortices are a quarter as tall, with the same optical depth.
- Uranus's and Neptune's methane sits below the tropopause, where the real methane is, with the same total absorption.
- Titan's golden haze follows Huygens' and Cassini's measurements: their thickness and shape, reaching the edge of the atmosphere near 1,000 km instead of 576 km, with a shorter gold-to-blue transition at the top of the main haze. Its haze layers move up to 290 and 380 km, where Cassini saw the detached haze around the 2009–10 equinox, and become soft bands 30–40 km thick that blend with the stratified haze, instead of 2 km sheets.
- Titan's disc takes its measured colour, gold instead of peach, and the haze above the main layer is a deeper blue instead of lavender. The main haze scatters with its measured spectrum and the upper haze like the gas.
- Titan's low clouds form two polar caps with clear tropics, where Cassini saw them, instead of the game's Venus cloud pattern. They are white like Earth's instead of Venus's brown, so Titan's own light colours them.
- Mars's air follows Pathfinder's and Phoenix's measurements, from the floor of Hellas, where the game starts Mars's sky, to the edge of the atmosphere at 134 km instead of 95 km. At the datum it is twice as dense as before, as derived from its 6.1 mbar: the game applied that strength 8.2 km lower down.
- Mars's dust layer is measured from the mean surface instead of the floor of Hellas, so it thickens into the basins and thins over the heights. Above the mean surface it is as thick as before.
- Pluto's air and haze follow New Horizons' measurements. The haze thins out with the nitrogen, as the spacecraft measured, reaching the edge of the atmosphere at 634 km instead of 218 km. It scatters light the way New Horizons saw it at every angle to the Sun: a faint blue rim with the Sun behind you, a bright ring when Pluto is lit from behind. It was 4 to 8 times too faint lit from behind, and about 50 times too faint in sunlight. Its stacked layers are 11 km apart instead of 5.5 km, about 20 below 200 km as New Horizons counted.
- Triton's air and haze follow Voyager's measurements. The nitrogen thins out as measured, reaching the edge of the atmosphere at 349 km instead of 165 km. The haze is half as tall, an 11 km layer close to the ground instead of 20 km, and paler blue. It is one even layer, as Voyager saw it, without Pluto's stacked bands. It takes its strength, colour and forward scattering from the haze particles Voyager's photometry found, and it absorbs no light.
- Venus's clouds take its measured colour, a pale cream white, instead of the game's brown, and its 2D clouds are shaded more evenly across the disc.
- Venus's haze thickens toward the cloud base, as the Venera probes measured, instead of being thickest at the ground. The clear air low down turns gradually into the clouds instead of meeting them suddenly.
- Venus ends at its cloud tops, without a blue shell around them. The gas above them thins out as fast as the Venus International Reference Atmosphere's.
- Venus's clouds form one solid deck at 48–72 km instead of separate cumulus columns at 10–45 km with holes between them. The deck is densest at its base and solid up to about 70 km, where Venus's cloud tops are measured, so it meets the 2D clouds that replace it from afar. The game's cloud patterns stay on top as low lumps, where they used to tower 13 km.

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
