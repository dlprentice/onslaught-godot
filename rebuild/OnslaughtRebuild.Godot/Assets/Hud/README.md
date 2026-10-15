# First-person HUD assets

This directory owns the ignored local PC HUD textures consumed by the current
Level 100 first-person slice. Run `npm run prepare:rebuild-assets` to
materialize the exact supported files from a user-provided retail installation.
The payloads are not tracked in the current source tree or included in release
packages and remain copyright of their respective rights holders;
`rebuild/LICENSE` covers reconstruction code only.

The texture sources came from the released game's
`data/resources/dxtntextures` directory; `Dial.raw` came from `data`. Their
names are direct literals in the canonical Steam executable.
A clean app-owned Level 100 run, the complete Level 100 mission script, and the
released HUD render paths establish the retained composition: generated threat
circle plus target layers, the lower-left scanner/weapon assembly, the
lower-right influence-map/portrait region, and the conditional lower-center
message treatment. Fifty-five exact textures plus the exact dial byte-sprite
source are **retained** here; the executable manifest in
`rebuild/tools/materialize_retail_assets.py` is authoritative.
**Retained is not the same as composed**: the client loads and composes
**twenty-nine** of these, which is the count `CURRENT_CAPABILITIES.md` and
`rebuild/README.md` quote. Neither figure is wrong; they count different sets.
*(Updated 2026-07-27: was "Fifty-four exact textures"; `cdf8979d` added
`Hud/forseti-icon.texture.aya`.)*
*(Corrected 2026-07-28: the 2026-07-27 update above bumped this prose count but
never added the row it names, so the table below listed 54 textures plus
`dial.raw` while the directory and the manifest held 55 plus `dial.raw`. The
`forseti-icon.texture.aya` row has now been added and the 2026-07-27 note is
left standing, because it correctly records the change — it was the table that
was wrong, not the note. MEASURED: differencing the table's rows against the
directory listing and against the tracked `GODOT_ASSETS` entries for `Hud/` at
HEAD returns exactly that one file in both directions, and the other 55 rows
were recomputed against disk in the same pass and all match.)*

| Curated file | Released source name | SHA-256 |
| --- | --- | --- |
| `bar-line.texture.aya` | `hud%v2%BarLine.tga(0)X8R8G8B8.aya` | `16796E3A8ACFEC3529E03C29AFBEFBE28C92FFCCD5B05574F992E8F31976704D` |
| `battleline-outline.texture.aya` | `hud%v2%BattleLineOutline.tga(0)X8R8G8B8.aya` | `B1C097B29DD81E2A0493F72A157CCD5AD249B5ABF758224C75DF4F93973D0405` |
| `battleline-marker.texture.aya` | `hud%marker.tga(0)A8R8G8B8.aya` | `AB14538237BDD38486ADD4F5E9F38CFCA0069496CEEE5FAF1268E50EAB319BE7` |
| `circle-darkener.texture.aya` | `hud%v2%CircleDarkener.tga(0)A8R8G8B8.aya` | `7BD18594757165DCDD8DADB618EA99EB500ED105DBE2D6A6F66BBCBC31C323A3` |
| `circle-mask.texture.aya` | `hud%v2%CircleMask.tga(0)A8R8G8B8.aya` | `14D809F9B45F5153F82FA1F80152690B554710D83F91B8CBE203DE5CF18A9DFA` |
| `radar-outline.texture.aya` | `hud%v2%RadarOutline.tga(0)X8R8G8B8.aya` | `507D465248F7321F2332413B2C6F461F3B3C45D87C52D86C38C43104043D7DC7` |
| `compass-objective-marker.texture.aya` | `hud%v2%CompassObjectiveMarker.tga(0)A8R8G8B8.aya` | `E24FCA83DE34646A7328C313E7B89AC02C6BC4B04A69A74BF3EE81B3D57283DF` |
| `crosshair-dot.texture.aya` | `hud%v3%hud_crosshair_dot.tga(0)A8R8G8B8.aya` | `19E1B35B885A36230E5A1D47A9910164B0CA177746649A15C146CEFCA29651DD` |
| `crosshair-enemy.texture.aya` | `hud%Crosshair Enemy.tga(0)A8R8G8B8.aya` | `6A5C0D6DC22EA911BA783E630B5F623AB027FF45DA094C5816263F7B87DB98EA` |
| `crosshair-friend.texture.aya` | `hud%Crosshair Friend.tga(0)A8R8G8B8.aya` | `75B827AC9560846AD85904032249F707D84A74A432D3B732116F0E89A3621895` |
| `crosshair-outline.texture.aya` | `hud%Crosshair Outline.tga(0)A8R8G8B8.aya` | `1E27B9771F3D0416C07EFF9AEDE6863CA6F081386C86259F635E3D9A02ADE5B2` |
| `crosshair-predictor.texture.aya` | `hud%Crosshair Predictor.tga(0)A8R8G8B8.aya` | `519B30E4EDEE809D3FE660CCB866FEEEA2ACE4485B75373C98293AFB6E730AA5` |
| `crosshair-primary.texture.aya` | `hud%v3%hud_crosshair_primary.tga(0)A8R8G8B8.aya` | `310DAE2F7DD976F6CC724604737726885AFF96AB6BC507E41F90DCA60D134B17` |
| `crosshair-secondary.texture.aya` | `hud%v3%hud_crosshair_secondary.tga(0)A8R8G8B8.aya` | `7B078344E64D1E78EF64A8E21BDD3787E059B628C6A442634E9D13BA7D3A0487` |
| `damage-flash.texture.aya` | `hud%v2%DamageFlash.tga(0)X8R8G8B8.aya` | `8A25EC2E0BA8E66D86217684125D2E245DAE3EDDEB327EE2A7B144E2B45391C0` |
| `dial.raw` | `data/Dial.raw` | `2C57B657B92CD8BD73CA8C8986E8CE60AAFFB065FDDE09940053A2DD6D59671C` |
| `forseti-icon.texture.aya` | `hud%ForsetiIcon.tga(0)A8R8G8B8.aya` *(row added 2026-07-28 — see the note above the table)* | `0093B1A1A1E89327B90CEF93F06882DDED060A3023510CA0F49E6EBBF3666DA1` |
| `message-noise.texture.aya` | `MessageBox%noisebig.tga(0)X8R8G8B8.aya` | `F5C43C330394DB9EB7C1E782F3F30FE847DE01D7CE9335D2C7F9FD24BABB1825` |
| `radio-view.texture.aya` | `hud%v2%RadioView.tga(0)A8R8G8B8.aya` | `888D5A70AB812E23F75DB76AB2ED71CD2CCE04191EE282D525C86E337CC01778` |
| `radio-north.texture.aya` | `hud%v2%RadioNorth.tga(0)A8R8G8B8.aya` | `E5DFD8DB4DD73E9AEEFFBB009FCA68D889572C996987BCE365B0B5B4D0A7ED85` |
| `scanner-blob-small.texture.aya` | `hud%ScannerBlobSmall.tga(0)A8R8G8B8.aya` | `D7E9D287536F23E67BF35F678EC75D1A349353AE1D9D00B87CE09F6BD03641E4` |
| `weapon-fill.texture.aya` | `hud%v2%WeaponFill.tga(0)A8R8G8B8.aya` | `E639910D70AE10B044423CD5025C300C61CB8A9B5765890FD1A011C7D4499C0D` |
| `weapon-outline.texture.aya` | `hud%v2%WeaponOutline.tga(0)X8R8G8B8.aya` | `2E2DA786DB82C8FD76DE36D8D71FE744DDDDD364247467CBA1BFB9A95E52D62B` |
| `objective-inner-centre.texture.aya` | `hud%v2%ObjectiveInnerCentre.tga(0)A8R8G8B8.aya` | `FC42774E8C4F4534B65009807BFDB333443A9F5202C6A2C59DFF0DDDBED4F55B` |
| `objective-inner-left.texture.aya` | `hud%v2%ObjectiveInnerLeft.tga(0)A8R8G8B8.aya` | `70030AACE505E8E3D7F56DDE0B9C6A929F3D2C61912FC03AC816746B6D8A96BD` |
| `objective-inner-right.texture.aya` | `hud%v2%ObjectiveInnerRight.tga(0)A8R8G8B8.aya` | `7C85B0293FC7A524978A21E7CDC06B1DD3308E9A595AADCA054B51CD9A6AA113` |
| `objective-left.texture.aya` | `hud%v2%ObjectiveLeft.tga(0)A8R8G8B8.aya` | `0AE835780D1AF6C01F0272A50AFDA141ABCA70EAA5C23D74E7FC3968B6D9194F` |
| `objective-right.texture.aya` | `hud%v2%ObjectiveRight.tga(0)A8R8G8B8.aya` | `581F10446DB76ECE7AA7044B4C02F0431A79A7D606225D5C69A412C17F85078B` |
| `font-13ps.texture.aya` | `mustbe_Font13PS.tga(0)A8R8G8B8.aya` | `7ACC088B75E729CBDC2782E239A7D18BA0EC409E1BC890109AA1020F5EE81DC0` |
| `font-22.texture.aya` | `mustbe_font22.512.tga(0)A8R8G8B8.aya` | `ACC8DBDD60839C0F9686250025672CF9C370530114BC44910DF09BE487B247C6` |
| `guns-darken.texture.aya` | `hud%v2%GunsDarken.tga(0)A8R8G8B8.aya` | `0054E526A0980C5F89FC1271E7D34CFB551FC2567F6259F0BE11D63100AB12B1` |
| `guns-front.texture.aya` | `hud%v2%GunsFront.tga(0)X8R8G8B8.aya` | `4CE73B693E83C01812EBCA6DDD64EDCFBEBD4F85720AB7F5B87E425C9ECE1A06` |
| `guns-outline.texture.aya` | `hud%v2%GunsOutline.tga(0)X8R8G8B8.aya` | `E80DC5FB51A2C29DC696D85B3BEA036C427261D1BBE8F1DB14A531FBA996CFFD` |
| `guns-side.texture.aya` | `hud%v2%GunsSide.tga(0)X8R8G8B8.aya` | `52D46B47D26D6C779FE8A7E0E77DC13F6BCF91849869EA9EC81C3FA190164594` |
| `guns-top.texture.aya` | `hud%v2%GunsTop.tga(0)X8R8G8B8.aya` | `D8B09AF12EFAD59E9A25B91930BF9B8215F028C4E766EACC815C073A5E63304F` |
| `tatiana-portrait-oo.texture.aya` | `MessageBox%tat_oo.tga(0)A8R8G8B8.aya` | `39F40088069A8C68584A5A0CDA9E5AE7D4E4E5A248A12F0D0A240B8D3668621E` |
| `tatiana-portrait-ee.texture.aya` | `MessageBox%tat_ee.tga(0)A8R8G8B8.aya` | `4A4A17B72BBAFAE2B324E3A0A1C847226A288FEAA7C4273C45AA2DE8AEA3F99A` |
| `tatiana-portrait-mm.texture.aya` | `MessageBox%tat_mm.tga(0)A8R8G8B8.aya` | `802D8E22D8D304E12589A547F22AC2F2D5771B96AEA47306B3B2BBF752730DE5` |
| `tatiana-portrait.texture.aya` | `MessageBox%tat_aa.tga(0)A8R8G8B8.aya` | `34D451A6FC31E399B99032230413A60F146B41A0FEA65E61561A37D8EC757CFD` |
| `technician-portrait-oo.texture.aya` | `MessageBox%technic_oo.tga(0)A8R8G8B8.aya` | `B28A3818B8EF37DECFD8779D7ACAE74C657B5D510EDD2332587864F2A1E58A2C` |
| `technician-portrait-ee.texture.aya` | `MessageBox%technic_ee.tga(0)A8R8G8B8.aya` | `05326C603E8C9224C5BAB488A32AB9E9E19CA5B3FB424BC700AF97AE71C2527F` |
| `technician-portrait-mm.texture.aya` | `MessageBox%technic_mm.tga(0)A8R8G8B8.aya` | `263A2C107D6463A717DDEF20CC113CADFB585BD8ECBB1DB479F049843DCF3636` |
| `technician-portrait.texture.aya` | `MessageBox%technic_aa.tga(0)A8R8G8B8.aya` | `C4C1B11F4DDFB960AFC1C1D2A04020FADF997795ECCF651C07314141652F9603` |
| `kramer-portrait-oo.texture.aya` | `MessageBox%FC_Kramer_oo.tga(0)A8R8G8B8.aya` | `F4887E775C5EBCAC182B5CAABAE0C11FFE3FAB8AA32FF14DDCD9CE33D1BD50B3` |
| `kramer-portrait-ee.texture.aya` | `MessageBox%FC_Kramer_ee.tga(0)A8R8G8B8.aya` | `0B34A213A25E0EBC48A74F8CF73CF27BBB817F4CDFFBC79CD294A7D2858B9BDE` |
| `kramer-portrait-mm.texture.aya` | `MessageBox%FC_Kramer_mm.tga(0)A8R8G8B8.aya` | `69ED961309997F300BE094B6BA899D182B91C145FBF07F80FC2E0F8C8D149689` |
| `kramer-portrait.texture.aya` | `MessageBox%FC_Kramer_aa.tga(0)A8R8G8B8.aya` | `C6FE448E1D88E52C8ABC0AD96C386B774B8D979AB07EB6FF2A4CBF069E99106B` |
| `scanner-blob-medium.texture.aya` | `hud%ScannerBlobMedium.tga(0)A8R8G8B8.aya` | `B7F9D3D0B6A2C80933673514B8D032589B1DE2E4C60BCC362DF39E6244D71853` |
| `scanner-blob-large.texture.aya` | `hud%ScannerBlobLarge.tga(0)A8R8G8B8.aya` | `791327844E9700103AFAD1572DE3B4426E48088ED958033B227E256FFBED4152` |
| `scanner-blob-repair-pad.texture.aya` | `hud%ScannerBlobRepairPad.tga(0)A8R8G8B8.aya` | `BDDF99CBDD816A5ECD32E04E01A1F2DB356202B64015426E3AFF446268EC6469` |
| `screen-marker.texture.aya` | `hud%v3%ScreenMarker.tga(0)X8R8G8B8.aya` | `C5EC9C34386546A327A0E7188156E9A6B0259FEDA301320C111B1B7062C30A49` |
| `target-sighted.texture.aya` | `hud%v3%hud_target_sighted.tga(0)A8R8G8B8.aya` | `6CAAFCDC20228617B86BD7F4010E718E1D681EE52B0E904D73E6F16369BCAA88` |
| `offscreen-arrow.texture.aya` | `hud%v3%offscreenarrow.tga(0)A8R8G8B8.aya` | `8BBF5F0898A7796668E8A1A2BC6393C077B14BEB7623585856709BDD937D35D2` |
| `threat-flash.texture.aya` | `hud%v2%ThreatFlash.tga(0)X8R8G8B8.aya` | `43F5FB70D318F4969F43147C225A399D233EDCEA577592F14876AC542BD57F50` |
| `weapon-plasma-cannon.texture.aya` | `hud%Weapon Plasma Cannon.tga(0)A8R8G8B8.aya` | `D5165347BE203C17F302250AE20E83AE5B9525938B15CA8C6ACEB0D4392F06DF` |
| `weapon-vulcan-cannon.texture.aya` | `hud%Weapon Vulcan Cannon.tga(0)A8R8G8B8.aya` | `20D3C6D31B2815AF0B0A7DE5AA993164D911AC527205B40892F19F07EE34F3B8` |

The renderer uses source alpha for DXT2 layers and additive composition for the
X8 outline, bar, and message-noise layers. The released font loader scans alpha
within Font13PS cells to derive proportional advances. The client uses those
advances to wrap the 232-pixel text area into five 15-pixel lines, paginates
longer messages, and clips both each glyph and its shadow to the 232×76 text
rectangle. It does not use a fixed character-count guess.

The twelve 128×128 portraits are the released `oo`, `ee`, `mm`, and `aa`
frames for Tatiana, the technician, and Kramer. Their source files carry opaque
black corners, so the client applies inverse alpha from the released
black-outside/transparent-centre CircleMask around the released 0.75-scale
portrait before normal alpha composition. The square source background
therefore never reaches the framebuffer. The released render owner supplies an
approximately 50 ms cadence and 8/12/40/40 pose weights, but static evidence
does not expose Steam's process-global RNG seed or initial sample phase and does
not establish phoneme analysis. The renderer accepts read-only
active-message/playback state from the audio owner. Text pagination and portrait
cadence follow that real playback position; the deterministic weighted pose
sample is a presentation reconstruction, not a claim of Steam's exact RNG phase
or phoneme synchronization.

The released scanner path rotates contacts by Battle Engine yaw and clamps them
at 46 HUD units. The north sprite follows the released 45-unit heading circle.
The target-marker path can draw the released friendly/enemy, sighted,
predictor, screen, and offscreen layers, but the current canonical actor
snapshot does not yet supply HUD classification, target lock, or prediction.
Those layers therefore remain absent rather than being inferred. Authored
objective actors use their canonical three-dimensional registry pose projected
onto the current horizontal HUD plane; exact released camera-space vertical
marker projection remains unavailable. The compass uses the
released 50/40-segment geometry and 31/27-percent thickness inputs. Its rotating north treatment consumes frame
zero of the exact `Dial.raw` byte-sprite source; threat, damage, gauge-needle,
and objective sprites use the released 111.5, 96, 110, and 98-unit radii. The
gauges consume Core-owned hull and energy, but the exact released dynamic-ring
pixels and hull mapping remain incomplete and charge is unavailable. The
consumer also retains and
exposes Core-owned shield without inventing a separate meter that the released
compass owner does not draw. The client does not claim byte-identical palette
interpolation for Steam's dynamically written 16-bit ring texture.

The message bar uses Steam's native 120-pixel pieces, bottom-centre anchors,
`0x90000000` inner tint, and five-line layout. The Level 100 script contains no
video command or Bink portrait asset: its complete released speaker set is the
three four-frame portrait sets above. Message text, signed ID, and audio identity
come only from the hash-verified manifest derived from the locally materialized
MSL and native language tables. Speaker and highlight remain arguments of the
matching ordered mission events. The Godot presentation projection consumes
those events without feeding any HUD timing or state back into Core; the
renderer has no authored fallback message catalog.
The same manifest supplies the exact native `Mission Complete`, `Retry`, `Back`,
and three Level 100 failure-reason strings for the separate frontend/result
owner; the HUD does not draw its own result screen.

The current projection consumes mission-owned enabled weapon gates, HUD
emphasis, ordered message/help events, and active objective actors from the
canonical registry. It retains each objective's typed `ActorId` and full
three-dimensional registry position until the renderer performs its final
horizontal HUD projection, and repeated help deliveries remain in emitted
order. It deliberately leaves selected weapon, selection UI,
weapon resources, non-objective contacts, threats, damage flashes,
target/prediction, active-help lifetime, and battleline influence unavailable
until their mechanics owners exist. Energy, shield, and hull continue to come
from the deterministic player snapshot. Godot does not invent substitutes.
The consumer retains the exact 13-node, 22-link Level 100 battleline topology.
The released
interior is a continuously triangulated terrain-extent mesh, not a drawing of
those links; its live influence values and render mesh are unavailable from the
current producer, so Godot draws no guessed links or interior. The typed
objective consumer uses only active actors marked by the canonical mission.
Split-screen and other missions are outside this asset slice.
