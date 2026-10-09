# Changelog

## Unreleased

### Added

- A Real Atmospheres page in the game's Mods menu, with [ModMenu](https://github.com/MrJeranimo/ModMenu) installed. It has one switch so far: the procedural haze bands of Pluto, Triton and Titan. Off, their haze is smooth, and their sky is cheaper to draw.
- Flight atmospheres from measured profiles. Drag and engine back-pressure on Earth, Venus, Mars, Titan, Jupiter, Saturn, Uranus, Neptune, Triton and Pluto now follow real density and pressure instead of the game's single exponential, and each atmosphere's edge moves to where the real one thins out (Titan 996 km instead of 480, Venus 143 km instead of 455). Uranus and Neptune had the game's Earth values.

- The game's Solar System + Interstellar system (KSA 2026.10) gets the same Titan, Pluto, Triton and Neptune as the Solar System.

### Changed

- Venus's clouds seen from orbit are opaque, and their markings are the cloud's own colour gone a little darker and yellower. The cloud map was see-through by a third on average and by up to two thirds in its markings, so the gas below the clouds showed through it: a blue tint in the darker patches and toward the limb. Nothing below Venus's clouds is seen from space, so the map now shades the cloud instead, by as much as the game's own Venus showed there.
- The haze bands of Pluto, Triton and Titan cost less to draw, most of all at Pluto. Their sky was worked out in 50 steps for every pixel of it, bands and all, however faint the haze there. It is now worked out only where the haze is bright enough for a band to show, and each step's bands only where that step adds to the pixel. In ordinary views of Pluto that is a fifth to a third of the work; with the Sun behind Pluto, when the whole haze is lit, about half. The picture is the same to within a display level.
- Volumetric clouds are drawn from higher up before the 2D layer takes over: Earth from 3,000 km (stock 1,500 km), Venus 1,800 km (250), Mars 2,000 km (900), Titan 2,200 km, Triton 800 km, Jupiter 14,000 km (9,000), Saturn 12,000 km, and Uranus and Neptune 10,000 km. Venus's and Titan's clouds take longer steps from afar, and fewer light samples, so drawing them from that high costs no more than the giants'.
- Earth's cirrus stays in the volumetric clouds as they take over from the 2D layer. It was marched in steps up to 4.5 km long through a sheet 1.2 km thick, which jumped over it most of the time, and it was a third as dense as the 2D cirrus shows it. Earth's faintest clouds, which the game's volumetrics dropped altogether, are kept too, as opaque as the 2D layer shows them.
- Clouds shade themselves along the whole path sunlight takes through their layer, in up to 10 steps, instead of over its first kilometre or so (Earth's: 1.2 km in 4 steps). Their bases and the sides away from the Sun darken, and they shadow each other when the Sun is low.
- Earth's sky uses the density of the US Standard Atmosphere 1976, so its blue limb thins out at the right height.
- Earth's ozone takes the same standard's shape, centred at 22 km and spread from 4 to 41 km instead of 10 to 30 km, with the game's amount of it. Its absorption is weighted over each colour's share of its broad band rather than read at one wavelength per colour, which let red through four times too easily and turned light that had crossed it magenta. Twilight and the terminator turn blue, as ozone makes them.
- Jupiter's, Saturn's, Uranus's and Neptune's skies use their measured gas density.
- The giants' gas scatters at its real strength, worked out from each planet's composition at 1 bar, instead of a fraction picked by eye: Jupiter's was 0.56 of it, Saturn's 0.03, Uranus's 0.02 and Neptune's 0.03. Their limbs show the gas's blue.
- Jupiter and Saturn have their thin stratospheric hazes above the clouds, as Hubble and Cassini measured them. Faint from above, along the limb they whiten the gas's blue.
- The giants' clouds seen from afar reflect the sky's light by their map's colour, as the game's own volumetric clouds do, instead of evenly over the whole map. Evenly, it laid a pale veil over the map's detail once the giants' gas was at its real strength.
- Saturn's clouds sit 72.5 km higher, from 77.5 to 172.5 km, so that only as much of its gas shows above them as above Neptune's and Uranus's decks. From afar its disc no longer takes a blue veil toward the edge.
- Saturn's stratospheric haze is moved up with its clouds, at its measured density, and compressed above their tops so that haze and gas there keep their measured proportion. It lies on the cloud tops as a sheet about 15 km deep and blends them into the sky, where the raised deck had ended in a hard edge.
- Uranus's and Neptune's methane sits below the tropopause, where the real methane is, with the same total absorption.
- Titan's golden haze follows Huygens' and Cassini's measurements: their thickness and shape, reaching the edge of the atmosphere near 1,000 km instead of 576 km, with a shorter gold-to-blue transition at the top of the main haze. Its haze layers move up to 290 and 380 km, where Cassini saw the detached haze around the 2009–10 equinox, and become soft bands 30–40 km thick that blend with the stratified haze, instead of 2 km sheets.
- Titan's disc takes its measured colour, gold instead of peach, and the haze above the main layer is a deeper blue instead of lavender. The main haze scatters with its measured spectrum and the upper haze like the gas.
- Titan's low clouds form two polar caps with clear tropics, where Cassini saw them, instead of the game's Venus cloud pattern. They are white like Earth's instead of Venus's brown, so Titan's own light colours them.
- Mars's air follows Pathfinder's and Phoenix's measurements, from the floor of Hellas, where the game starts Mars's sky, to the edge of the atmosphere at 134 km instead of 95 km. At the datum it is twice as dense as before, as derived from its 6.1 mbar: the game applied that strength 8.2 km lower down.
- Jupiter's bright zones stand above its dark belts in the volumetric clouds, as they do on Jupiter. The game's cloud mask built its tallest clouds over the dark belts and left the bright zones low and thin. The mod uses that mask with its grey and white swapped, so the zones' deck now tops the darkest belts by about 25 km, where it was the other way round, with all of the mask's relief kept. The vortices are as deep and dark as before.
- Jupiter's cloud deck rises and falls evenly between its low and its tall clouds. The game cross-faded the two cloud types, which left full-height spikes of the tall one standing on the low deck along every edge between them.
- The giants' volumetric clouds keep their brightness as the camera backs away. The game lights each step of its cloud march at the step's far end, and from afar a step is tens of kilometres long: in a dense deck that is deep inside the cloud, where little light arrives. Jupiter's dense clouds came out at a seventh of their close-up brightness from 10,000 km, and Saturn's, Uranus's and Neptune's decks at a third to a half. Such a step is now lit at the cloud's face, under the cloud's own shadow there, so the decks also dim toward the terminator as their 2D maps do.
- Venus's volumetric clouds are lit the same way. Their brightness and their grain no longer depend on how long a step the cloud march takes, so the deck looks the same from close up as from 1,800 km, where the 2D layer takes over. Its towering cloud types are set to the brightness of its stratus, which matches the 2D layer; the game had them brighter to make up for its own lighting.
- Triton's 2D clouds can be as bright as its ice. The game cannot draw a 2D cloud brighter than a surface reflecting 38% of the sunlight, whatever its colour setting, and Triton's ice reflects about 70%, so its clouds could only darken the ground under them. Their colour setting now reaches the brightness of a white surface.
- Triton's clouds no longer darken the ground when seen from above 800 km. From up there the game shadows the ground by the cloud map alone, as if every cloud were opaque, and Triton's wisps let 99% of the light through: the shadows appeared as the 2D layer took over and vanished on the way down. They are now as faint from above as from below.
- Pluto's and Titan's haze bands are a ladder instead of a barcode. Each band was a strip of extra haze laid on the smooth falloff, so every band outshone the gap beneath it and the stack read as evenly spaced bars. Now the falloff itself is cut into bands: each is densest just under a sharp top and thins downward to the next, its amount of haze unchanged, so each band is fainter than the one below and none outshines what lies under it. The undulation, the bands' broken lengths and their slow change with simulation time are as before; a band that ends fades into the smooth haze. The bands' tops are soft shoulders of uneven depth, ragged rather than smooth arcs, with finer layers of their own between them, as Cassini's and New Horizons' pictures show them.
- Triton's 2D clouds are as thin as its volumetric ones. A 2D layer is as opaque as its map says from every angle, and Triton's showed its wisps at up to 6% looking straight down, where the volumetric wisps are at most 0.3% and only gather toward the limb. A cloud vanished as you descended onto it. The 2D layer's opacity now follows the line of sight through the layer, from the volumetric layer's own map and density: faint over the disc, 10 to 20% along the limb.
- Jupiter's volumetric clouds keep the brightness of the 2D map as they take over from it. The game's mask put a cloud type 43 times as dense as the other on the dark belts, which drew them far darker in 3D than in 2D. Both types are now dense and equally bright, at the 2D map's brightness, so the cloud mask sets height alone.
- Saturn's, Uranus's and Neptune's volumetric clouds are as dense and as sharp-edged as Jupiter's, at their 2D maps' brightness. They were a third to a fifth as dense, with soft edges on their lower clouds, and looked wispy up close.
- Mars's dust clouds are as opaque up close as the 2D dust layer shows them from afar. The volumetric dust was a third as opaque as the same dust drawn in 2D, and its faint parts vanished as the volumetrics took over. A full dust cloud now has an optical depth of 0.4, an ordinary dusty day on Mars, where it had 0.24, and the thinner dust between the clouds shows as a light veil. The storm fronts and every height are unchanged.
- Mars's dust layer is measured from the mean surface instead of the floor of Hellas, so it thickens into the basins and thins over the heights. Above the mean surface it is as thick as before.
- Pluto's air and haze follow New Horizons' measurements. The haze thins out with the nitrogen, as the spacecraft measured, reaching the edge of the atmosphere at 634 km instead of 218 km. It scatters light the way New Horizons saw it at every angle to the Sun: a faint blue rim with the Sun behind you, a bright ring when Pluto is lit from behind. It was 4 to 8 times too faint lit from behind, and about 50 times too faint in sunlight. Its stacked layers are 11 km apart instead of 5.5 km, about 20 below 200 km as New Horizons counted.
- Triton's air and haze follow Voyager's measurements. The nitrogen thins out as measured, reaching the edge of the atmosphere at 349 km instead of 165 km. The haze is half as tall, an 11 km layer close to the ground instead of 20 km, and paler blue. Its stacked bands are now Pluto's laddered ones in this thinner haze: three in the 30 km Voyager traced it to, a few percent of its brightness. Voyager's pictures show one even layer and no bands, so these are modelled on Pluto's and nothing measured on Triton. It takes its strength, colour and forward scattering from the haze particles Voyager's photometry found, and it absorbs no light.
- Venus's clouds take its measured colour, a pale cream white, instead of the game's brown, and its 2D clouds are shaded more evenly across the disc.
- Venus's haze thickens toward the cloud base, as the Venera probes measured, instead of being thickest at the ground. The clear air low down turns gradually into the clouds instead of meeting them suddenly.
- Venus ends at its cloud tops, without a blue shell around them. The gas above them thins out as fast as the Venus International Reference Atmosphere's.
- Venus's clouds form one solid deck at 48–72 km instead of separate cumulus columns at 10–45 km with holes between them. The deck is densest at its base and solid up to about 70 km, where Venus's cloud tops are measured, so it meets the 2D clouds that replace it from afar. The game's cloud patterns stay on top as low lumps, where they used to tower 13 km.

### Fixed

- The haze bands on Titan, Pluto and Triton turn about each body's own pole. Titan's pattern turned about an axis lying in its equator, so its haze travelled north to south over the poles instead of round the planet, and the pattern turned against the ground instead of with it. The haze now follows the game's own rotation of each body, carried a little ahead of the ground or behind it by its winds. Titan's laps the ground every two days, as its stratosphere does (100 m/s). Pluto's falls behind the ground at 8 m/s, as circulation models have it. Triton's runs ahead at 10 m/s, the way Voyager saw its plumes carried.
- The giants' cloud maps stream with their winds again. Their flow was set so that the map moved 50 to 100 km before each loop restarted, a texture pixel or two, which showed nothing at any time warp. The map now travels up to 4,000 km a loop, at the same wind speeds (Jupiter 150 m/s, Saturn and Neptune 450, Uranus 200), so it shows under time warp. The volumetric clouds' belts and storms flow with it; the game moves only their fine detail, so they stood still wherever the volumetric clouds are drawn.
- Mars flickered from orbit under time warp, between its full look and a flatter one with a thicker-looking atmosphere and no clouds. The game draws one body in detail for the camera, and it picked Phobos over Mars whenever Phobos came nearer than Mars's centre, once every orbit of Phobos. Deimos, Pan, Miranda and Charon did the same to Mars, Saturn, Uranus and Pluto. A moon now takes over only once the camera is within 25 of its radii, 280 km from Phobos.
- Mars's cirrus, seen from afar as a 2D layer, went dark well before the terminator while the volumetric cirrus stayed lit past it. The 2D layer is shaded less like a solid surface, so it keeps more of its light toward the terminator. A 2D layer's Lambertian now also sets how it fades there: shaded layers (0.5 and up) as before, less shaded ones nearer their own shadow line, where the volumetric clouds go dark.
- The terminator on rugged ground with little or no air, most of all on Pluto, went from lit to black along one line, with only the peaks lit past it. The game drew the body's own shadow as a smooth ball's. It now falls over the range of horizons the body's relief allows, so slopes facing the Sun dim gradually past the terminator. Earth and the other bodies with thick air look as before.

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
