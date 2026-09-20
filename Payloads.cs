namespace RealAtmospheres;

/// <summary>
/// The patch payloads, ported verbatim from the four Python patchers in
/// "Content/Mars Test" (patch_mars_atmosphere.py, patch_mars_cirrus.py,
/// patch_mars_shaders.py, patch_cloud_terminator.py). All text is matched and
/// written with LF newlines; ShadowBuilder normalizes CRLF before applying.
/// Indentation inside the multi-line literals is significant: it must match the
/// stock Core files exactly (closing delimiters sit at column 0 so no dedent).
/// </summary>
internal static class Payloads
{
    // ---------------- Astronomicals.xml: Mars atmosphere (v4) ----------------
    // Pairs of (stock text, corrected text, already-applied marker). A pair is
    // applied only when the stock text occurs exactly once; if the marker is
    // already present the pair is silently skipped (hand-patched install).

    public static readonly (string Stock, string Corrected, string Marker)[] AtmospherePairs =
    {
        (
            "<Lambertian Value=\"0.20000000298023224\" />",
            "<Lambertian Value=\"0.65\" /> <!-- user-tuned with the terminator-fade shader patch -->",
            "user-tuned with the terminator-fade"
        ),
        (
            "<Coefficients R=\"0.00705758097024072\" G=\"0.005894684206252668\" B=\"0.003929789369254173\" />",
            "<Coefficients R=\"0.000119\" G=\"0.000278\" B=\"0.000682\" /> <!-- v4: fully derived CO2 lambda^-4 (dust phase now owns the colour) -->",
            "v4: fully derived CO2"
        ),
        (
"""
<MieScattering>
                    <Coefficients R="0.002309401186449168" G="0.002309401186449168" B="0.002309401186449168" />
                    <ScaleHeight Km="6" />
                    <PhaseFunctionAsymmetry X="0.6299999952316284" Y="0.699999988079071" Z="0.7300000190734863" />
                    <AbsorptionMultiplier Value="7" />
                </MieScattering>
""".Trim('\n'),
"""
<MieScattering>
                    <!-- dust = the colour engine (Titan technique): blue-heavy extinction reddens
                         transmitted light; v4 Mie-theory mode: sca spectrum from bhmie (red-heavy 1.18:1:0.76), blue halo via phase LUT fit -->
                    <Coefficients R="0.00943" G="0.0080" B="0.0061" />
                    <ScaleHeight Km="9.0" />
                    <PhaseFunctionAsymmetry X="-1" Y="-1" Z="-1" /> <!-- SENTINEL: selects MarsDustPhaseFunction in patched AtmosphereFunctions.glsl -->
                    <AbsorptionMultiplier Value="1.2" />
                </MieScattering>
""".Trim('\n'),
            "SENTINEL: selects MarsDustPhaseFunction"
        ),
    };

    // ---------------- Astronomicals.xml: Venus atmosphere (v1) ----------------
    // Port of the user's atmosphere.html Venus preset (densityMultiplier 53,
    // same unit system as KSA: beta = slider x 1e-3 x density, per km).
    // Composition-true dense CO2 Rayleigh replaces the stock inverted
    // yellow-tint spectrum; the preset's per-channel blue-selective Mie
    // absorption (the real "unknown UV absorber") moves into the Ozone block
    // because KSA Mie absorption is a grey scalar. Ozone tent (Altitude 0,
    // Extent 25) integrates to the same column as the HTML's exponential
    // absorber (0.1325/km over H=12.5 km).

    public static readonly (string Stock, string Corrected, string Marker)[] VenusPairs =
    {
        (
"""
<RayleighScattering>
                    <Coefficients R="0.014694534304543984" G="0.011939309340552273" B="0.0026021572012503305" />
                    <ScaleHeight Km="25" />
                </RayleighScattering>
""".Trim('\n'),
"""
<RayleighScattering>
                    <!-- Real Atmospheres v1: composition-true dense CO2 Rayleigh (atmosphere.html preset, x53 density) -->
                    <Coefficients R="0.1696" G="0.212" B="0.424" />
                    <ScaleHeight Km="15.9" />
                </RayleighScattering>
""".Trim('\n'),
            "composition-true dense CO2 Rayleigh"
        ),
        (
"""
<MieScattering>
                    <Coefficients R="0.02078460879609483" G="0.02078460879609483" B="0.02078460879609483" />
                    <ScaleHeight Km="22" />
                    <PhaseFunctionAsymmetry X="0.30000001192092896" Y="0.20000000298023224" Z="0.10000000149011612" />
                    <AbsorptionMultiplier Value="1.1109999418258667" />
                </MieScattering>
""".Trim('\n'),
"""
<MieScattering>
                    <!-- Real Atmospheres v1: faint H2SO4 droplet scatter; per-channel absorption lives in the Ozone block -->
                    <Coefficients R="0.00424" G="0.00318" B="0.0" />
                    <ScaleHeight Km="12.5" />
                    <PhaseFunctionAsymmetry X="0.85" Y="0.85" Z="0.85" />
                    <AbsorptionMultiplier Value="1.0" />
                </MieScattering>
""".Trim('\n'),
            "faint H2SO4 droplet scatter"
        ),
        (
"""
<Ozone>
                    <Coefficients R="0.004764705938889696" G="0.006117647004478116" B="0.010823529418777013" />
                    <Altitude Km="0" />
                    <Extent Km="100" />
                </Ozone>
""".Trim('\n'),
"""
<Ozone>
                    <!-- Real Atmospheres v1: Venus 'unknown UV absorber' - blue-selective, tent 0-25 km matches the HTML absorber column -->
                    <Coefficients R="0.0" G="0.0212" B="0.1325" />
                    <Altitude Km="0" />
                    <Extent Km="25" />
                </Ozone>
""".Trim('\n'),
            "Venus 'unknown UV absorber'"
        ),
        // anchored on Venus's unique 2D cloud Color line: a bare 0.65 anchor
        // collides with the line the Mars lambertian pair just created.
        (
            "<Color R=\"0.45551604\" G=\"0.38998407\" B=\"0.30151597\" />\n                    <Lambertian Value=\"0.65\" />",
            "<Color R=\"0.45551604\" G=\"0.38998407\" B=\"0.30151597\" />\n                    <Lambertian Value=\"0.55\" /> <!-- Real Atmospheres: user-tuned Venus 2D cloud shading -->",
            "user-tuned Venus 2D cloud shading"
        ),
    };

    // ---------------- Astronomicals.xml: Jupiter atmosphere (v3) ----------------
    // v1 (deck-anchored via StartHeight=220) blew out the showcase storm towers
    // above the auto-computed atmosphere top; v2 (H=285 envelope) covered them
    // but read too tall and too blue. v3 pairs with the cloud rescale below:
    // with realistic cloud altitudes there is nothing to envelope, so the
    // atmosphere becomes the SE-equivalent shell the user likes - SpaceEngine's
    // Jupiter is literally Model "Earth" at Height 300 (x5 stretch of the
    // 60 km Earth profile): Earth spectrum, beta/5, H_ray=40, H_mie=6, total
    // optical depth preserved. Chromophore absorber (tan tint) sits just above
    // the deck. Physical scale height matched to visual (Saturn convention).

    // ---------------- Astronomicals.xml: Saturn atmosphere (transferred from SE) ----------------
    // SE's gas giants ALL use Model "Earth" = the Earth lambda^-4 spectrum; that's
    // why our physics-derived Jupiter (also Earth-spectrum x0.188) looks like SE's.
    // Saturn transfer: keep the Earth spectrum, magnitude Earth x0.30 (physics
    // anchor is x0.23 = Jupiter's x0.188 scaled by Saturn's 1.23x colder-denser
    // 1-bar density; nudged up toward SE's brighter intent since KSA can't
    // exposure-compensate Saturn's distance like SE's Bright=5.0 does - THIS IS
    // THE BRIGHTNESS TUNING KNOB). H=52 km from physics (kT/mug; Saturn's low
    // gravity makes it ~2x puffier than Jupiter - NOT SE's artistic Height=250).
    // Mie haze slightly stronger than Jupiter's (SE Opacity 0.2 vs Jupiter 0).
    // Mild gold chromophore in Ozone (paler than Jupiter). Physical H = visual.
    public static readonly (string Stock, string Corrected, string Marker)[] SaturnPairs =
    {
        (
"""
<Coefficients R="0.0004235294423997408" G="0.0005552941466867926" B="0.0007058824159204974" />
                    <ScaleHeight Km="60" />
""".Trim('\n'),
"""
<!-- Real Atmospheres: gas-giant Rayleigh = only the thin gas column ABOVE the reflective
                         haze deck (~tens of mbar), NOT the full 1-bar column - user-tuned to 0.00005 (~1-2% of
                         full column, matches photos). lambda^-4 tilt preserved (still molecular gas). -->
                    <Coefficients R="0.00008" G="0.000186" B="0.000456" /> <!-- bumped x1.6: tall clouds cut the limb (user) -->
                    <ScaleHeight Km="52" />
""".Trim('\n'),
            "SE-transferred, Earth spectrum"
        ),
        (
"""
<MieScattering>
                    <Coefficients R="0.0008660254113122294" G="0.0008660254113122294" B="0.0008660254113122294" />
                    <ScaleHeight Km="30" />
                    <PhaseFunctionAsymmetry X="0.10000000149011612" Y="0.10000000149011612" Z="0.10000000149011612" />
                    <AbsorptionMultiplier Value="1.1111111640930176" />
                </MieScattering>
                <Ozone>
                    <Coefficients R="0" G="0" B="0" />
                    <Altitude Km="0" />
                    <Extent Km="0" />
                </Ozone>
""".Trim('\n'),
"""
<MieScattering>
                    <!-- Real Atmospheres: Saturn haze (SE Opacity 0.2) - THIN above-cloud only, so low
                         tau + gentle forward lobe (a strong sun halo needs a thick haze this deck lacks) -->
                    <Coefficients R="0.0002" G="0.0002" B="0.0002" />
                    <ScaleHeight Km="8" />
                    <PhaseFunctionAsymmetry X="0.4" Y="0.4" Z="0.4" />
                    <AbsorptionMultiplier Value="1.2" />
                </MieScattering>
                <Ozone>
                    <!-- Real Atmospheres: pale gold chromophore (milder than Jupiter's), scaled to H=52 -->
                    <Coefficients R="0.00012" G="0.000125" B="0.00028" />
                    <Altitude Km="20" />
                    <Extent Km="30" />
                </Ozone>
""".Trim('\n'),
            "Saturn haze (SE Opacity 0.2)"
        ),
        (
"""
<SeaLevelDensity KgPerM3="0.19"/>
                <ScaleHeight Km="60" />
""".Trim('\n'),
"""
<SeaLevelDensity KgPerM3="0.19"/>
                <ScaleHeight Km="52" /> <!-- Real Atmospheres: Saturn physical H matched to visual -->
""".Trim('\n'),
            "Saturn physical H matched to visual"
        ),
    };

    // ---------------- Astronomicals.xml: Uranus atmosphere (ice giant) ----------------
    // Ice-giant recipe (all lessons applied): the cyan is NOT big Rayleigh - it's
    // CH4 absorbing red (Ozone slot, R-heavy). Rayleigh is a THIN above-haze
    // sliver (λ⁻⁴). Mie a thin gentle haze (Uranus's thick photochemical haze
    // mutes it to PALE cyan; low g so no sun halo). H=28 (kT/μg). Stock Uranus
    // was a thick full-column Rayleigh + odd ozone. The user's new map carries the
    // base disk colour; the atmosphere adds the limb + methane tint. FIRST PASS -
    // tune the Ozone R (blue depth), Mie (paleness), Rayleigh (limb) live.
    public static readonly (string Stock, string Corrected, string Marker)[] UranusPairs =
    {
        (
"""
<Coefficients R="0.007086390319582045" G="0.012379838674277414" B="0.01758790819132919" />
                    <ScaleHeight Km="32" />
""".Trim('\n'),
"""
<!-- Real Atmospheres: thin above-haze λ⁻⁴ Rayleigh (cyan comes from CH4 absorber, not this) -->
                    <Coefficients R="0.000084" G="0.000196" B="0.00048" /> <!-- bumped x1.2 (user) -->
                    <ScaleHeight Km="28" />
""".Trim('\n'),
            "cyan comes from CH4 absorber"
        ),
        (
"""
<MieScattering>
                    <Coefficients R="0.0017320508226244587" G="0.0017320508226244587" B="0.0017320508226244587" />
                    <ScaleHeight Km="32" />
                    <PhaseFunctionAsymmetry X="0.10000000149011612" Y="0.10000000149011612" Z="0.10000000149011612" />
                    <AbsorptionMultiplier Value="6.111000061035156" />
                </MieScattering>
                <Ozone>
                    <Coefficients R="0.00018257418872565814" G="9.128709436282907E-05" B="0.0004564354891577625" />
                    <Altitude Km="0" />
                    <Extent Km="200" />
                </Ozone>
""".Trim('\n'),
"""
<MieScattering>
                    <!-- Real Atmospheres: thin muting haze (Uranus's photochemical haze -> PALE cyan); gentle g, no sun halo -->
                    <Coefficients R="0.0004" G="0.0004" B="0.0004" />
                    <ScaleHeight Km="20" />
                    <PhaseFunctionAsymmetry X="0.3" Y="0.3" Z="0.3" />
                    <AbsorptionMultiplier Value="2.0" />
                </MieScattering>
                <Ozone>
                    <!-- Real Atmospheres: CH4 absorber - R-heavy (removes red -> cyan); moderate (Uranus is pale); R nudged up (user) -->
                    <Coefficients R="0.00045" G="0.0002" B="0.00003" />
                    <Altitude Km="0" />
                    <Extent Km="100" />
                </Ozone>
""".Trim('\n'),
            "Uranus's photochemical haze"
        ),
    };

    // ---------------- SolSystem(Dense).xml: Neptune atmosphere (ice giant) ----------------
    // Same recipe as Uranus but DEEPER blue: less muting haze + a STRONGER CH4
    // red-absorber. H=21 (kT/μg). Stock Neptune was already λ⁻⁴-ish but full-
    // column-thick. Applied to both system files.
    public static readonly (string Stock, string Corrected, string Marker)[] NeptunePairs =
    {
        (
"""
<Coefficients R="0.003607843271949712" G="0.005411764736561217" B="0.021917647165936582" />
                    <ScaleHeight Km="32" />
""".Trim('\n'),
"""
<!-- Real Atmospheres: thin above-haze λ⁻⁴ Rayleigh (cyan comes from CH4 absorber, not this) -->
                    <Coefficients R="0.00012" G="0.00028" B="0.000684" /> <!-- bumped x1.2 (user) -->
                    <ScaleHeight Km="21" />
""".Trim('\n'),
            "cyan comes from CH4 absorber"
        ),
        (
"""
<MieScattering>
                    <Coefficients R="0.0017320508226244587" G="0.0017320508226244587" B="0.0017320508226244587" />
                    <ScaleHeight Km="32" />
                    <PhaseFunctionAsymmetry X="0.10000000149011612" Y="0.10000000149011612" Z="0.10000000149011612" />
                    <AbsorptionMultiplier Value="6.111000061035156" />
                </MieScattering>
                <Ozone>
                    <Coefficients R="0.0014606337246560659" G="0.0008746309597134718" B="0.0010495571383624897" />
                    <Altitude Km="0" />
                    <Extent Km="300" />
                </Ozone>
""".Trim('\n'),
"""
<MieScattering>
                    <!-- Real Atmospheres: less haze than Uranus (Neptune is clearer -> deeper blue); gentle g -->
                    <Coefficients R="0.0002" G="0.0002" B="0.0002" />
                    <ScaleHeight Km="15" />
                    <PhaseFunctionAsymmetry X="0.3" Y="0.3" Z="0.3" />
                    <AbsorptionMultiplier Value="2.0" />
                </MieScattering>
                <Ozone>
                    <!-- Real Atmospheres: STRONG CH4 R-heavy absorber -> deep Neptune blue; R nudged up (user) -->
                    <Coefficients R="0.001" G="0.00045" B="0.00008" />
                    <Altitude Km="0" />
                    <Extent Km="150" />
                </Ozone>
""".Trim('\n'),
            "Neptune is clearer"
        ),
    };

    // ---------------- SolSystem(Dense).xml: Titan forward-scattering pass ----------------
    // Frees the Mie slot for a forward lobe by moving the blue-kill absorber to the
    // OZONE slot (per-channel; tent Alt0/Ext90 reproduces the stock Mie×100 column).
    // The 2019 attempt washed WHITE because its forward Mie was conservative; here the
    // Mie KEEPS absorption (AbsorptionMultiplier 0.3, SSA~0.77) AND is only ~10% of the
    // haze (bulk stays in the near-isotropic Rayleigh) so it can't dominate multiple
    // scattering. g=0.6 forward lobe = the sunward crescent halo. FIRST PASS - tune g,
    // the Mie fraction, and mult live.
    public static readonly (string Stock, string Corrected, string Marker)[] TitanPairs =
    {
        (
"""
<RayleighScattering>
                    <Coefficients R="0.10978052645012698" G="0.10787130002933647" B="0.1890134301676055" />
                    <ScaleHeight Km="40" />
                </RayleighScattering>
""".Trim('\n'),
"""
<RayleighScattering>
                    <!-- Real Atmospheres: bulk haze, 90% (10% moved to the forward Mie lobe) -->
                    <Coefficients R="0.0988" G="0.0971" B="0.1701" />
                    <ScaleHeight Km="40" />
                </RayleighScattering>
""".Trim('\n'),
            "bulk haze, 90%"
        ),
        (
"""
<MieScattering>
                    <Coefficients R="6.743806227624956E-05" G="0.00026975224910499824" B="0.0014740034075936717" />
                    <ScaleHeight Km="45" />
                    <PhaseFunctionAsymmetry X="0" Y="0" Z="0" />
                    <AbsorptionMultiplier Value="100" />
                </MieScattering>
""".Trim('\n'),
"""
<MieScattering>
                    <!-- Real Atmospheres: forward-scatter lobe (aggregate, g~0.6); absorbing (SSA~0.77) so it can't wash white -->
                    <Coefficients R="0.011" G="0.0108" B="0.0189" />
                    <ScaleHeight Km="45" />
                    <PhaseFunctionAsymmetry X="0.6" Y="0.6" Z="0.6" />
                    <AbsorptionMultiplier Value="0.3" />
                </MieScattering>
""".Trim('\n'),
            "forward-scatter lobe (aggregate"
        ),
        (
"""
<Ozone>
                    <Coefficients R="2.9026752316166213E-11" G="2.7065485469193217E-11" B="9.178729440026801E-11" />
                    <Altitude Km="300" />
                    <Extent Km="20" />
                </Ozone>
""".Trim('\n'),
"""
<Ozone>
                    <!-- Real Atmospheres: tholin blue-kill moved here from the Mie×100 slot (same column: tent 0-90 km) -->
                    <Coefficients R="0.0067" G="0.027" B="0.147" />
                    <Altitude Km="0" />
                    <Extent Km="90" />
                </Ozone>
""".Trim('\n'),
            "tholin blue-kill moved here"
        ),
    };

    // ---------------- Volumetric clouds for Saturn/Uranus/Neptune (Jupiter-style) ----------------
    // {BODY} = Saturn|Uranus|Neptune, {ASSETS} = absolute mod assets dir. Coverage
    // from a banding mask (make_giant_clouds.py, from the diffuse); COLOUR sampled
    // from the planet's own Diffuse cubemap (VolumetricsColorMap {BODY}_Diffuse) -
    // exactly like Jupiter. Detail + flowmap reuse Jupiter's Core textures by path
    // (no load-order dependency). Inserted after the body's </Atmosphere> (Clouds
    // is a sibling of Atmosphere). FIRST PASS - altitudes/density/coverage tunable.
    public const string GiantCloudsTemplate =
"""

        <!-- Real Atmospheres: Jupiter-style volumetric clouds (banding mask + diffuse colour) -->
        <Clouds>
            <OrbitTransitionStartAltitude Km="400" />
            <OrbitTransitionEndAltitude Km="800" />
            <MaxShadowsAltitude Km="800" />
            <VolumetricsFlickerReductionDistance Km="800" />
            <Layer Id="{BODY}Clouds">
                <RotationSpeed X="0" Y="0" Z="0" />
                <VolumetricCloud>
                    <Texture Id="{BODY}CloudsMask" Path="{ASSETS}\{BODY}CloudsMask.dds" Category="Terrain">
                        <IsVirtual>false</IsVirtual>
                        <Manifest><MaxSize>0</MaxSize><MipMaps>false</MipMaps></Manifest>
                    </Texture>
                    <Detail>
                        <DetailSelector Id="{BODY}CloudsMask"/>
                        <Texture Path="{ASSETS}\GiantCloudDetail.dds" Category="Terrain">
                            <IsVirtual>false</IsVirtual>
                            <Manifest><MaxSize>0</MaxSize><MipMaps>false</MipMaps></Manifest>
                        </Texture>
                        <Size Km="40000"/>
                    </Detail>
                    <Color R="1" G="1" B="1" />
                    <VolumetricsFlowMap>
                        {VOLFLOWTEX}
                        <Displacement Km="{DISP}"/> <!-- per-planet, calibrated to real peak zonal winds -->
                        <LoopDuration Hours="1.0"/>
                    </VolumetricsFlowMap>
                    <VolumetricsColorMap Id="{BODY}_Diffuse"/>
                    <Raymarching>
                        <Step Scale="0.00275">
                            <Size M="2000" />
                            <MaxSize M="30000" />
                        </Step>
                        <LightDistance M="30000" />
                        <LightSamples Value="6" />
                    </Raymarching>
                    <Noise>
                        <ScrollSpeed Value="30" />
                    </Noise>
                    <CloudType Name="GiantBands">
                        <StartAltitude M="5000" />
                        <Height M="{HEIGHT}" /> <!-- tower height, per-planet (user) -->
                        <Density Value="0.0006" />
                        <NoiseScale M="70000" /> <!-- finer noise -> more defined towers -->
                        <EdgeSharpness Value="0.95" />
                        <MultipleScatteringBrightness Value="1" />
                        <!-- ShapeCurve keeps density high well up the column so noise-driven peaks
                             tower rather than roll; sharp base, gradual taper to the tops -->
                        <CloudShape InterpolateShapes="true">
                            <ShapeCurve>
                                <SplinePoint><Key Value="0.0"/><Value Value="0.0"/><InTangent Value="3.5"/><OutTangent Value="3.5"/></SplinePoint>
                                <SplinePoint><Key Value="0.18"/><Value Value="1.0"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.55"/><Value Value="1.0"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.8"/><Value Value="0.55"/><InTangent Value="-1.5"/><OutTangent Value="-1.5"/></SplinePoint>
                                <SplinePoint><Key Value="1.0"/><Value Value="0.0"/><InTangent Value="-2.5"/><OutTangent Value="-2.5"/></SplinePoint>
                            </ShapeCurve>
                        </CloudShape>
                    </CloudType>

                    <!-- Types 1-3 give the deck vertical variety. They top out BELOW the deck, the
                         way Jupiter's storm types do, so vortices read as pits in it and the
                         layer's top (where the 2D billboard hangs) does not move. The shader picks a type from
                         the detail tile's green channel, blended by our mask's red channel
                         (GetCoverageAndCloudType in CloudFunctions.glsl), so most of the planet
                         stays on the deck above and storms appear where the mask says so. -->
                    <CloudType Name="GiantBandEdge">
                        <StartAltitude M="5000" />
                        <Height M="{HEIGHT_EDGE}" />
                        <Density Value="0.00042" />
                        <NoiseScale M="95000" />
                        <EdgeSharpness Value="0.6" />
                        <MultipleScatteringBrightness Value="1" />
                        <!-- ragged belt edges: rounder than the deck, taper starts earlier -->
                        <CloudShape InterpolateShapes="true">
                            <ShapeCurve>
                                <SplinePoint><Key Value="0.0"/><Value Value="0.0"/><InTangent Value="3.0"/><OutTangent Value="3.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.22"/><Value Value="1.0"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.45"/><Value Value="0.9"/><InTangent Value="-0.4"/><OutTangent Value="-0.4"/></SplinePoint>
                                <SplinePoint><Key Value="0.75"/><Value Value="0.4"/><InTangent Value="-1.8"/><OutTangent Value="-1.8"/></SplinePoint>
                                <SplinePoint><Key Value="1.0"/><Value Value="0.0"/><InTangent Value="-2.0"/><OutTangent Value="-2.0"/></SplinePoint>
                            </ShapeCurve>
                        </CloudShape>
                    </CloudType>
                    <CloudType Name="GiantStormEdge">
                        <StartAltitude M="5000" />
                        <Height M="{HEIGHT_STORM}" />
                        <Density Value="0.00035" />
                        <NoiseScale M="140000" />
                        <EdgeSharpness Value="0.0" />
                        <MultipleScatteringBrightness Value="1" />
                        <!-- anvil: thin at the base, broad plateau aloft, like Jupiter's StormEdge -->
                        <CloudShape InterpolateShapes="true">
                            <ShapeCurve>
                                <SplinePoint><Key Value="0.0"/><Value Value="0.0"/><InTangent Value="0.8"/><OutTangent Value="0.8"/></SplinePoint>
                                <SplinePoint><Key Value="0.35"/><Value Value="0.55"/><InTangent Value="1.6"/><OutTangent Value="1.6"/></SplinePoint>
                                <SplinePoint><Key Value="0.62"/><Value Value="1.0"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.88"/><Value Value="0.85"/><InTangent Value="-0.8"/><OutTangent Value="-0.8"/></SplinePoint>
                                <SplinePoint><Key Value="1.0"/><Value Value="0.0"/><InTangent Value="-3.0"/><OutTangent Value="-3.0"/></SplinePoint>
                            </ShapeCurve>
                        </CloudShape>
                    </CloudType>
                    <CloudType Name="GiantStormCenter">
                        <StartAltitude M="5000" />
                        <Height M="{HEIGHT_CORE}" />
                        <Density Value="0.0016" />
                        <NoiseScale M="170000" />
                        <EdgeSharpness Value="0.97" />
                        <MultipleScatteringBrightness Value="2.0" />
                        <!-- vortex core: roots below the deck, tower punching well above it -->
                        <CloudShape InterpolateShapes="true">
                            <ShapeCurve>
                                <SplinePoint><Key Value="0.0"/><Value Value="0.7"/><InTangent Value="1.2"/><OutTangent Value="1.2"/></SplinePoint>
                                <SplinePoint><Key Value="0.25"/><Value Value="1.0"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.65"/><Value Value="0.95"/><InTangent Value="-0.3"/><OutTangent Value="-0.3"/></SplinePoint>
                                <SplinePoint><Key Value="0.88"/><Value Value="0.5"/><InTangent Value="-2.2"/><OutTangent Value="-2.2"/></SplinePoint>
                                <SplinePoint><Key Value="1.0"/><Value Value="0.0"/><InTangent Value="-3.5"/><OutTangent Value="-3.5"/></SplinePoint>
                            </ShapeCurve>
                        </CloudShape>
                    </CloudType>
                </VolumetricCloud>
                <TwoDimensionalCloud>
                    <Texture Id="{BODY}_Diffuse"/>
                    <Color R="1" G="1" B="1" />
                    <Lambertian Value="0.95" /> <!-- match Jupiter's top deck (user) -->{FLOWMAP}
                </TwoDimensionalCloud>
                <MatchGroundColor Value="false" />
            </Layer>
        </Clouds>
""";

    // Per-planet 2D-cloud flowmap (make_giant_flowmaps.py): zonal jets derived from
    // the planet's own bands + turbulent eddies (west-red/east-green lean), and for
    // Neptune the Great Dark Spot vortex. Same structure/encoding as stock
    // Jupiter2DFlowmap. Inserted at {FLOWMAP} only when the DDS exists (delete the
    // .dds to fall back to a static 2D cloud). Displacement/loop are user-tunable.
    public const string GiantFlowMapTemplate =
"""

                    <FlowMap>
                        <Texture Id="{BODY}2DFlowmap" Path="{ASSETS}\{BODY}Flowmap.dds" Category="Terrain">
                            <IsVirtual>false</IsVirtual>
                            <Manifest><MaxSize>0</MaxSize><MipMaps>true</MipMaps></Manifest>
                        </Texture>
                        <Displacement Km="{DISP}"/> <!-- per-planet, calibrated to real peak zonal winds -->
                        <LoopDuration Hours="1.0"/>
                    </FlowMap>
""";

    // <VolumetricsFlowMap> texture, substituted at {VOLFLOWTEX}. Same per-planet
    // equirect flowmap as the 2D <FlowMap> (both stock Jupiter flowmaps are 2:1
    // BC5U equirects), so the volumetric clouds flow with the planet's own bands
    // instead of Jupiter's. Falls back to Jupiter's volumetric flowmap when the
    // per-planet DDS is absent. Displacement/loop stay the volumetric's own.
    public const string GiantVolFlowTexPerPlanet =
"""
<Texture Id="{BODY}VolFlowmap" Path="{ASSETS}\{BODY}Flowmap.dds" Category="Terrain">
                            <IsVirtual>false</IsVirtual>
                            <Manifest><MaxSize>0</MaxSize><MipMaps>true</MipMaps></Manifest>
                        </Texture>
""";

    public const string GiantVolFlowTexJupiter =
"""
<Texture Path="Textures/Clouds/Compressed/JupiterFlowmap.dds" Category="Terrain">
                            <IsVirtual>false</IsVirtual>
                            <Manifest><MaxSize>0</MaxSize><MipMaps>false</MipMaps></Manifest>
                        </Texture>
""";

    public static readonly (string Stock, string Corrected, string Marker)[] JupiterPairs =
    {
        (
"""
<RayleighScattering>
                    <Coefficients R="1.8e-4" G="1.8e-4" B="1.1e-4" />
                    <ScaleHeight Km="650"/>
                </RayleighScattering>
                <MieScattering>
                    <Coefficients R="4e-4" G="4e-4" B="4e-4"/>
                    <ScaleHeight Km="100"/>
                    <PhaseFunctionAsymmetry X="0.1" Y="0.1" Z="0.1"/>
                </MieScattering>
""".Trim('\n'),
"""
<RayleighScattering>
                    <!-- Real Atmospheres v3.2: physically-derived H2/He Rayleigh (x0.31 Earth from composition, x0.6 exposure comp), true H = kT/mg -->
                    <Coefficients R="0.00109" G="0.00254" B="0.00622" />
                    <ScaleHeight Km="25"/>
                </RayleighScattering>
                <MieScattering>
                    <!-- Real Atmospheres v3: Earth-model haze /5, stretched to H=6 -->
                    <Coefficients R="0.000693" G="0.000693" B="0.000693"/>
                    <ScaleHeight Km="6"/>
                    <PhaseFunctionAsymmetry X="0.75" Y="0.75" Z="0.75"/>
                    <AbsorptionMultiplier Value="1.1111"/>
                </MieScattering>
                <Ozone>
                    <!-- Real Atmospheres v3: Jupiter chromophore layer - blue-heavy absorber (tan tint), just above the realistic deck -->
                    <Coefficients R="0.000195" G="0.000205" B="0.0004825" />
                    <Altitude Km="10" />
                    <Extent Km="15" />
                </Ozone>
""".Trim('\n'),
            "SE-equivalent Jupiter shell"
        ),
        (
"""
<SeaLevelPressure Atm="1.0"/>
                <SeaLevelDensity KgPerM3="0.16"/>
                <ScaleHeight Km="150" />
""".Trim('\n'),
"""
<SeaLevelPressure Atm="1.0"/>
                <SeaLevelDensity KgPerM3="0.16"/>
                <ScaleHeight Km="25" /> <!-- Real Atmospheres v3.2: physical H matched to visual (Saturn convention) -->
""".Trim('\n'),
            "physical H matched to visual"
        ),
        // cloud transition settings: stock 5000/9000 km was tuned for the
        // 220-770 km showcase towers; rescaled with the realistic deck (~x6.5
        // of the ~111 km cloud tops, same ratio as stock used).
        (
            "<OrbitTransitionStartAltitude Km=\"5000\" />",
            "<OrbitTransitionStartAltitude Km=\"700\" /> <!-- Real Atmospheres: rescaled with the realistic deck altitudes -->",
            "rescaled with the realistic deck altitudes"
        ),
        (
            "<OrbitTransitionEndAltitude Km=\"9000\" />",
            "<OrbitTransitionEndAltitude Km=\"1300\" />",
            "<OrbitTransitionEndAltitude Km=\"1300\" />"
        ),
        (
            "<MaxShadowsAltitude Km=\"9000\" />",
            "<MaxShadowsAltitude Km=\"1300\" />",
            "<MaxShadowsAltitude Km=\"1300\" />"
        ),
        (
            "<VolumetricsFlickerReductionDistance Km=\"10000.0\"/>",
            "<VolumetricsFlickerReductionDistance Km=\"1500\"/>",
            "<VolumetricsFlickerReductionDistance Km=\"1500\"/>"
        ),
    };

    // ---------------- Astronomicals.xml: Jupiter clouds at realistic altitudes ----------------
    // The stock showcase decks (220 km base, towers to 770 km) forced a puffed-up
    // atmosphere to envelope them. Rescaled to the real ammonia-deck geometry
    // (mesh = 1 bar): lower deck -25..+5 km, main deck base 5 km with structures
    // 4-40 km tall (storm tops ~45 km). Densities are multiplied by the same
    // factor the heights shrank by (optical depth ~ density x thickness stays
    // put), and the raymarch steps/light distance shrink to match the new scale.
    // Applied only inside the two Jupiter cloud layers' span.

    public const string JupiterCloudsMarker = "Jupiter clouds rescaled to realistic altitudes";
    public const string JupiterCloudsMarkerComment =
        "<!-- Real Atmospheres v3: Jupiter clouds rescaled to realistic altitudes -->\n            ";

    public static readonly (string Stock, string Corrected, int Count)[] JupiterCloudEdits =
    {
        // lower deck: -4..216 km -> -25..+15 km (meets the raised main deck)
        // Lower deck: -4..216 km -> -32..+8 km, 7 km below the main deck's base. Stock
        // separates the decks (216 against 220), and closing that gap in the rescale put
        // this deck's flat top exactly where the vortices bottom out. Its top
        // is also brightened: shadowed from above and lit by a thin atmosphere, it read as
        // a flat black floor. Thickness is unchanged, so the density and optical depth
        // still hold. Height, density and brightness travel together because every anchor
        // is matched against the pristine span.
        ("<StartAltitude M=\"-4000\" />\n                        <Height M=\"220000\" />\n                        <Density Value=\"0.0002800000074785203\" />\n                        <NoiseScale M=\"120000\" />\n                        <EdgeSharpness Value=\"0.9700000286102295\" />\n                        <MultipleScatteringBrightness Value=\"1\" />",
         "<StartAltitude M=\"-32000\" />\n                        <Height M=\"40000\" />\n                        <Density Value=\"0.00154\" />\n                        <NoiseScale M=\"120000\" />\n                        <EdgeSharpness Value=\"0.9700000286102295\" />\n                        <MultipleScatteringBrightness Value=\"3.0\" />", 1),
        ("<StartAltitude M=\"-4000\" />\n                        <Height M=\"220000\" />\n                        <Density Value=\"0.0007999999797903001\" />\n                        <NoiseScale M=\"120000\" />\n                        <EdgeSharpness Value=\"0.97\" />\n                        <MultipleScatteringBrightness Value=\"1\" />",
         "<StartAltitude M=\"-32000\" />\n                        <Height M=\"40000\" />\n                        <Density Value=\"0.0044\" />\n                        <NoiseScale M=\"120000\" />\n                        <EdgeSharpness Value=\"0.97\" />\n                        <MultipleScatteringBrightness Value=\"3.0\" />", 1),
        // main deck: base 220 km -> 15 km (raised so vortex roots clear the
        // mesh - black spots fix); heights /5.73 (towers ~20% taller than the
        // x2 cut, tops ~111 km), densities scaled inversely
        ("<StartAltitude M=\"220000\" />", "<StartAltitude M=\"15000\" />",  4),
        ("<Height M=\"440000\" />",        "<Height M=\"76000\" />",         1),
        ("<Height M=\"550000\" />",        "<Height M=\"96000\" />",         1),
        ("<Density Value=\"0.0000081\" />","<Density Value=\"0.0000464\" />", 1),
        ("<Density Value=\"0.00035\" />",  "<Density Value=\"0.002\" />",    1),
        // The two storm types carry height, density and brightness in one edit each,
        // because every anchor is checked against the pristine span before anything is
        // applied: an anchor written against already-edited text skips the whole rescale.
        // Brightness is raised because our Jupiter atmosphere is thin (H 25 km against
        // stock's 650), so little inscatter reaches a vortex pit whose walls tower ~85 km
        // above its floor, and the deck's own shadows finish the job, leaving the cores
        // black. Brightening keeps the sunken vortices, which are worth keeping. Too
        // bright: lower these. Still dark: raise the storm heights and drop their
        // densities in step, so the optical depth holds.
        ("<Height M=\"55000\" />\n                        <Density Value=\"0.00018\" />\n                        <NoiseScale M=\"420000\" />\n                        <EdgeSharpness Value=\"0.97\" />\n                        <MultipleScatteringBrightness Value=\"1.0\" />",
         "<Height M=\"10000\" />\n                        <Density Value=\"0.00103\" />\n                        <NoiseScale M=\"420000\" />\n                        <EdgeSharpness Value=\"0.97\" />\n                        <MultipleScatteringBrightness Value=\"2.4\" />", 1),
        ("<Height M=\"330000\" />\n                        <Density Value=\"0.0004\" />\n                        <NoiseScale M=\"420000\" />\n                        <EdgeSharpness Value=\"0.0\" />\n                        <MultipleScatteringBrightness Value=\"1.0\" />",
         "<Height M=\"56000\" />\n                        <Density Value=\"0.00229\" />\n                        <NoiseScale M=\"420000\" />\n                        <EdgeSharpness Value=\"0.0\" />\n                        <MultipleScatteringBrightness Value=\"1.5\" />", 1),
        // raymarch scale: steps and light reach shrink with the layer thickness
        ("<Size M=\"10000\" />",           "<Size M=\"1500\" />",            2),
        ("<MaxSize M=\"175000\" />",       "<MaxSize M=\"25000\" />",        2),
        ("<LightDistance M=\"120000\" />", "<LightDistance M=\"35000\" />",  2),
        // Noise scale follows the rescale: /5.73 for the main deck, /5.5 for the lower
        // one, which puts noise/height back in stock's 0.5-1.3 band (7.3 for the vortex
        // core, stock 7.6). Without this the noise is far larger than the deck is deep,
        // so storms render as hard-edged slabs instead of being eroded into shape.
        // Must stay after the per-type edits above: those carry the stock NoiseScale
        // through untouched, so the counts here still match.
        ("<NoiseScale M=\"420000\" />",    "<NoiseScale M=\"73000\" />",     4),
        ("<NoiseScale M=\"120000\" />",    "<NoiseScale M=\"22000\" />",     2),
        // 2D billboards (user-tuned): JupiterClouds 0.95 (its 2D block is the
        // one with a FlowMap - unique discriminator), JupiterLowerClouds 1.
        // Order matters: the specific edit must run before the generic one;
        // counts are verified against the pristine span.
        ("<Lambertian Value=\"0.5\" />\n                    <FlowMap>",
         "<Lambertian Value=\"0.95\" />\n                    <FlowMap>",     1),
        ("<Lambertian Value=\"0.5\" />",   "<Lambertian Value=\"1\" />",     2),
        // Flow-rate realism: stock uses "ridiculous" demo speeds (the 2D flowmap
        // is ~4000 m/s vs Jupiter's real ~150 m/s peak jets). Calibrate all three
        // Jupiter flowmaps to ~150 m/s: velocity = flowValue_peak x Disp / Loop,
        // flowValue_peak ~= 0.70. 2D (1h) & vol-B (1h) -> 770 km; vol-A (0.5h) -> 385 km.
        ("<Displacement Km=\"21080.0\"/>", "<Displacement Km=\"770.0\"/>",   1),
        ("<Displacement Km=\"1840.0\"/>",  "<Displacement Km=\"385.0\"/>",   1),
        ("<Displacement Km=\"1240.0\"/>",  "<Displacement Km=\"770.0\"/>",   1),
    };

    // ---------------- SolSystem(Dense).xml: Titan cloud transition settings ----------------
    // The stock block was copied from Venus (150/250 km) for the 20-55 km
    // placeholder clouds; our haze laminae sit at 230-292 km, so the
    // volumetric->2D fade completed BELOW the layers (only the faint 2D smudge
    // ever showed from orbit). Raised well above the top lamina.

    public static readonly (string Stock, string Corrected, string Marker)[] TitanTransitionPairs =
    {
        (
"""
<OrbitTransitionStartAltitude Km="150" />
            <OrbitTransitionEndAltitude Km="250" />
            <MaxShadowsAltitude Au="1" />
            <VolumetricsFlickerReductionDistance Km="100" />
""".Trim('\n'),
"""
<OrbitTransitionStartAltitude Km="600" /> <!-- Real Atmospheres: raised above the 290 km haze laminae (stock faded volumetrics out below the layers) -->
            <OrbitTransitionEndAltitude Km="900" />
            <MaxShadowsAltitude Au="1" />
            <VolumetricsFlickerReductionDistance Km="300" />
""".Trim('\n'),
            "raised above the 290 km haze laminae"
        ),
    };

    // ---------------- SolSystem(Dense).xml: Titan detached-haze layer ----------------
    // The STOCK Titan atmosphere is kept as-is (well-tuned; the 60/40
    // forward-lobe restructure attempt washed the haze into a white shell and
    // was reverted - phase experiments happen live in-game instead). This adds
    // only the Cassini-style stratified haze rims: the horizontal mask is
    // near-uniform (subtle banding + north polar hood, make_titan_haze.py);
    // the LAYERED limb strips come from the ShapeCurve's three vertical peaks
    // inside one 380-520 km shell. NOTE: Titan's <Clouds> block sits OUTSIDE
    // </Atmosphere> in the system files (unlike Astronomicals bodies) and a
    // second in-Atmosphere Clouds block is silently ignored - the layer must
    // be inserted into the EXISTING block, after TitanPlaceholderClouds.

    public const string TitanHazeMarker = "TitanDetachedHaze";
    public const string TitanCloudsAnchor = "<Layer Id=\"TitanPlaceholderClouds\">";

    public const string TitanHazeLayerTemplate =
"""


            <!-- Real Atmospheres: Titan detached haze - stratified upper-haze shell (layers via ShapeCurve peaks) -->
            <Layer Id="TitanDetachedHaze">
                <RotationSpeed X="0" Y="0" Z="-15" />
                <VolumetricCloud>
                    <!-- user's organic SE export (TitanLowerHazeSource), whitened via luminance->alpha -->
                    <Texture Id="TitanUpperHazeMaskVolumetric" Path="{ASSETS}\TitanUpperHazeMaskVolumetric.dds" Category="Terrain">
                        <IsVirtual>false</IsVirtual>
                        <Manifest>
                            <MaxSize>0</MaxSize>
                            <MipMaps>false</MipMaps>
                        </Manifest>
                    </Texture>
                    <Detail>
                        <DetailSelector Id="TitanUpperHazeMaskVolumetric"/>
                        <Texture Id="MarsDetail"/>
                        <Size Km="600" />
                    </Detail>
                    <Color R="0.45" G="0.55" B="0.72" />
                    <Raymarching>
                        <Step Scale="0.02">
                            <Size M="2000" />
                            <MaxSize M="30000" />
                        </Step>
                        <LightDistance M="20000" />
                        <LightSamples Value="3" />
                    </Raymarching>
                    <Noise>
                        <ScrollSpeed Value="20" />
                    </Noise>
                    <CloudType Name="DetachedHaze">
                        <StartAltitude M="290000" /> <!-- user-tuned live: matches Cassini limb photos -->
                        <Height M="2000" /> <!-- single thin lamina; Cassini resolves km-scale layers -->
                        <Density Value="0.000005" /> <!-- user-tuned -->
                        <NoiseScale M="250000" />
                        <EdgeSharpness Value="0.2" />
                        <MultipleScatteringBrightness Value="0.7" />
                        <!-- three ShapeCurve peaks = three stratified haze strips at the limb -->
                        <CloudShape InterpolateShapes="true">
                            <ShapeCurve>
                                <SplinePoint><Key Value="0.0"/><Value Value="0.0"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.1"/><Value Value="0.9"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.22"/><Value Value="0.15"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.4"/><Value Value="1.0"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.55"/><Value Value="0.2"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.72"/><Value Value="0.85"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.85"/><Value Value="0.1"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="1.0"/><Value Value="0.0"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                            </ShapeCurve>
                        </CloudShape>
                    </CloudType>
                </VolumetricCloud>
                <TwoDimensionalCloud>
                    <Texture Id="TitanUpperHazeMask2D" Path="{ASSETS}\TitanUpperHazeMask2D.png" Category="Terrain"/>
                    <Color R="0.55" G="0.65" B="0.8" />
                    <Lambertian Value="1.0" />
                </TwoDimensionalCloud>
                <MatchGroundColor Value="false" />
            </Layer>

            <!-- Real Atmospheres: lower haze lamina - the broken/stratified strips below the main
                 detached layer (user's SE export TitanUpperHazeSource, whitened) -->
            <Layer Id="TitanLowerHaze">
                <RotationSpeed X="0" Y="0" Z="-15" />
                <VolumetricCloud>
                    <Texture Id="TitanLowerHazeMaskVolumetric" Path="{ASSETS}\TitanLowerHazeMaskVolumetric.dds" Category="Terrain">
                        <IsVirtual>false</IsVirtual>
                        <Manifest>
                            <MaxSize>0</MaxSize>
                            <MipMaps>false</MipMaps>
                        </Manifest>
                    </Texture>
                    <Detail>
                        <DetailSelector Id="TitanLowerHazeMaskVolumetric"/>
                        <Texture Id="MarsDetail"/>
                        <Size Km="600" />
                    </Detail>
                    <Color R="0.45" G="0.55" B="0.72" />
                    <Raymarching>
                        <Step Scale="0.02">
                            <Size M="2000" />
                            <MaxSize M="30000" />
                        </Step>
                        <LightDistance M="20000" />
                        <LightSamples Value="3" />
                    </Raymarching>
                    <Noise>
                        <ScrollSpeed Value="20" />
                    </Noise>
                    <CloudType Name="LowerHaze">
                        <StartAltitude M="230000" /> <!-- user-tuned live -->
                        <Height M="2000" />
                        <Density Value="0.000005" /> <!-- user-tuned -->
                        <NoiseScale M="250000" />
                        <EdgeSharpness Value="0.2" />
                        <MultipleScatteringBrightness Value="0.7" />
                        <CloudShape InterpolateShapes="true">
                            <ShapeCurve>
                                <SplinePoint><Key Value="0.0"/><Value Value="0.0"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.1"/><Value Value="0.9"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.22"/><Value Value="0.15"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.4"/><Value Value="1.0"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.55"/><Value Value="0.2"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.72"/><Value Value="0.85"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.85"/><Value Value="0.1"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="1.0"/><Value Value="0.0"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                            </ShapeCurve>
                        </CloudShape>
                    </CloudType>
                </VolumetricCloud>
                <TwoDimensionalCloud>
                    <Texture Id="TitanLowerHazeMask2D" Path="{ASSETS}\TitanLowerHazeMask2D.png" Category="Terrain"/>
                    <Color R="0.55" G="0.65" B="0.8" />
                    <Lambertian Value="1.0" />
                </TwoDimensionalCloud>
                <MatchGroundColor Value="false" />
            </Layer>
""";

    // ---------------- SolSystem(Dense).xml: Triton + Pluto atmospheres ----------------
    // Neither body can carry an atmosphere as defined: Pluto is a
    // PlanetaryBody, Triton a bare MinorBody. AtmosphericBodyTemplate extends
    // PlanetaryBodyTemplate, so the shadow transform renames the element and
    // adds the Atmosphere block (Triton also gets the minimal PlanetaryBody
    // kit: MeshCollection Default + Color). Values are PURE derived physics -
    // no exposure compensation (user decision; KSA exposure differs from the
    // HTML app): beta = beta_Earth x n-ratio x sigma_N2, same T used for both
    // density and H. Mie is an epsilon placeholder until a haze pass; Ozone
    // zeroed + StartHeight 0 are required by AtmosphereReference.IsValid().
    //   Triton: P=1.5 Pa, T=38 K -> x1.15e-4 Earth; H=14.8 km (measured).
    //   Pluto:  P=1.0 Pa, T=40 K -> x7.3e-5 Earth; H=kT/mg=19.2 km.

    public const string PlutoBodyOpen = "<PlanetaryBody Id=\"Pluto\" Parent=\"Sol\">";
    public const string TritonBodyOpen = "<MinorBody Id=\"Triton\" Parent=\"Neptune\">";

    public const string PlutoAtmosphere =
"""
<Atmosphere>
            <Visual>
                <RayleighScattering>
                    <!-- Real Atmospheres: derived N2 Rayleigh, New Horizons P=1.0 Pa @ 40 K, no exposure comp -->
                    <Coefficients R="0.000000421" G="0.00000098" B="0.0000024" />
                    <ScaleHeight Km="19.2" />
                </RayleighScattering>
                <MieScattering>
                    <!-- NH blue tholin haze: fractal aggregates, Rayleigh-like blue spectrum but
                         STRONGLY forward-scattering (g~0.85) - the crescent blue ring; tau_B ~0.009 -->
                    <Coefficients R="0.00003" G="0.00007" B="0.00017" />
                    <ScaleHeight Km="30" /> <!-- user-tuned to NH photos -->
                    <PhaseFunctionAsymmetry X="0.85" Y="0.85" Z="0.85" /> <!-- freely tunable; no longer a band flag -->
                    <AbsorptionMultiplier Value="1.5" />
                </MieScattering>
                <Ozone>
                    <!-- No ozone on Pluto. Haze bands are armed by body radius (RaBodyHasHaze in
                         AtmosphereData.glsl), not by this slot, so it stays clean. -->
                    <Coefficients R="0" G="0" B="0" />
                    <Altitude Km="0" />
                    <Extent Km="0" />
                </Ozone>
                <StartHeight Km="0" />
            </Visual>
            <Physical>
                <SeaLevelPressure Atm="0.00000987"/>
                <SeaLevelDensity KgPerM3="0.0000842"/>
                <ScaleHeight Km="19.2"/>
            </Physical>
        </Atmosphere>
""";

    public const string TritonAtmosphere =
"""
<Atmosphere>
            <Visual>
                <RayleighScattering>
                    <!-- Real Atmospheres: derived N2 Rayleigh, Voyager P=1.5 Pa @ 38 K, no exposure comp -->
                    <Coefficients R="0.000000664" G="0.00000155" B="0.00000379" />
                    <ScaleHeight Km="14.8" />
                </RayleighScattering>
                <MieScattering>
                    <!-- Voyager photochemical haze: smaller monomers than Pluto's aggregates ->
                         milder forward lobe (g~0.55), thin low deck; tau_B ~0.004 -->
                    <Coefficients R="0.0001" G="0.0002" B="0.0004" />
                    <ScaleHeight Km="20" /> <!-- user-tuned -->
                    <PhaseFunctionAsymmetry X="0.55" Y="0.55" Z="0.55" />
                    <AbsorptionMultiplier Value="1.3" />
                </MieScattering>
                <Ozone>
                    <Coefficients R="0" G="0" B="0" />
                    <Altitude Km="0" />
                    <Extent Km="0" />
                </Ozone>
                <StartHeight Km="0" />
            </Visual>
            <Physical>
                <SeaLevelPressure Atm="0.0000148"/>
                <SeaLevelDensity KgPerM3="0.000133"/>
                <ScaleHeight Km="14.8"/>
            </Physical>
        </Atmosphere>
""";

    public const string TritonBodyKit =
"""
<MeshCollection Id="Default"/>
        <Color R="0.87" G="0.84" B="0.8" />
""";

    // The TEMPORARY Pluto/Triton preview surfaces (Diffuse/Normal/Height) + Pluto's
    // rotation moved to the separate "Real Surfaces" mod (RealSurfaces/Payloads.cs);
    // Real Atmospheres keeps only their ATMOSPHERES. Real Surfaces inserts the Triton
    // surface right before this TritonBodyKit's <Color>, and the Pluto surface +
    // rotation at Pluto's <MeanRadius>, via the shared system-XML shadow.

    // Triton condensate clouds (Voyager 2): patchy N2-ice wisps at ~1-3.5 km,
    // southern-weighted, from make_triton_clouds.py. Own Clouds block (Triton
    // had none) with transitions sized for a 1353 km body with 3.5 km clouds -
    // NOT copied from a big body (the Titan transition lesson, applied early).

    public const string TritonClouds =
"""
<Clouds>
            <OrbitTransitionStartAltitude Km="100" />
            <OrbitTransitionEndAltitude Km="180" />
            <MaxShadowsAltitude Au="1" />
            <VolumetricsFlickerReductionDistance Km="50" />

            <Layer Id="TritonClouds">
                <RotationSpeed X="0" Y="0" Z="-20" />
                <VolumetricCloud>
                    <Texture Id="TritonCloudsMaskVolumetric" Path="{ASSETS}\TritonCloudsMaskVolumetric.dds" Category="Terrain">
                        <IsVirtual>false</IsVirtual>
                        <Manifest>
                            <MaxSize>0</MaxSize>
                            <MipMaps>false</MipMaps>
                        </Manifest>
                    </Texture>
                    <Detail>
                        <DetailSelector Id="TritonCloudsMaskVolumetric"/>
                        <Texture Id="MarsDetail"/>
                        <Size Km="300" />
                    </Detail>
                    <Color R="0.85" G="0.88" B="0.92" />
                    <Raymarching>
                        <Step Scale="0.02">
                            <Size M="400" />
                            <MaxSize M="6000" />
                        </Step>
                        <LightDistance M="5000" />
                        <LightSamples Value="3" />
                    </Raymarching>
                    <Noise>
                        <ScrollSpeed Value="15" />
                    </Noise>
                    <CloudType Name="CondensateWisps">
                        <StartAltitude M="4000" /> <!-- user-tuned -->
                        <Height M="2500" />
                        <Density Value="0.000005" /> <!-- user-tuned: wispy -->
                        <NoiseScale M="80000" />
                        <EdgeSharpness Value="0.4" />
                        <MultipleScatteringBrightness Value="0.7" />
                        <CloudShape InterpolateShapes="true">
                            <ShapeCurve>
                                <SplinePoint><Key Value="0.0"/><Value Value="0.0"/><InTangent Value="2.5"/><OutTangent Value="2.5"/></SplinePoint>
                                <SplinePoint><Key Value="0.25"/><Value Value="1.0"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.75"/><Value Value="0.9"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="1.0"/><Value Value="0.0"/><InTangent Value="-2.5"/><OutTangent Value="-2.5"/></SplinePoint>
                            </ShapeCurve>
                        </CloudShape>
                    </CloudType>
                </VolumetricCloud>
                <TwoDimensionalCloud>
                    <Texture Id="TritonCloudsMask2D" Path="{ASSETS}\TritonCloudsMask2D.png" Category="Terrain"/>
                    <Color R="0.85" G="0.88" B="0.92" />
                    <Lambertian Value="0.9" /> <!-- user-tuned -->
                </TwoDimensionalCloud>
                <MatchGroundColor Value="false" />
            </Layer>
        </Clouds>
""";

    // ---------------- Astronomicals.xml: MarsCirrus layer ----------------
    // Inserted after the MarsDustStorms layer's closing </Layer>. {ASSETS} is
    // replaced at runtime with the absolute path of the mod's assets folder
    // (KSA.Mod.GetPath = Path.Combine, which passes rooted paths through
    // untouched, so absolute Paths resolve into the mod folder, no elevation).

    public const string CirrusMarker = "MarsCirrus";

    public const string CirrusLayerTemplate =
"""

            <!-- MarsCirrus: high-altitude water-ice clouds (polar hoods + equatorial wisps).
                 PATCH - injected in-memory by the Real Atmospheres StarMap mod -->
            <Layer Id="MarsCirrus">
                <RotationSpeed X="0" Y="0" Z="-25" />
                <VolumetricCloud>
                    <Texture Id="MarsCirrusMaskVolumetric" Path="{ASSETS}\MarsCirrusMaskVolumetric.dds" Category="Terrain">
                        <IsVirtual>false</IsVirtual>
                        <Manifest>
                            <MaxSize>0</MaxSize>
                            <MipMaps>false</MipMaps>
                        </Manifest>
                    </Texture>
                    <Detail>
                        <DetailSelector Id="MarsCirrusMaskVolumetric"/>
                        <Texture Id="MarsDetail"/>
                        <Size Km="400" />
                    </Detail>
                    <Color R="0.392" G="0.412" B="0.431" /> <!-- user-tuned in-game (100/105/110): final -->
                    <Raymarching>
                        <Step Scale="0.02">
                            <Size M="500" />
                            <MaxSize M="2000" />
                        </Step>
                        <LightDistance M="10000" />
                        <LightSamples Value="3" />
                    </Raymarching>
                    <Noise>
                        <ScrollSpeed Value="30" />
                    </Noise>
                    <CloudType Name="Cirrus">
                        <StartAltitude M="22000" />
                        <Height M="7000" />
                        <Density Value="0.00005" />
                        <NoiseScale M="120000" />
                        <EdgeSharpness Value="0.3" />
                        <MultipleScatteringBrightness Value="0.7" />
                        <!-- ShapeCurve is REQUIRED for volumetrics: dimensionalProfile =
                             coverage + shapeGradient - 1, so no curve -> never renders -->
                        <CloudShape InterpolateShapes="true">
                            <ShapeCurve>
                                <SplinePoint><Key Value="0.0"/><Value Value="0.0"/><InTangent Value="2.5"/><OutTangent Value="2.5"/></SplinePoint>
                                <SplinePoint><Key Value="0.25"/><Value Value="1.0"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="0.75"/><Value Value="0.9"/><InTangent Value="0.0"/><OutTangent Value="0.0"/></SplinePoint>
                                <SplinePoint><Key Value="1.0"/><Value Value="0.0"/><InTangent Value="-2.5"/><OutTangent Value="-2.5"/></SplinePoint>
                            </ShapeCurve>
                        </CloudShape>
                    </CloudType>
                </VolumetricCloud>
                <TwoDimensionalCloud>
                    <Texture Id="MarsCirrusMask2D" Path="{ASSETS}\MarsCirrusMask2D.png" Category="Terrain"/>
                    <Color R="0.95" G="0.98" B="1.0" />
                    <Lambertian Value="0.9" />
                </TwoDimensionalCloud>
                <MatchGroundColor Value="false" />
            </Layer>
""";

    // ---------------- AtmosphereFunctions.glsl: Level-1 Mie dust phase ----------------
    // The span from the stock Cornette-Shanks comment through the end of
    // MiePhaseFunction is replaced with: bhmie 3-lobe HG fit + soft knee
    // compression, then a sentinel-dispatching MiePhaseFunction.
    // Constants generated by mars_dust_mie.py (in this folder) - keep in sync
    // with mars_dust_phase.glsl beside it if the fit is ever re-run.

    public const string StockPhaseStart = "// Cornette-Shanks aproximation";
    public const string PhaseFnSignature = "vec3 MiePhaseFunction(float cosLight, vec3 asymmetry)";
    public const string PhaseMarker = "MarsDustPhaseFunction";

    public static readonly string PatchedPhaseBlock =
"""
// ---- Martian dust phase function (Mie theory, Schneegans et al. 2024) ----
// 3-lobe HG fit per RGB channel of bhmie output for feldspar+hematite dust
// (bimodal log-normal sizes). Generated by mars_dust_mie.py - do not hand-edit.
vec3 MarsDustHG(float g_scale, vec3 g, float cosLight)
{
    vec3 gg = g * g;
    return (vec3(1.0) - gg) / (4.0 * PI * pow(vec3(1.0) + gg - 2.0 * g * cosLight, vec3(1.5))) * g_scale;
}

// Soft radiance compression of the forward spike: KSA has no exposure control, so
// the physically-correct peak (P~10/sr near 0 deg) washes the sunset out. This is
// "exposure applied in the medium": identity for P << MARS_DUST_PHASE_KNEE, smooth
// compression above it. Only the innermost few degrees around the sun are affected.
#define MARS_DUST_PHASE_KNEE 8.0

vec3 MarsDustPhaseFunction(float cosLight)
{
    vec3 p = vec3(0.0);
    p += vec3(0.1726, 0.4362, 0.6934) * MarsDustHG(1.0, vec3(0.9050, 0.8900, 0.9000), cosLight);
    p += vec3(0.7728, 0.5176, 0.2683) * MarsDustHG(1.0, vec3(0.7100, 0.6500, 0.5000), cosLight);
    p += vec3(0.0546, 0.0462, 0.0384) * MarsDustHG(1.0, vec3(-0.5000, -0.5000, -0.5000), cosLight);
    return p; // EXPERIMENT: raw phase, knee compression removed (was: p / (1 + p/MARS_DUST_PHASE_KNEE))
}
// ---- end Martian dust phase function ----

// Cornette-Shanks aproximation for mie phase function
// With a different asymmetry per-component
// PATCHED (Level-1 Mie dust): asymmetry.x < -0.5 selects the Martian dust phase above.
vec3 MiePhaseFunction(float cosLight, vec3 asymmetry)
{
    if (asymmetry.x < -0.5)
        return MarsDustPhaseFunction(cosLight);

    vec3 asymmetrySq = asymmetry * asymmetry;

    vec3 denom = 1.0 + asymmetrySq - 2.0 * asymmetry * cosLight;
    float numerator = 1.0 + cosLight * cosLight;

    return 1.5 / (4.0 * PI) * (1.0 - asymmetrySq) * pow(denom, vec3(-1.5)) * numerator / (2.0 + asymmetrySq);
}
""".Trim('\n');

    // ---------------- Double-exponential Mars dust density (Schneegans 2024) ----------------
    // Compact capped dust layer replacing the single-exp falloff, gated IN-SHADER on
    // the Mars sentinel (mieAsymmetry.x < -0.5) so only Mars uses it. Tames the
    // sunset grazing-path pileup physically (vs the phase knee, now removed for L2).
    // Helper goes in AtmosphereData.glsl; the 6 density sites across 4 files are
    // swapped to call it (3 DENS_A + 3 DENS_B).
    public const string MarsDensityMarker = "AtmosphereDensityFalloff";
    public const string AtmosphereDataEndif = "#endif // ATMOSPHERE_DATA_GLSL_INCLUDED";

    public const string MarsDensityHelper =
"""
// ---- Mars dust density (double-exponential compact layer, Schneegans et al. 2024) ----
// Hard-ceilinged dust confined to a boundary layer; gated on the Mars sentinel.

// 1D hash + value noise (Real Atmospheres; prefixed to avoid include collisions) - used
// to salt the Pluto haze bands so they aren't a perfectly regular wave.
float raHash11(float p)
{
    p = fract(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}
float raVNoise(float x)
{
    float i = floor(x);
    float f = fract(x);
    float u = f * f * (3.0 - 2.0 * f);
    return mix(raHash11(i), raHash11(i + 1.0), u);
}
// 3D hash + value noise - drives the HORIZONTAL variation (undulation + break-up) from the
// sample's planet-relative direction, when the per-pixel march supplies a real one.
float raHash13(vec3 p)
{
    p = fract(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return fract((p.x + p.y) * p.z);
}
float raVNoise3(vec3 x)
{
    vec3 i = floor(x);
    vec3 f = fract(x);
    vec3 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(mix(raHash13(i), raHash13(i + vec3(1,0,0)), u.x),
                   mix(raHash13(i + vec3(0,1,0)), raHash13(i + vec3(1,1,0)), u.x), u.y),
               mix(mix(raHash13(i + vec3(0,0,1)), raHash13(i + vec3(1,0,1)), u.x),
                   mix(raHash13(i + vec3(0,1,1)), raHash13(i + vec3(1,1,1)), u.x), u.y), u.z);
}

// Pluto haze-band multiplier for the Mie density. horizDir = the sample's planet-relative unit
// direction: pass vec3(0.0) for the vertical-only (LUT / azimuthally-symmetric) form, or the
// REAL direction (from the per-pixel march) to add azimuthal undulation + per-band break-up.
// Shared by both paths so the vertical structure matches exactly.
//
// ---- TUNING GUIDE ----
//   band COUNT:       seaLevelAltitude / 12000.0   (nominal spacing in m; smaller = more layers.
//                     ~220 km haze column => 12000 ~18 layers, 11000 ~20)
//   more CONTINUOUS   flatCancel smoothstep(0.38,0.58) -> lower both / replace with 1.0 to disable;
//   (vs patchy):      coverage smoothstep first arg lower; occ floor 0.3 -> raise toward 1.0
//   layer SHARPNESS:  widthExp mix(0.5,3.0) (raise => thinner streaks); leading 1.0 in return (contrast)
//   brightness var:   strength 0.3 + 1.3*...
//   irregularity:     rSpacing 0.6+0.8 ; buWarp 0.5/0.2 ; undulation 1.4*hAlt
//   animation:        rotAngle *1.3e-5 (spin) ; evolveT *3.0e-5 (morph) ; sT = evolveT*0.5 (layout)
//   patch sizes:      the horizDir * N multipliers (bigger N = finer horizontal features)
float PlutoHazeBandFactor(float seaLevelAltitude, vec3 horizDir, float evolveT, float bottomRadius)
{
    // PER-BODY: Titan wants much wider spacing + wider, more-varied layer thickness than Pluto.
    bool isTitan = bottomRadius > 2000000.0;
    float spacing = isTitan ? 40000.0 : 5500.0;           // nominal band spacing (m)
    float wLo = isTitan ? 0.3 : 0.5;                       // band-width range: low exp = wide/cloudy,
    float wHi = isTitan ? 1.6 : 3.0;                       // high exp = thin streak; wider range = varied depths
    float warp = isTitan ? 0.25 : 1.0;                    // band-POSITION warp (undulation + regional spacing/
                                                          // phase). LOW on Titan so the atmosphere edge stays
                                                          // regular (varied layer thickness, smooth outline).
    float bandAmp = isTitan ? 0.8 : 0.6;                  // OPACITY of OUR haze layers (the band modulation)
                                                          // on top of the base Mie/Rayleigh. Lower = subtler
                                                          // layers, 0 = none. Doesn't touch the base atmosphere.
    // bandCurve = 1.0 = evenly-spaced layers (Pluto's real hazes ARE even, and the periodic
    // cosine ridge below gives the natural haze/gap rhythm). Lower it (<1) only if a body should
    // read progressively SPARSER with altitude; `spacing` is then the near-surface gap.
    float bandCurve = 1.0;
    float bu = pow(max(seaLevelAltitude, 0.0) / spacing, bandCurve);
    bool horiz = dot(horizDir, horizDir) > 0.5;
    float sT = evolveT * 0.5;   // the band LAYOUT evolves too (slower than coverage) so the clouds
                                // don't just slide across a static grid of bands.

    // REGIONAL LAYOUT: band spacing + phase vary slowly across the limb, so different
    // longitudes get genuinely different band altitudes/spacing - not one fixed layout warped.
    float rSpacing = horiz ? mix(1.0, 0.6 + 0.8 * raVNoise3(horizDir * 2.5 + vec3(0.0, 0.0, sT)), warp) : 1.0;
    float rPhase   = horiz ? warp * raVNoise3(horizDir * 2.9 + vec3(0.0, 0.0, sT) + 11.0) : 0.0;
    float hAlt     = horiz ? warp * (raVNoise3(horizDir * 9.0 + vec3(0.0, 0.0, sT)) - 0.5) : 0.0;

    // IRREGULAR SPACING: domain-warp the altitude (2 octaves, seeded per region) so bands
    // aren't evenly spaced and the spacing pattern differs region to region.
    float buWarp = warp * (0.2 * (raVNoise(bu * 0.4 + rPhase * 5.0 + sT) - 0.5) + 0.05 * (raVNoise(bu * 1.5 + rPhase * 5.0 + sT) - 0.5));
    float phase = (bu + buWarp) / rSpacing + rPhase + 1.4 * hAlt;
    float idx = floor(phase + 0.5);                          // band index (steps in the gaps)
    float ridgeRaw = 0.5 + 0.5 * cos(6.28318530718 * phase);  // 0..1, =0 between bands (no seams)
    // BAND WIDTH varies per band + region: high exponent = thin streak, low = wide soft cloudy
    // haze (and adjacent wide bands merge into blobs).
    float widthExp = horiz ? mix(wLo, wHi, raVNoise3(horizDir * 3.5 + vec3(idx * 5.1, 0.0, sT))) : mix(wLo, wHi, 0.5);
    float ridge = pow(ridgeRaw, widthExp);

    // PER-BAND density (varies smoothly across the limb and per band).
    float strength = horiz ? 0.3 + 1.3 * raVNoise3(horizDir * 4.0 + vec3(0.0, 0.0, idx * 1.7 + sT))
                           : 0.55 + 0.9 * raHash11(idx * 1.7 + 0.5);

    // CLOUD-LIKE COVERAGE: cancel MOST of the layering in coherent patches, so the bands read
    // as a broken cloud deck, not near-total coverage. 'coverage' is idx-independent (clears
    // whole regions -> real gaps through the line of sight); 'bandPresence' varies per band.
    // Product of the two -> aggressive cancellation. Raise the smoothstep windows to cancel more.
    float occ;
    if (horiz)
    {
        float coverage     = smoothstep(0.40, 0.62, raVNoise3(horizDir * 3.0 + vec3(0.0, 0.0, evolveT) + 7.0));
        float bandPresence = smoothstep(0.42, 0.66, raVNoise3(horizDir * 5.5 + vec3(idx * 2.3, 0.0, evolveT)));
        float flatCancel   = smoothstep(0.28, 0.44, raVNoise3(horizDir * 7.0 + vec3(0.0, idx * 3.7, evolveT))); // Some cancel but ROUNDED/tapered ends, not square; evolveT morphs it
        // regional thinning keeps a 30% residual floor; the flat per-band cancel drops bands outright
        occ = (0.3 + 0.7 * coverage * bandPresence) * flatCancel;
    }
    else occ = step(0.18, raHash11(idx * 3.1 + 9.2));

    // Titan: confine bands to ABOVE the low cloud deck (~22 km) - physically right (its stratified
    // hazes are high; the lower atmosphere is a uniform thick haze) AND it keeps the bands off the
    // 2D cloud's terminator, where marched-vs-LUT atmosphere differences were seaming.
    float altFade = isTitan ? smoothstep(28000.0, 55000.0, seaLevelAltitude) : 1.0;
    return 1.0 + bandAmp * altFade * ridge * strength * occ;
}

// Which bodies carry haze bands, keyed on the (unique, per-body) radius in metres. This is a
// slot no body "uses up", so it works even on bodies that use their ozone (unlike the old
// ozone-Altitude sentinel). bottomRadius = MeanRadius + Visual StartHeight; the Real Surfaces
// mod lowers StartHeight below each body's terrain floor (so the atmosphere covers the low
// topography), shifting bottomRadius by up to ~a few km. The 20 km tolerance absorbs that and
// still uniquely identifies these three (their radii are >150 km apart). Add bodies here;
// params live in PlutoHazeBandFactor (branch on bottomRadius when a second body differs).
bool RaBodyHasHaze(float bottomRadius)
{
    return abs(bottomRadius - 1188300.0) < 20000.0    // Pluto  (r = 1188.3 km)
        || abs(bottomRadius - 2575500.0) < 20000.0    // Titan  (r = 2575.5 km)
        || abs(bottomRadius - 1353400.0) < 20000.0;   // Triton (r = 1353.4 km, thin -> Pluto-like path)
}

// bodyRadius identifies the body (see RaBodyHasHaze). Haze params are hardcoded per body, so
// this generalises to any body (incl. ones using ozone) without a free push-constant slot.
vec2 AtmosphereDensityFalloff(float seaLevelAltitude, float rayleighScaleHeight, float mieScaleHeight, vec3 mieAsymmetry, float bodyRadius)
{
    // compact 4 km + extended 20 km layers, expressed as fractions of the (working)
    // stock mieScaleHeight (=9 km) so this is UNIT-AGNOSTIC - seaLevelAltitude is in
    // METERS in this build, so a literal /4.0 capped dust to a 4-metre layer (bug).
    float mie = mieAsymmetry.x < -0.5
        ? 0.75 * exp(1.0 - exp(seaLevelAltitude / (0.44 * mieScaleHeight))) + 0.25 * exp(1.0 - exp(seaLevelAltitude / (2.2 * mieScaleHeight)))
        : exp(-seaLevelAltitude / mieScaleHeight);
    // Pluto haze bands (DORMANT unless armed): gravity-wave stratified layers baked into the
    // haze density. Armed by bandSentinel < 0 (fed the ozone Altitude - a field Pluto doesn't
    // use, so this is DECOUPLED from the Mie asymmetry; tuning forward-scatter can't trigger
    // it, and arming it can't disturb the phase function). Modulates the Mie (haze) density
    // into denser shells ~12 km apart, so the forward-scattered crescent shows NH-style
    // stacked layers. RELATIVE (x) modulation -> rides the exp falloff, so upper bands fade
    // naturally with the haze. Because every LUT integrates this same function, the bands
    // stay consistent across single + multiple scattering + transmittance.
    // Salted (hash noise, altitude-only) three ways: jittered spacing, ~18% dropped bands,
    // per-band density. NOTE: HORIZONTAL (azimuthal) variation is IMPOSSIBLE here - the
    // visible atmosphere is drawn from the sky-view LUT (SkyLut.comp), parameterized only
    // by (viewZenithCosAngle, lightViewAngle) with NO azimuth axis, so it hard-assumes
    // azimuthal symmetry. Bands can only be concentric. Horizontal break-up would need an
    // engine change (per-pixel march or an azimuth LUT dim) or a 2D/volumetric cloud overlay.
    float ray = exp(-seaLevelAltitude / rayleighScaleHeight);
    // Bake bands into the LUT density ONLY for thin atmospheres (Pluto = Mie, its forward haze).
    // For thick Titan this put bands into BOTH the sky-view and aerial LUTs, which resolve the
    // altitude structure at different resolutions -> a brightness SEAM at the sky/aerial
    // (geometry-edge) boundary. Titan's bands come from the per-pixel march instead (blended high
    // in the halo), so its LUTs stay stock and continuous across that boundary.
    if (RaBodyHasHaze(bodyRadius) && bodyRadius < 2000000.0)
    {
        mie *= PlutoHazeBandFactor(seaLevelAltitude, vec3(0.0), 0.0, bodyRadius);
    }
    return vec2(ray, mie);
}
// ---- end Mars dust density ----

""";

    // 5th arg = bottomRadius (the per-body haze identifier; see RaBodyHasHaze). atmosphereData
    // is in scope at every site.
    public const string DensAStock = "exp(-seaLevelAltitude / vec2(atmosphereData.rayleighCoefficientsAndScaleHeight.w, atmosphereData.mieCoefficientsAndScaleHeight.w))";
    public const string DensAPatch = "AtmosphereDensityFalloff(seaLevelAltitude, atmosphereData.rayleighCoefficientsAndScaleHeight.w, atmosphereData.mieCoefficientsAndScaleHeight.w, atmosphereData.miePhaseFunctionAsymmetry.xyz, atmosphereData.bottomRadius)";
    public const string DensBStock = "exp(-seaLevelAltitude / vec2(rayleighScaleHeight, mieScaleHeight))";
    public const string DensBPatch = "AtmosphereDensityFalloff(seaLevelAltitude, rayleighScaleHeight, mieScaleHeight, atmosphereData.miePhaseFunctionAsymmetry.xyz, atmosphereData.bottomRadius)";

    // (file, stock, patch, expected-count) for the density sites
    public static readonly (string File, string Stock, string Patch, int Count)[] MarsDensitySites =
    {
        ("Atmosphere/AtmosphereFunctions.glsl", DensAStock, DensAPatch, 2),
        ("Atmosphere/AerialPerspectiveLut.comp", DensAStock, DensAPatch, 1),
        ("Atmosphere/Atmosphere.comp", DensBStock, DensBPatch, 1),
        ("Atmosphere/Godrays.glsl", DensBStock, DensBPatch, 2),
    };

    // ---------------- Atmosphere.comp: per-pixel horizontal haze-band march (Pluto) ----------------
    // The visible atmosphere is drawn from the azimuthally-symmetric sky-view LUT, which cannot
    // hold horizontal structure. For a flagged (haze-band) body we instead march the real view
    // ray per pixel and sample a 3D-modulated Mie density, so the bands undulate/break up. Mirrors
    // the stock RaymarchAtmosphere integration exactly (transmittance LUT + MS LUT), so it matches
    // stock when the horizontal term is off. Only the flagged body's SKY pixels take this path.

    public const string HazeBandMarchFunc =
"""
// ---- Real Atmospheres: per-pixel horizontal haze-band march (Pluto) ----
vec3 raRotateAxis(vec3 v, vec3 axis, float a)   // Rodrigues; axis must be normalized
{
    float c = cos(a), s = sin(a);
    return v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
}
// NOTE: adapted for KSA v2026.8.3 atmosphere refactor - the LUTs are now per-planet
// sampler2DArrays indexed by lutLayer (was sampler2D), and per-planet data moved from
// the removed atmospherePushConsts to the atmosphereDataUbo. lutLayer is threaded in.
void RaymarchAtmosphereHorizontal(vec3 worldViewDir, float startDistance, float distanceToTravel,
    vec3 planetPosition, vec3 sunPosition, float planetRadius, AtmosphereData atmosphereData,
    float lutLayer, int iterations, out vec3 inscatter, out vec3 transmittance)
{
    vec3 currentPosition = worldViewDir * startDistance;
    float stepSize = distanceToTravel / iterations;
    vec2 transmittanceLutDimensions = textureSize(transmittanceLut, 0).xy;

    // ANIMATION: hardcoded rates (time is sim-time, so both auto-speed-up under time-warp).
    // DEV live-tuning: temporarily multiply animSpeed by atmosphereData.ozoneExtent and set the
    // Ozone Extent slider in the debug editor (works only while the ozone slot is otherwise unused).
    // Spin axis per body (VERIFY frames in-game). Titan: ~upright (tidally locked to Saturn,
    // low obliquity). Pluto: ecliptic estimate of its steep IAU pole.
    vec3 spinAxis = atmosphereData.bottomRadius > 2000000.0
        ? normalize(vec3(0.0, 1.0, 0.05))
        : normalize(vec3(-0.68, 0.62, -0.39));
    // SIM-LINKED time: the renderer injects elapsed SIM seconds into ozoneExtent for Pluto
    // (freezes on pause, scales with time-warp). Falls back to wall-clock if not injected.
    float animT = atmosphereData.ozoneExtent > 0.5 ? atmosphereData.ozoneExtent : global.camera.time;
    // Realistic high-altitude rotation: Pluto spins once per 6.39 d (omega ~1.14e-5 rad/s);
    // 1.3e-5 is ~1.14x (near co-rotation, a hair of super-rotation). Sim-linked (this is the 1x rate).
    float rotAngle = mod(animT * 1.3e-5, 6.28318530718);
    float evolveT  = animT * 3.0e-5;                           // cloud evolution ~2.3x rotation (haze morphs over hours, not seconds)

    vec3 totalTransmittance = vec3(1.0);
    vec3 rayleighInscatter = vec3(0.0);
    vec3 mieInscatter = vec3(0.0);
    inscatter = vec3(0.0);

    vec3 directionToSun = normalize(sunPosition - planetPosition);
    vec3 rayleighAbsorption = atmosphereData.rayleighCoefficientsAndScaleHeight.xyz;
    vec3 mieAbsorption = atmosphereData.mieCoefficientsAndScaleHeight.xyz * atmosphereData.mieAbsorptionMultiplier;
    float raRH = atmosphereData.rayleighCoefficientsAndScaleHeight.w;
    float raMH = atmosphereData.mieCoefficientsAndScaleHeight.w;

    for (int i = 0; i < iterations; i++)
    {
        currentPosition += stepSize * worldViewDir;
        vec3 planetRelativePosition = currentPosition - planetPosition;
        float distanceFromPlanetCenter = length(planetRelativePosition);
        vec3 zenithDirection = planetRelativePosition / distanceFromPlanetCenter;

        float seaLevelAltitude = max(distanceFromPlanetCenter - atmosphereData.bottomRadius, 0.0);
        vec2 densityFalloff = exp(-seaLevelAltitude / vec2(raRH, raMH));
        vec3 hazeDir = raRotateAxis(zenithDirection, spinAxis, rotAngle);           // rotate the haze field over time
        float band = PlutoHazeBandFactor(seaLevelAltitude, hazeDir, evolveT, atmosphereData.bottomRadius); // vertical + horizontal + time
        if (atmosphereData.bottomRadius > 2000000.0)
            densityFalloff.x *= band;   // Titan etc.: Rayleigh is the bright bulk haze
        else
            densityFalloff.y *= band;   // Pluto: Mie is the forward haze
        float ozoneDensity = saturate(1.0 - abs(seaLevelAltitude - atmosphereData.ozoneCoefficientsAndAltitude.w) / atmosphereData.ozoneExtent);
        vec3 localAbsorption = rayleighAbsorption * densityFalloff.x + mieAbsorption * densityFalloff.y + atmosphereData.ozoneCoefficientsAndAltitude.xyz * ozoneDensity;
        vec3 stepTransmittance = exp(-localAbsorption * stepSize);

        float sunCosZenith = dot(zenithDirection, directionToSun);
        vec3 lightReceived = GetTransmittanceFromLut(planetRadius, atmosphereData.bottomRadius, atmosphereData.topRadius,
                                distanceFromPlanetCenter, sunCosZenith, transmittanceLut, lutLayer, transmittanceLutDimensions);

        vec3 integrationWeight = totalTransmittance * (vec3(1.0) - stepTransmittance) / localAbsorption;
        vec3 currentRayleighInscatter = densityFalloff.x * atmosphereData.rayleighCoefficientsAndScaleHeight.xyz;
        vec3 currentMieInscatter      = densityFalloff.y * atmosphereData.mieCoefficientsAndScaleHeight.xyz;

        vec2 msCoords = GetMultiScatteringLutCoordsFromPhysicalParameters(sunCosZenith,
                                seaLevelAltitude, atmosphereData.bottomRadius, atmosphereData.topRadius);
        vec3 multipleScatteringLight = textureLod(multipleScatteringLut, vec3(msCoords, lutLayer), 0.0).rgb;
        inscatter += integrationWeight * multipleScatteringLight * (currentRayleighInscatter + currentMieInscatter);

        integrationWeight *= lightReceived;
        rayleighInscatter += integrationWeight * currentRayleighInscatter;
        mieInscatter      += integrationWeight * currentMieInscatter;
        totalTransmittance *= stepTransmittance;
    }

    float dotViewDirSunDir = dot(worldViewDir, normalize(sunPosition));
    vec3 miePhase = MiePhaseFunction(dotViewDirSunDir, atmosphereData.miePhaseFunctionAsymmetry.xyz);
    float rayleighPhase = RayleighPhaseFunction(dotViewDirSunDir);
    inscatter += rayleighInscatter * rayleighPhase + mieInscatter * miePhase;
    transmittance = totalTransmittance;
}
// ---- end horizontal haze-band march ----
""";

    // insert the march function before main()
    public const string HazeBandMarchAnchor =
        "void main()\n{\n    ivec2 dim = imageSize(resultImage);";

    // stock sky-color path (only reached for sky pixels; the crescent limb)
    public static readonly string HazeBandSkyStock =
"""
    if (!sampleAerialPerspective)
    {
        GetSkyColorFromLuts(worldViewDir, planetPosition, global.lighting.sunPosition.xyz, atmosphereData.bottomRadius,
            atmosphereData.topRadius, skyColorRGBTransmittanceRLut, skyColorTransmittanceGBLut, inscatter, transmittance);
    }
""".Trim('\n');

    // flagged (ozone Altitude < 0) body -> per-pixel horizontal march; else stock LUT
    public static readonly string HazeBandSkyPatch =
"""
    if (!sampleAerialPerspective)
    {
        GetSkyColorFromLuts(worldViewDir, planetPosition, global.lighting.sunPosition.xyz, atmosphereData.bottomRadius,
            atmosphereData.topRadius, skyColorRGBTransmittanceRLut, skyColorTransmittanceGBLut, inscatter, transmittance);
        if (RaBodyHasHaze(atmosphereData.bottomRadius))
        {
            // Base above = the sky LUT, which is continuous with the aerial LUT at the disk edge
            // (so no seam there). Blend in the per-pixel banded march ONLY for rays grazing high in
            // the halo (tangent altitude above the band region); rays grazing near the surface keep
            // the LUT, hiding the march-vs-LUT brightness step that was seaming at the geometry edge.
            float rayT = max(dot(planetPosition, worldViewDir), 0.0);
            float tangentAlt = length(planetPosition - worldViewDir * rayT) - atmosphereData.bottomRadius;
            float bandStart = atmosphereData.bottomRadius > 2000000.0 ? 30000.0 : 3000.0;  // Titan high, Pluto low
            float bandRange = atmosphereData.bottomRadius > 2000000.0 ? 110000.0 : 25000.0; // WIDE on Titan: spreads any
                                                                                            // base march-vs-LUT difference out so it can't read as a line
            float wMarch = smoothstep(bandStart, bandStart + bandRange, tangentAlt);
            if (wMarch > 0.002)
            {
                float raBandDist = inAtmosphereDistance;
                if (planetIntersections.x >= 0.0)
                    raBandDist = min(startDistance + inAtmosphereDistance, planetIntersections.x) - startDistance;
                vec3 raMInscatter, raMTransmittance;
                RaymarchAtmosphereHorizontal(worldViewDir, startDistance, raBandDist, planetPosition,
                    global.lighting.sunPosition.xyz, atmosphereDataUbo.planetRadius, atmosphereData,
                    float(atmosphereDataUbo.atmosphereLutLayer), LUT_RAYMARCHING_ITERATIONS, raMInscatter, raMTransmittance);
                inscatter = mix(inscatter, raMInscatter, wMarch);
                // keep the LUT transmittance (don't mix) - avoids a transmittance step at the blend edge
            }
        }
    }
""".Trim('\n');


    // ---------------- 2DCloud.comp: terminator fade ----------------

    public const string TerminatorAnchor =
        "color.rgb *= mix(1.0, lambertian, uboCloudLayer.twoDimensionalCloudLambertian);";

    public const string TerminatorMarker = "2D cloud terminator fade";

    public static readonly string TerminatorFade = TerminatorAnchor + "\n" +
"""

        // PATCHED (2D cloud terminator fade): decouple the day/night rolloff from the
        // Lambertian blend. The transmittance LUT's planet-shadow cutoff is a knife
        // edge, so an unshaded (Lambertian~0) layer snaps to black at the terminator;
        // fade smoothly across ~11 deg of solar zenith instead.
        color.rgb *= smoothstep(-0.12, 0.08, dot(normalize(cloudPosition - planetPosition), lightDirection));
""".TrimEnd('\n');
}
