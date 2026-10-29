# Level 100 opening assets

Status: active
Last updated: 2026-10-29 (retail byte dumps, instruction quotes and private paths removed; 2026-10-22: a link to another repository's goal document stated inline; content otherwise as of 2026-09-08)
Summary: locally materialized training assets, their identities and provenance.

This directory owns the ignored local released heightfield,
macro/detail/cloud-shadow terrain inputs, cube-25 sky, four close-pine meshes,
three training target meshes, the two ambient aircraft meshes,
eleven player-projectile/target-destruction
effect textures, 26 exact mission effects, the tutorial music selection, and all
51 English character messages in the accepted Level 100 mission table. The
three shared front-end effects retain the startup lane's `Assets/Frontend` paths. Run
`npm run prepare:rebuild-assets` to materialize the exact supported files from a
user-provided retail installation. The payloads are not tracked in the current
source tree or included in release packages and remain copyright of their
respective rights holders; `rebuild/LICENSE` covers reconstruction code only.

`StaticWorld/level100-static-world.json` is a deterministic ignored manifest
derived from the exact released Level 100 archive. It owns all 33 visible
base-world objects, 24 selected non-tree mesh types, four pine mesh variants,
1,481 pine transforms, 34 mesh/water/imposter textures, their active material
signatures, and their exact hashes. The materializer converts those 28 meshes
and verifies a 62-file retail source set; those generated payloads are not
mirrored in this document.

`../../../OnslaughtRebuild.Core/Assets/Level100/level100-contact-owners.json`
is a separate ignored Core input derived from the supported physics definitions,
mesh archives and Level 100 placements. It retains hash-verified definitions
and deterministic millimetre-quantized projected topology for contact. It is a
locally materialized derivative, remains ignored, and is not a claim of
bit-identical retail collision geometry.

| Local materialized file | Role | SHA-256 |
| --- | --- | --- |
| `../../../OnslaughtRebuild.Core/Assets/Level100/level100-heightfield.hfld.bin` | Exact released `HFLD` chunk embedded in Core and adapted by Godot | `7A4C7C5B9400E2C8D2325CECB5C44701CD8A6E6F8609CBC8BC31D449C0620F5D` |
| `../../../OnslaughtRebuild.Core/Assets/Level100/level100-contact-owners.json` | Hash-verified deterministic contact projection for the 24 static definitions plus the Target Tank, Target Truck, Warehouse and Target Drone definitions *(hash corrected 2026-07-28 — see below)* | `C45E89D14AD7ABD9BED37D388453018C4F5C5E37E10F3B8307BEC16C81D524F2` |
| `Source/level100-root-terrain.rgb565.bin` | Exact initial 512x512 root landscape pixels reconstructed from the released Level 100 and base archives | `6EB202F450926097930BEDCA440F0163A1886572981E3C69B4EDF9289A68AE2B` |
| `Source/level100-terrain-hierarchy.bin` | Exact retained sources for the five released logical landscape caches | `541EACD0AA75FAE8BEFB8A3E1505EA52AE6B1F6C1367C15C65D7DD23B7CFE977` |
| `Textures/terrain-detail-00.texture.aya` | Exact released 512×512 DXT1 `mixers%detail00.tga(0)R5G6B5.aya` selected by Level 100 | `7C9C22169D13ED8B7D6AD69286BDB59CC88F9AE3BFB6A9D3A0503D320386BFEF` |
| `Textures/terrain-cloud-shadow.texture.aya` | Exact released 256×256 DXT1 `clouds%shadow.tga(0)A8R8G8B8.aya` loaded by the landscape renderer | `FC7441887E494E4B18F2B16179ED42C17801B128D71E29D653A4E8B792869519` |
| `StaticWorld/Source/level100-water-surface.surf.bin` | Exact released 18,572-byte `SURF` shoreline payload | `C3177354FED3EB5A94DC72DEBF2465C32AB1D931DE79E5E88AC431043D3E917D` |
| `StaticWorld/Textures/water-reflection-00.texture.aya` | Exact released 512×512 DXT1 authored water reflection image | `41117238976776B114B8AF4D1E4FBCCD3AFB90245F46F59B353E83663CAC7B6E` |
| `StaticWorld/Textures/water-caustic-00.texture.aya` | Exact released 64×64 DXT1 caustic stage | `7F34EE7D90CA483893C3ED8B0BF01BDF07B9A0B0F4A48F9DF5FEFD961D796F0A` |
| `StaticWorld/Textures/water-waves.texture.aya` | Exact released 128×128 DXT1 shoreline-wave stage | `6EC848D1F9801BE12F3A6591D6A4F5D5ECF1FC9F21D1A4242E1D681D826AB078` |
| `StaticWorld/Textures/water-sun-blob.texture.aya` | Exact released 128×128 RGBA8 water-sun blob | `5D97F24F514383C928C58C7F333BF489888B6A402004213FFBAAAAD2EF30A53E` |
| `StaticWorld/Textures/water-sun-reflection.texture.aya` | Exact released 64×64 RGBA8 sun-reflection stage | `A65940D6CDFE93F8B8820EFB883FD33166AEC63863ED894673466F3F58527AB4` |
| `Source/m_f_pulsetank_training.msh.aya` | Released Firing Range Target Tank CMSH archive | `9B2CFDCEB86ED700ED924051FBFF13C32DC30BD8F8B948EA1CF8AA9FBFE8B97B` |
| `level100-target-tank.obj` | Target Tank geometry and exact material group consumed by Godot *(hash corrected 2026-07-28 — see below)* | `88BAE82B7D5080E307CBE49AA39C06C21EFBCC7DE806E236B5CB663AA7D5BA63` |
| `Source/m_f_truck_training.msh.aya` | Released moving Target Truck CMSH archive | `3BD92CE93D0619B7C4B0DD158680641FBAB6CD88580A68C6EF34E5F22F7596C5` |
| `level100-target-truck.obj` | Target Truck geometry and exact material group consumed by Godot *(hash corrected 2026-07-28 — see below)* | `39EB4F7723725422EE348CA6052C437BE2E59AAC38DDAC0CA4559213FC4E0113` |
| `Source/m_m_warehouse.msh.aya` | Released Firing Range Warehouse CMSH archive | `61FE5465BD7AFFEDF749AD784209BE02B2E4DD28631E70386C3810302B5F6F15` |
| `level100-target-warehouse.obj` | Static intact Warehouse geometry and exact material groups consumed by Godot *(hash corrected 2026-07-28 — see below)* | `DD605F3778D465A5E25A99B43C7721DEC37D509732E65DB3E6C71534F6B4A9AC` |
| `Source/m_FA_F24_training.msh.aya` | Released Air Trainer / Target Drone CMSH archive — one retention serves both definitions *(row added 2026-07-31, task #114 — see below)* | `48876552AE836750221241719F333FB9B5221F78F1AB8BC03D5950CDBF4E6EC5` |
| `level100-air-trainer.obj` | Air Trainer and Target Drone geometry and its single material group, the Target Tank's verbatim *(row added 2026-07-31)* | `1D8EE300E9349DF27A889AF85E597778A2422634D73CED5D284C7140DD9B885F` |
| `Source/m_f_lifter.msh.aya` | Released U-17 Highside Transporter CMSH archive *(row added 2026-07-31)* | `CED8FDD223E6592B37586395197D86843AFEC68456EFDE4C8211B8CD75593DF3` |
| `level100-transporter.obj` | U-17 Highside Transporter geometry and its two material groups *(row added 2026-07-31)* | `A6A39EA17E811B4D6F18FCC02B8EB753E6FD29643E8359ED068BB26FD76DE787` |
| `Textures/target-tank.texture.aya` | Released 512×512 DXT2 Target Tank base texture | `97DDD1E18E45B19E249E91E881D773D80D36768A2CD48F6549A769C2559A7B7E` |
| `Textures/target-truck.texture.aya` | Released 512×512 DXT2 Target Truck base texture | `AB4125242321DE4963C51C9B22F63C951A33C22E874D8F039FA2C61A109F5E81` |
| `Textures/target-warehouse-m001.texture.aya` | Released 512×512 DXT2 Warehouse base texture used by material groups 0 and 1 | `689B184AB8A5D03F33B69E5C35EDCFDFDEC12AA9B4B31F7C74CE5209F6236A49` |
| `Textures/target-warehouse-m002.texture.aya` | Released 512×512 DXT2 Warehouse base texture used by material group 3 | `8FABADBE1C5AF067A740CF05DEBD1C952C628FD5FA3EA92B8202094704B8A20D` |
| `Textures/transporter-lifter01.texture.aya` | Released 512×512 DXT2 U-17 base texture, mesh texture slot 2 *(row added 2026-07-31)* | `556F8FAB2E9D708412D085C81F07DBFE800A9EE415C4E7CCF1AAAD56BD2135E6` |
| `Textures/transporter-lifter02.texture.aya` | Released 512×512 DXT2 U-17 base texture, mesh texture slot 0 *(row added 2026-07-31)* | `FA03F88A6132819FD0AC33266CDFB2B7423BF20F7B6C9ABA05D370807EFBA21E` |
| `Textures/material-overlay-a8trust5.texture.aya` | Released 128×128 `meshtex%a8trust5.tga(0)A8R8G8B8.aya` Warehouse material overlay stage *(row added 2026-07-28 — see below)* | `4CCDE973F9741C110A82F350E102F1A12C566FF3D3B1B4F5426F2BBF536BE843` |
| `Textures/pulse-bolt-blue-spark.texture.aya` | Exact released 64×64 DXT2 `Particle%Blue Spark 2.tga(0)A4R4G4B4.aya` Pulse Bolt sprite | `B3730B1E9D7713910E0DE4BD0CB0DCFEFCB9CEB8F6402D50681A524ADC0DCB08` |
| `Textures/pulse-bolt-blue-trail.texture.aya` | Exact released 64×64 DXT1 `Particle%Blue Trail.tga(0)R5G6B5.aya` Pulse Bolt trail | `2B4BC5CF8902D7EA8452F1068AC8F11514C8238A733CA33AAD7D6D0667688A63` |
| `Textures/vulcan-bullet-trail.texture.aya` | Exact released 64×64 DXT1 `Particle%Bullet.tga(0)R5G6B5.aya` Walker/Jet Vulcan round trail | `42DA4484967F48958E71C9529435306C284FBBC36FC1F7A6CA2BEFC1EAC2F01C` |
| `Textures/mech-pulse-medium-halo.texture.aya` | Exact released 64×64 DXT1 `Particle%Halo.tga(0)R5G6B5.aya` medium-round halo | `CDE6EFC90DC7958C5BDA425A04486E277BEB85A7F1C33FB9074F369E92D58EDB` |
| `Textures/mech-pulse-medium-energy-trail.texture.aya` | Exact released 64×64 DXT1 `Particle%Energy Trail.tga(0)R5G6B5.aya` medium-round energy core | `64EDDC6B147C67886F41EF4D2BCC2A0606B453B01E4D93B9962F10CC07ABA92E` |
| `Textures/pulse-impact-animated-blob.texture.aya` | Exact released 256×256 DXT2 `alparticle4` impact/smoke animation | `74085B280199E20B765640CFC3E417E6DA0FCBFB25384E129858A32F5DEB995D` |
| `Textures/pulse-impact-shockwave.texture.aya` | Exact released 128×128 DXT1 `1telep` medium Pulse Bolt shockwave | `E92EFC3F5ADFA347E6B50F1E3D20AF4C6800D76853A2126D71237DFEFEEA9F10` |
| `Textures/vulcan-impact-spark.texture.aya` | Exact released 256×256 DXT1 `alparticle2` Vulcan unit-impact spark atlas | `95C15D4269FFEA56E7BE13AC7FB64A71A999CCE2B9417CB73CE9C7313CEF4389` |
| `Textures/effect-flash-medium.texture.aya` | Exact released 128×128 DXT1 `sun2` medium impact flash | `D7FBFCB4EDB2167FEDC0A467D4501C9BBC2F6A2852C7873DAEC3953E6F518F5C` |
| `Textures/particle-alparticle5-additive.texture.aya` | Exact released 128×128 DXT2 `alparticle5` Pulse Cannon muzzle-flash atlas | `5004B8C6A688B82605F870E60D4ED32A32203B4371F1AEC72155FEF1619A5FA0` |
| `Textures/target-tank-explosion-animated.texture.aya` | Exact released 256×256 DXT1 `alparticle6` medium explosion animation | `3C8FC30AD4923C56C3735CAAB5661A3F176EB661EAA678093870F51DE4204C9E` |
| `Textures/target-tank-explosion-fireball.texture.aya` | Exact released 256×256 DXT2 `fireball` target-destruction layer | `E6C166669E351632A90B41C74782967923C78FC8BE644A1E8948D356806B23ED` |
| `Sky/cube25-cent.texture.aya` | Released 512×512 cube-25 center DXT1 texture | `1AAD6CC8F85B6BB7CCBB8D2C7B0E6AA31722A9ADBDE5A3F19B248430CA83469E` |
| `Sky/cube25-up.texture.aya` | Released 512×512 cube-25 up DXT1 texture | `419E2424BCFD698058D72111FFA7D84FDC9022E03815DB7C0DA28403F4925F3C` |
| `Sky/cube25-right.texture.aya` | Released 512×512 cube-25 right DXT1 texture | `830C9B965C76A4023C2415B7C8924CA32590562C850CC84E92C003E173263D11` |
| `Sky/cube25-down.texture.aya` | Released 512×512 cube-25 down DXT1 texture | `4770829BA631E93FBC33DB2012754DA75A06BFCCC2FB2B36875E92032E22D19D` |
| `Sky/cube25-left.texture.aya` | Released 512×512 cube-25 left DXT1 texture | `D7CBCE30E51473DDC89ED0C44326E598DAC4D2682F64EF20C19237AFD2CEBE14` |
| `Music/tutorial-track-03.ogg` | Released `MUS_TUTORIAL` zero-based track 3, `data/Music/BEA_04(Master).ogg` | `32D3E338964D74F50D0094536C585375F1E14AA2BAE6087487803F3529EAF360` |
| `TutorialAudio/hud_01.ogg` | Released English `data/sounds/english/MessageBox/hud_01.ogg` | `BAE30243A2B5FE3DAE718181AC5B05D766F93D5E25B042FE1B04C71FC9347909` |
| `TutorialAudio/hud_02.ogg` | Released English `data/sounds/english/MessageBox/hud_02.ogg` | `43AE0C306B7935A21D415338348508EABF3A61F8799C0FD0873C89919FB84A35` |
| `TutorialAudio/hud_06.ogg` | Released English `data/sounds/english/MessageBox/hud_06.ogg` | `4ED80A12FA7D2AD07A044F95F94D52455413962B75E7689101DF6907711F3235` |
| `TutorialAudio/tutorial_message_log.ogg` | Released English message-log instruction | `7A03FF8F3FAA4BE4B729E7619055379C62921E2EAEB67FC9711DAC0DFE273F8B` |
| `TutorialAudio/tutorial_technician_01.ogg` | Released English technician status | `4792371453B4402454B922A481EB0968A099EFB13981FF1918AA6177FB6AE151` |
| `TutorialAudio/tutorial_13_mod.ogg` | Released English movement instruction | `7EEE9087F86C00ABE4FEAB115B20E4E2F27A8E6D1ADC7318B1602446A7493E65` |
| `TutorialAudio/tutorial_01.ogg` | Released English Target Zone 1 instruction | `48E40B07A77B5776F817ED8D8FFE1EFF1A978B10480CAB92019077E7B66784A8` |
| `TutorialAudio/tutorial_scanner.ogg` | Released English objective-scanner instruction | `7A9535B1187B6E1FF276CEBC3906EC2102E5D166F381EE674113B4F09C2B3BD2` |
| `TutorialAudio/tutorial_02.ogg` | Released English Firing Range assignment | `FA0533DE72B8D7702B83B709BA631BC8F7A42A5183BABCB147AE653A5D7A2904` |
| `TutorialAudio/tutorial_03.ogg` | Released English weapon-system introduction | `8E3BBD3F680099F7664F473F73837BF3E6D09474B4426677DD6BF27B31177DC2` |
| `TutorialAudio/hud_05.ogg` | Released English current-weapon indicator explanation | `66256D87557946647A51A2E8D49E044BC55AE370C4AD1C8E950B1D884EC082EB` |
| `TutorialAudio/tutorial_pulse_cannon.ogg` | Released English IS-5 Pulse Cannon introduction | `2FDA4A38B4737E03647C03BAC38BFB36E7E6FF16B279007C04616C23857C25F8` |
| `TutorialAudio/tutorial_open_fire.ogg` | Released English Firing Range target instruction | `04A1A65B45F75F4D1E85B0FAB6970125584EFBABE3609D7413E60B569A26D20C` |
| `TutorialAudio/tutorial_pulse_cannon_2.ogg` | Released English Pulse Cannon energy explanation | `F4ECA49F26F61F0369C0D8B770300596695F8A62EC12269A4C9D1CB3F61B13E0` |
| `TutorialAudio/tutorial_vulcan_cannon.ogg` | Released English Vulcan Cannon introduction | `7F483D8F3C876C8E9E8BD52B0369F1C54C39C83C174C0A43D5FB8674C069172C` |
| `TutorialAudio/tutorial_open_fire_2.ogg` | Released English three-truck instruction | `122782139A31FBD777A734E0979F4F0AB8A7308D1154D7215CE2AF13D56E3237` |
| `TutorialAudio/tutorial_vulcan_cannon_2.ogg` | Released English Vulcan ammunition warning | `6F872CA07FCC4F49FFB0CB2536A460411139FE1D77D263198C0F423FCEFA9D90` |
| `TutorialAudio/hud_03.ogg` | Released English energy-gauge explanation | `3D58AB5ABF9715EE4B3C657BF3CA7C0B91C2985382A7D72C948513045669C31E` |
| `TutorialAudio/hud_04.ogg` | Released English armour-gauge explanation | `3B96AA4CF15202D2977D32A04E73715DC1E940E00596D1614ACD47262C86E77D` |
| `TutorialAudio/hud_07.ogg` | Released English battle-line explanation | `8D75F5981ABB9FC494EB77F0A721B38B8EB95083BE9282E67561E4BF18E9A8D4` |
| `TutorialAudio/hud_08.ogg` | Released English `data/sounds/english/MessageBox/hud_08.ogg` | `06E0C641803B25FE157E8FD27420E11BE5972DF3B0DCD80681926577C9B0C361` |
| `TutorialAudio/tutorial_04.ogg` | Released English flight-mode instruction | `78C04E99FF5AE90FB224FA2DFFAE6B3101123D32FE73E3C9EE6B37EC4D6482C6` |
| `TutorialAudio/tutorial_05.ogg` | Released English moving-target instruction | `BD047BD04EEEFEECA91ED1AE3244EB65F8FE26DE33F81BA18BD9505BC4494C58` |
| `TutorialAudio/tutorial_06.ogg` | Released English remote-drone instruction | `D61F834AC1EFE27FF6982D25A401CFC48DD7BFAF03EE38F3BDFADB0D819F11C1` |
| `TutorialAudio/tutorial_07.ogg` | Released English armed-drone instruction | `1D01BB7C56BC582944161B415CFB18518ABBEB211067A0DB62F80D70C587AF7E` |
| `TutorialAudio/tutorial_08.ogg` | Released English return-for-diagnostics instruction | `A7888025D9DB2094DF63145E7D58B53BE5054ABF28C678F727155F6ED282C205` |
| `TutorialAudio/tutorial_09.ogg` | Released English repair-pad instruction | `D306048BB7D1680C4B5E3FD8B7A01F966725C1C4D7E4F9A09B9E9E25DCAE7557` |
| `TutorialAudio/tutorial_10.ogg` | Released English training-abort instruction | `89A881C253C9CBC5227E84E942B0C8C3361B5B6BD2694B61522BD97AF320F5FB` |
| `TutorialAudio/tutorial_11.ogg` | Released English successful-completion line | `C46293FF014967F62D90F870CFE8A8FC228FCC7A0B5ACB7CBE3D203341A39984` |
| `TutorialAudio/tutorial_12.ogg` | Released English Target Zone 3 instruction | `E6CBF2CB4DABCFACEC0479DEE1EEB33ED62E1D3B46BF238B638D75AA55B7A425` |
| `TutorialAudio/tutorial_aborted.ogg` | Released English aborted-completion line | `320B8A1E4619DDEECF94AFD8862E8B90C7EBABA3FFFD5B8976A3D6160497C1B5` |
| `TutorialAudio/tutorial_broke_1.ogg` | Released English Tatiana failure line | `30E2E6652508C7B9BF13BEB2F19CD0ED66CE474D677E946CEEEBD30A905D6696` |
| `TutorialAudio/tutorial_broke_2.ogg` | Released English Kramer failure line | `8E6AF2BD4038B89C72E45C8E86A5B5E32FBA12F50728B82A26223918F6D96315` |
| `TutorialAudio/tutorial_broke_3.ogg` | Released English friendly-fire warning | `57C3C737699B02D96CD2B9CB2E6B5E8D2F513C92B51E3B13B7728F9127BC8D8F` |
| `TutorialAudio/tutorial_dodge_2.ogg` | Released English incoming-missile exercise line | `D88EFF763474BCFF488EBF913B1D8B45900452F34331EF6DB334C589768D66B2` |
| `TutorialAudio/tutorial_dodge_3.ogg` | Released English missile-threat-circle line | `7796DD70479A91C48D027594BBFC1BB6C0206336006657C13C2220185BE5C0A8` |
| `TutorialAudio/tutorial_dodge_bad.ogg` | Released English failed-dodge assessment | `B551C989F66C3C30E048A73F230DBABCF19E588CD93A95B37B5A2654569BCDD5` |
| `TutorialAudio/tutorial_dodge_good.ogg` | Released English successful-dodge assessment | `329D8269262C2F4C25B95A4373AD932F53A09B26003238C9D6AA3D613E98669D` |
| `TutorialAudio/tutorial_dodge_mod.ogg` | Released English dodge-control instruction | `C74568F49114F77CC42426CF029BE35434BF8058F2D05B1D4B6ADB73B124C3A5` |
| `TutorialAudio/tutorial_help_player.ogg` | Released English repair-help line | `287998779062D08030350BFCA83E625AC55D12BB75775D4CC59B1F9E3AFB7B5C` |
| `TutorialAudio/tutorial_landing.ogg` | Released English landing instruction | `07428CE3BFC31603E472BA83AFFD1D355B0C3CABEC24D8AD87E3FE30CC646176` |
| `TutorialAudio/tutorial_strafe.ogg` | Released English flight-strafe instruction | `F6DA37701FFD0DDC9CF2780C80674305459C552FEA3D1F383B65EBC76565DB70` |
| `TutorialAudio/tutorial_throttle_mod.ogg` | Released English throttle instruction | `29D3791D61286318FDEDB4BDC88C267488F647D65996A581FB3DA91B2D2E2EF4` |
| `TutorialAudio/tutorial_technician_02.ogg` | Released English technician line 2 | `196032DDDADBE1CBFE3315635991C02E62B05CAC083A90AD4DFAAEBBAB8EB955` |
| `TutorialAudio/tutorial_technician_03.ogg` | Released English technician line 3 | `952D91A0A416A20487D790DEBCF82DDE357935946FD7E4B69678CBD350D8681C` |
| `TutorialAudio/tutorial_movement.ogg` | Released English movement callout | `79F7DDA58130D4B8E506683F7CF65C9213DFFCA074885DF06AAAA857B0C28340` |
| `TutorialAudio/tutorial_weapon.ogg` | Released English weapon callout | `E1AFE902725A81D9900A1D5BEE65D7FDF47B61AB59246DE7ECADE89F2A9E7B00` |
| `TutorialAudio/tutorial_overheat.ogg` | Released English overheat callout | `34A084751BAFA8320FA0D8B34ED17B6F8FC3F38630B0CAF6528A3287A39B8683` |
| `TutorialAudio/tutorial_ammo.ogg` | Released English ammunition callout | `1A7474FFD1AA2C88A56B60D4F27A77A3FEA348ABF24A17A9F6E3251308AF8769` |
| `TutorialAudio/tutorial_water.ogg` | Released English water callout | `99DC92041FB49BD4B06D0E875B3DED4155E2F4A8BA4AED9B40596CE0012D1151` |
| `TutorialAudio/tutorial_zoom.ogg` | Released English zoom instruction | `D0AADAC588017F5A410358494603F7E052890C93D6B52DDBC61CBE171CB6940B` |
| `SoundEffects/terminal-ammunition-depleted.wav` | Exact HUD ammunition-depleted warning | `F1BD9E787FAA1D32C149340B16AF3D485CD6AD6B46ED09C30C889FC16B5A8DA1` |
| `SoundEffects/terminal-armour-low.wav` | Exact HUD armour-low warning | `6DA6B88ABC77E7E281D338CF45EF35DD2D8AA99CCA578418F6B6F28115D60D61` |
| `SoundEffects/terminal-energy-low.wav` | Exact HUD energy-low warning | `E7BF31D24623F3A37A6D578A52AFAE975A1B6D95DC9BB07A0DD1E1ED9C913F6D` |
| `SoundEffects/terminal-hostile-environment.wav` | Exact HUD hostile-environment warning | `3F841B810AAC4A67221319A0D28F877C5BF1555130AEEC337234E3F304DDF32F` |
| `SoundEffects/terminal-incoming-missile.wav` | Exact HUD incoming-missile warning | `5BF1FB9CFAEF17FC0D62A16D3B37E07C85A5F9FA9F78D7135DC4EE75759ECDF6` |
| `SoundEffects/terminal-incoming-warhead.wav` | Exact HUD incoming-warhead warning | `711297354E61F9A94AD66172646CAEBC7683C9A67066B3FF7793996DD57E918E` |
| `SoundEffects/terminal-micro-missiles-selected.wav` | Exact HUD Micro Missiles callout | `811D18C02399FBE05DF649ADB42732DF9C6C4C3E85F4D9ACAFA1ED3751DB04C1` |
| `SoundEffects/terminal-pulse-cannon-selected.wav` | Exact HUD Pulse Cannon callout | `99A29B4BEAD7CD484547ADBC3954A6F805638E6FCDF95CF70E90B6D5183734DA` |
| `SoundEffects/terminal-vulcan-cannon-selected.wav` | Exact HUD Vulcan Cannon callout | `A742431A169BBA0A92F0241556A79D704626D56E1DC14BB43F41079AA2CE4990` |
| `SoundEffects/terminal-weapon-overheating.wav` | Exact HUD weapon-overheating warning | `F3F9E967708177F52667C872B152362284E0A8BA55B9E6A1137A5B981F44A34D` |
| `SoundEffects/drone-vulcan-fire.wav` | Exact Target Drone `Blaster 2` launch effect | `5E9227999084BD4FB06558B498C86F968C79B8DF87B06D4621336BBC994B577B` |
| `SoundEffects/explosion-small.wav` | Exact shared Pulse-hit and Target Drone `Explosion Small` effect | `28F89761970629118B989B41B5DDA3253FECB431EC479B64D056248BC3E5C1DC` |
| `SoundEffects/facility-explosion-medium.wav` | Exact Level 100 facility medium-building explosion | `F86D23CFA18025BA9D1283E0B10D5BC1F939161465A924140044D3C0A6095D3A` |
| `SoundEffects/aquila-explosion-huge.wav` | Exact Battle Engine destruction effect | `23A85AA8C5543F5BE15F7D8B2279859560F5D417E9475F015A69522DB3C8AED8` |
| `SoundEffects/transport-explosion-large.wav` | Exact U-17 transport destruction effect | `4CE37C52A010E7903A5E7D4073E0647CA790FBFE839FACBBA97AD4D7FE2DE2FD` |
| `SoundEffects/component-explosion.wav` | Exact released component-destruction effect | `C840A5EA48F975ECDB03E82646C369D164282E1AC10B03164923271DA1067DEC` |
| `SoundEffects/explosion-large-debris.wav` | Exact released large-debris explosion | `E5DAEFB4F5DB5738B0CD8CD2619868D090CA4A604642BC05D302C1AFB54980FE` |
| `SoundEffects/explosion-huge-ground-debris.wav` | Exact released huge ground/debris explosion | `5205F38B925EC77A04AAED0501387176D49F0A69B11479127168936E8EBFCDEC` |
| `SoundEffects/trainer-flyby.wav` | Exact Air Trainer Forsetti flyby loop | `838EAC239FC8B53EF89471D433978D7E4AEE0B3F96CEA5D3281F60E2CFA6D2CE` |
| `SoundEffects/transport-flyby.wav` | Exact U-17 bomber/flyby loop | `6317E8056A8B4F657AEFC1319FB8EF512927846DDE8A287498B2C2E852108A1C` |
| `SoundEffects/repair-charging.wav` | Exact repair-pad prefire/charging effect | `9AC76F06602D5432188901DE75DAAA2BB87DD12F46F7DF88D6EDA73BF31632BA` |
| `SoundEffects/repair-full.wav` | Exact repair-pad launch/heal effect | `2725B5298E6FA84C0D96DEEFDF3D1FE7FEFB854CE8512A70441D1F95C395BB1F` |
| `SoundEffects/repair-idle.wav` | Exact repair-pad idle loop | `EC3F2AF86C5281D42923C3AE00FB66222E4E358A5F986FD26C7A43C34406F7D4` |
| `SoundEffects/pulse-cannon-fire.wav` | Exact 44.1 kHz mono PCM decode of `Battle Engine\N_BE_pulse_cannon_fire` | `710FF06DB55BC694EFB8FF7D3A5AB658125E7CA0FE6B4733A805DA98B22B0277` |
| `SoundEffects/target-tank-explosion-medium.wav` | Exact 44.1 kHz mono PCM decode of `Impact\N_I_explosion_medium` | `7228AE049CB0A9877E63671A65E51829443017B2C4981DF90A9C64D2F38B6D9C` |
| `SoundEffects/pulse-impact-small.wav` | Exact 44.1 kHz mono PCM decode of `Impact\N_I_explosion_small`, `sounds.sfx` record 105 *(row added 2026-07-28 — see below)* | `3296B13938928F54847A29E17307E7875E9933F8FD6381BF0DFCD260CD6FC131` |

### Corrections to the table above, 2026-07-28

**Four hashes were superseded, and one Role text with them.** The values this
table carried and what replaced them, recorded so that a reader holding an old
hash can tell it was corrected rather than lost:

| Row | Was | Is |
| --- | --- | --- |
| `level100-contact-owners.json` | `FE5F1095…45F138B0`, Role "…for the 24 static definitions plus Target Tank and Warehouse" | `C45E89D1…C81D524F2`, Role naming all four target definitions |
| `level100-target-tank.obj` | `3A67F2BF…C5DF9F` | `88BAE82B…A7D5BA63` |
| `level100-target-truck.obj` | `76EF7ED7…44581D` | `39EB4F77…FC4E0113` |
| `level100-target-warehouse.obj` | `3883B651…D8EB5CE` | `DD605F37…F6B4A9AC` |

MEASURED: SHA-256 recomputed over the four files as materialized on this
machine. Each new value is independently pinned by the tracked producer — the
three OBJ hashes at `../../../tools/materialize_retail_assets.py` beside their
`.msh.aya` sources, and the contact-owners hash both there and at
`../../../OnslaughtRebuild.Core/Level100ContactMap.cs`. The three OBJ hashes
moved together in commit `dd325044`, which made the OBJ conversion carry
per-vertex `DIFFUSE`; the contact-owners hash and its Role moved in commit
`9688ee0b`, the same schema bump described under [Dependency-inverted contact
and destruction payload](#dependency-inverted-contact-and-destruction-payload).
In each case the table was not carried along. **The other 109 hashes in the
table were recomputed in the same pass and all match**, so these four were the
only stale values.

**Two rows were added.** `Textures/material-overlay-a8trust5.texture.aya` and
`SoundEffects/pulse-impact-small.wav` are both produced by
`../../../tools/materialize_retail_assets.py`, both landed on 2026-07-20, and
neither was ever listed here. No existing row changed to accommodate them, and
the "25 exact mission effects" count in the opening paragraph was corrected to
26 in the same pass. MEASURED: differencing this table's rows against the
`GODOT_ASSETS` entries for `Level100/SoundEffects/` and `Level100/Textures/`
returns exactly these two and nothing else, in both directions.

### Rows added 2026-07-31 — the two ambient aircraft (task #114)

Six rows: `Source/m_FA_F24_training.msh.aya`, `Source/m_f_lifter.msh.aya`, their
two converted OBJs, and `Textures/transporter-lifter0{1,2}.texture.aya`.

The Air Trainer mesh was previously read and hash-verified **in place** rather
than retained, because Core needed only its contact parts and nothing drew it.
It is now an ordinary `DIRECT_ASSETS` retention, and the in-place special case
is gone. MEASURED, and the reason the special case existed: `default
physics.dat` record #660 spells the mesh `fa_f24_training.msh` in lower case
while the shipped archive file is `m_FA_F24_training.msh.aya`, and that string
is emitted verbatim as the `mesh` field of the hash-pinned
`level100-contact-owners.json`. The producer now resolves through the retained
file name, so both pinned generated payloads are byte-unchanged:
`level100-static-world.json` = `2DFAD0DC…8568` and `level100-contact-owners.json`
= `C45E89D1…D524F2`, exactly their pins at the time. `level100-static-world.json`
subsequently moved to `97E3B3BF…0C27`, by the 2026-08-01 vertical-datum correction
in `_actor_pose` and nothing else — 54 leaves, all of them an actor's or a
spawn's `initialPose.positionMillimeters[1]`.
The September 8 signed basis conversion moved it to `EE834981…D552`:
237 projected-basis words change from +0 to -0, with all raw retail fields,
positions and other content unchanged. The subsequent Trainer-life correction
moves it to `D6D3F9ED…D493` (164,780 bytes): only the authored Flyby and spawned
AirTrainer `initialHealth` fields change from 0 to 3000. Both read Air Trainer's
physics field 3 (`0x40400000`, 3.0), which Unit Init copies to instance life.
The materializer and decoder enforce this identity; historical receipts retain
their original pins. Initial life does not establish aircraft vulnerability or
complete damage/death behavior.

MEASURED from each mesh's own `MSHT`/`TEXB` records: `m_FA_F24_training.msh.aya`
names `meshtex\f_pulsetank_training.tga` and `meshtex\Chrome3.tga` with TEXB
metadata byte-identical to the Target Tank's (Chrome3 strength `0x3E4CCCCC`,
zero offset, unit scale) and emits the Target Tank's single material group
verbatim, so it needs **no new texture**. `m_f_lifter.msh.aya` names
(`f_lifter02`, `Chrome3`, `f_lifter01`, `Chrome3`) — note the inversion, slot 0
is `lifter02` — and emits two groups against the same Chrome3 reflection, which
is already materialized as `StaticWorld/Textures/meshtex-chrome3.texture.aya`.

`particle-alparticle5-additive` is now an ordinary `DIRECT_ASSETS` retention for
the released Pulse Cannon muzzle flash. `particle-fireball-additive` is not a
second producer output: its exact retail source is already retained above as
`target-tank-explosion-fireball.texture.aya`.

## Complete Level 100 audio contract

The accepted Level 100 mission/HUD message table contains 51 unique numeric
identifiers. The retained English Ogg files are exactly that set, including both
dodge outcomes, success/aborted and failure branches, friendly-fire/help lines,
`HUD_08`, technician 02/03, and the movement, weapon, overheat, ammunition, and
water callouts; unused alternate tutorial takes are not materialized. The
mission lane's typed `Level100MessageRequested` events own identity, ordering
gates, expected playback ticks, branch selection, and objective progression.
`InteractiveSession` delivers the initial batch and every fixed-step batch once,
in order, through `FrameAdvanceResult`. The audio adapter queues every numeric
event, including duplicates, with the released six-tick inter-message handoff;
none of those mission fields are duplicated in audio state, and the queue never
advances a mission continuation. `CharacterMessagePlayback` exposes only active
message identity and presentation playback position/length for HUD lip and page
synchronization.

Level 100 selects `MUS_TUTORIAL`. Stuart's alphabetically sorted playlist and
zero-based `GetSong` resolve its track index `3` to the fourth entry,
`BEA_04(Master).ogg`. Selection playback repeats the same track at completion.
It uses the separate released music-option curve, continues while sound samples
are paused, and stops at the level-exit boundary.

The shipped `sounds.sfx` version-103 descriptors provide each retained PCM's
effect volume, positive pitch-variance range, loop flag, and language flag.
Stuart's sound manager independently establishes the `0.70` ordinary-effect,
`0.45` HUD, and PC `0.42` radio-message call volumes; random pitch is
`1 + rand() % variance / 100`. `Level100Audio` applies that bounded contract
plus the PC nonlinear master-option curve and externally supplied game-sound
mix. The owner of a fade or duck supplies its current value; the adapter does
not schedule it. Exact DirectSound attenuation remains outside the
reconstruction. The three front-end effects use the startup/menu lane's
canonical ignored paths and hashes rather than a second Level 100 copy.

The released front end exposes only Back, Move, and Select effects. Select is
also the confirm sound that survives the loading transition; there is no
separate loading sample. Gameplay pause freezes all current sound-manager
samples, including any current frontend sample. Frontend/pause-menu cues created
after that pause remain live, and tutorial music continues. Level exit stops all
samples and music, then the frontend owner emits Select when its navigation
transition requires it. This preserves Stuart's `KillAllSamples` then
return/restart cue ordering without making audio own a frontend state.

The copied `default physics.dat` assignments narrow actor audio further:

- Air Trainer owns `Forsetti Fighter Flyby 02`; the U-17 transporter owns
  `Bomber Flyby 03`.
- Target Drone has no engine-sound assignment. Its Vulcan uses `Blaster 2`, its
  destruction uses `Explosion Small`, and both its and the trainer's missile
  launch modes omit a launch sound.
- Both `Forseti Missile` and the player's `Micro Missile` resolve through
  `Micro Missile Hit` to `Explosion Medium`; `MissileImpact` shares that exact
  retained PCM without duplicating an asset.
- Target Tank and Target Truck use `Tank Explosion Medium`; Level 100
  facilities use the medium-building explosion; the Battle Engine uses the
  huge explosion.
- The repair pad owns an idle loop, charging prefire effect, and full/heal
  launch effect.

The adapter exposes `PlayFrontendCue`, numeric `QueueCharacterMessage`,
ordered Aquila-flight and destruction-event consumers, `PlayTerminalCue`,
explicit trainer/transport/repair loop setters, sound/music option inputs,
`SetGameplayMix`, `SetGameplayPaused`, and level-exit stop ownership. Mission,
native flight/weapon, dynamic-actor, impact/destruction, frontend, pause, and
result states remain owned by their source lanes. The active Aquila emitter is
selected by the canonical `Player 1` Battle Engine ActorId and follows its full
three-dimensional registry pose; destruction samples use their typed event
positions. The presentation infers no cue from snapshot deltas and owns no
mission progress, waits, objectives, frontend pages, or pause actions. Retained
warning, Pulse-launch, missile, trainer, transport, repair, and debris cues stay
silent until their canonical source events are integrated.

## Heightfield consumed by the slice

The retained `HFLD` is the smallest exact terrain input used by Core and the client. It
comes from `100_res_PC.aya` → `ERES` → `ENGN` → `MAP!` and contains a
5,084-byte `CHFD` metadata block followed by 663,552 bytes of signed 16-bit
`HFDT` samples. The released loader at `0x0047F750` reads 64×64 tiles of 9×9
samples. The Godot client uses the released 65×65 eight-step lattice for coarse
selections and camera-selected 4/2/1-step tile grids at the `CHFD` scale
`0.0009155832231044769`. It uses the recovered midpoint-error score, projected
distance thresholds, released triangle diagonal, and all 16 edge-stitch index
variants. Eight-step tiles cover every rejected selection so the mesh cannot
develop holes.

The terrain mesh is translated so the authored player-one start
`(288.6875, 243.25, -10)` is the reconstruction origin. BEA's
`(X, Y, Z-down)` coordinates map to Godot `(X, -Z, -Y)`. Steam's sampler at
`0x0047EB80` converts coordinates to 24.8 fixed point and truncates signed
bilinear interpolation after each axis. Core now owns that exact sampler and a
hashed player-ground elevation; Godot consumes the snapshot instead of
independently lifting the player. At the start it produces HFLD unit `-11153`,
ground `-10.211499`, and the copied-retail Battle Engine center
`Z=-12.111499` after the released 1.9-unit center-of-gravity offset. Two later
points on the repeated forward route likewise matched units `-11161` and
`-11469`. The observed route held zero vertical velocity, zero pitch/roll, and
no steep-slope flag, so steep-slope sliding and terrain-aware body tilt remain
unimplemented rather than inferred.

## Terrain appearance and environment consumed by the slice

The materializer retains Steam's five selected `MAPT` sources from mixer set 10:
six indexed materials and palettes at each of widths `16/32/64/128/256`, all
4,096 variable-length `MMAP` material/weight records, and the 512×512 lighting
mask in the released Level 100 archive. It follows the gradient builder at
`0x0047E8E0`, the load tail at
`0x0047F932`, and blend path at `0x0047EFF0`. Before packing RGB565 it also
applies all 30 initially active `SSHD` structure-shadow owners and then processes
all 1,481 `pinesnow` placements through the exact `DMKR` shadow-stamp rules from
`data/resources/base_res_PC.aya`. The exact initial Level 0 result remains
independently verified by its RGB565 hash. The compact ignored hierarchy payload
retains the sources needed to repaint all five logical caches without duplicating
the retail archives.
The released 20-byte terrain vertices contain position plus repeated landscape
coordinates, with no normal or diffuse-color channel; the prelit macro texture
therefore owns the terrain's base illumination and the client does not invent a
separate normal-lighting pass.

The `CHFD` detail selector is `0`, which the released loader formats as
`mixers\detail00.tga`. `CDXLandscape__RenderTerrain` at `0x00545590` maps the
macro texture once across the 512-unit landscape, maps that exact RGB detail
texture once per world unit, and applies it again through the released
quarter-scale one-radian rotation plus `(0.3, 0.3)` offset. In the released
Level 100 render path, the wrapping macro and first detail stages use plain
`D3DTOP_MODULATE`; the cloud-shadow and rotated second-detail stages use
`D3DTOP_MODULATE2X`. The exact cloud-shadow texture repeats every 256 world
units. Its static
increments are `(0.001, 0.0005)` per retail renderer-time unit; an uninterrupted
copied-runtime sample measured `(0.01993, 0.00996)` texture cycles per wall-clock
second, represented as `(0.02, 0.01)` against Godot's seconds-based `TIME`. The
active Steam state uses anisotropic minification for the root cache, but each of
its five logical landscape levels is a separate one-level 512×512 RGB565 cyclic
cache—not a hardware macro mip chain. Their absolute-coordinate spans are
`512/256/128/64/32`. After the released `0.03` camera smoothing, selection uses
the root beyond 128 units, then forward-shifted rings with thresholds
`64/32/16` and shifts `60/28/12`; the innermost ring owns Level 4. The client
repaints the selected cache slots from the retained fixed-point compositor inputs
and passes each vertex's absolute landscape coordinate and logical cache owner to
the material. Exact stateful gamut clipping and bounded patch-pool reuse order
remain unclaimed.

The `CHFD` also selects cube 25 and supplies the fog color/density, sun,
anti-sun and ambient colors, and sun vector. The five exact DXT1 cube textures
use the released formatter order `cent`, `up`, `right`, `down`, `left` and the
20 released sky vertices. The sky is camera-centered and excluded from scene
fog, matching the released no-depth world backdrop rather than treating it as
distant terrain.

This establishes the authored macro material layout, repeating terrain detail,
moving cloud-shadow stage, and environment inputs, not whole-scene pixel parity.
Terrain-damage and other post-load overlay updates, and the separate visible-sun
particle, are not implemented; initial structure and pine shadow stamps are.

## Authored placement consumed by the slice

The released `data/resources/100_res_PC.aya` archive has SHA-256
`ED6350C0E214D00AB1BF6A7BD137FBA3E77D0AFE19A6DC4C0607F56AC037496A`.
Its version-50 `WRES/WRLD/BSWD` base-world stream contains 35 unit records: 33
visible static objects and two nonvisual markers. The materializer preserves
all 33 object definitions, positions, Z values, and yaws in its exact ignored
manifest. The same stream contains 753 `fernsnow` and 1,481 `pinesnow` records;
the Steam loader deliberately skips `fern*`/`bush*` groups, so the client
instantiates only the 1,481 pines. Steam does not explain those trees with one
all-distance billboard owner. The static ownership trace is:

- `CRTTree__Init` at `0x004DD7B0` retains both the selected `pinesnow0..3`
  `CMesh` and a six-view `CImposter`. `CRTTree__VFuncSlot02_BuildRenderOutputs`
  at `0x004DD960` submits the full mesh when squared horizontal camera distance
  is at or below `g_MeshQualityDistance²`, or while the tree is falling. The
  image initializes that option-backed global to **`30.0`** (`.data`
  `0x006321A0`, file `0x2321A0` = `0x41F00000`), and that is the value manifest
  v7 now selects — **corrected from `70.0` on 2026-07-27 under GOAL.md's
  defaults rule.** The "Geometry detail" setter at `0x004DD6B0` has exactly
  three arms writing `10.0` / `30.0` / `70.0` to that global, and the image's
  companion initializers (LOD bias `0x00631E88`, file `0x231E88` = `1.0`;
  quality scale `0x00630E0C`, file `0x230E0C` = `1.0`) pick out the middle arm
  uniquely. Boot-time
  `CCareer::Load(flag=0)` then overwrites it from `defaultoptions.bea`
  OptionsTail `+0x0C`, which is **persisted user state, not shipped data**: this
  machine's snapshot stores `70.0` (`0x428C0000`) at file offset `0x26CA` while
  `proof_defaultoptions.bea` from the same install stores `30.0` there, and no
  `.bea` appears in `INSTALL.LOG`. A separate capability branch at `0x004DD832`
  can write `45.0` when `[0x00662F10]` is non-zero; that global is statically
  zero and no writer to it was found by a literal-operand scan, so the branch is
  not reconstructed here.
- Outside that boundary, `CRTTree__VFuncSlot03_UpdateVisibilityState` at
  `0x004DD850` queues `CDXEngine__RenderImposterBillboardSet` at `0x00543300`.
  That helper emits all six `VIEW` records for the tree, not one chosen card:
  four successive vertical faces at approximately 90-degree rotations and two
  faces tilted by `+π/2` and `+3π/2`. Each half-extent is multiplied by `0.99`.
- After the normal world and global-imposter passes, `CDXTrees__Render` at
  `0x0055AA10` draws a separate fast-tree batch. Its primary card uses one of
  `VIEW` 0..3. The unlabelled CTree virtual target at `0x004F6540` returns
  `(tree_object_address >> 4) & 3`; placement ordinal is not an input. Its
  secondary horizontal card uses `VIEW` 4 and a half-size equal to the selected
  standing frame half-width times `0.7`. That secondary batch is drawn only when
  absolute camera-height versus sampled-ground-height delta exceeds `20.0`.

Both card owners add the mesh's final global-BBOX center to the tree position.
The exact BEA `(X,Y,Z)` centers for `pinesnow0..3` are
`(-.024962962,.000355244,-.886774659)`,
`(.070154905,-.082703590,-.911682606)`,
`(.018942535,-.120034099,-.914225817)`, and
`(.047512651,.026186585,-.814044118)`. The fast primary buffer stores signed
half-width/half-height lanes that the tree shader expands around this center as
a camera-facing vertical card. Its secondary buffer writes a literal X/Y square
at the center's Z. The general imposter helper instead transforms right/up basis
vectors and constructs each face as `center ± right ± up`.

The exact 1024×256 BC2 `Imposters_100` atlas has SHA-256
`7368BA0C586221FF1B1572CEE8F84DE2BF6DB426C005A73A10BAD54A938AD882`.
Every serialized view occupies a 32×32 cell: all V ranges are `[0, 0.125]`,
each U interval below is 0.03125 wide, and the pairs are half-width ×
half-height.

| Mesh | `VIEW` U intervals 0..5 | Half-extents 0..5 |
| --- | --- | --- |
| `pinesnow0` | `[0,.03125]`, `[.03125,.0625]`, `[.0625,.09375]`, `[.09375,.125]`, `[.125,.15625]`, `[.15625,.1875]` | `.783503950×.976928771`, `.699241579×.976928771`, `.783503890×.976928651`, `.699241519×.976928651`, `.783503950×.699241459`, `.783503950×.699241459` |
| `pinesnow1` | `[.375,.40625]`, `[.40625,.4375]`, `[.4375,.46875]`, `[.46875,.5]`, `[.5,.53125]`, `[.53125,.5625]` | `.650126278×.982503712`, `.743201494×.982503653`, `.650126219×.982503593`, `.743201494×.982503653`, `.650126278×.743201435`, `.650126278×.743201435` |
| `pinesnow2` | `[.5625,.59375]`, `[.59375,.625]`, `[.625,.65625]`, `[.65625,.6875]`, `[.6875,.71875]`, `[.71875,.75]` | `.815672159×.995326340`, `.781502008×.995326340`, `.815672100×.995326221`, `.781501949×.995326221`, `.815672159×.781501889`, `.815672159×.781501889` |
| `pinesnow3` | `[.1875,.21875]`, `[.21875,.25]`, `[.25,.28125]`, `[.28125,.3125]`, `[.3125,.34375]`, `[.34375,.375]` | `.899441719×.888172686`, `.889750004×.888172686`, `.899441659×.888172567`, `.889749944×.888172567`, `.899441719×.889749885`, `.899441719×.889749885` |

The close owner uses the ordinary world-object fixed-function path: alpha test
is enabled with reference `8` and greater-or-equal comparison; stage zero uses
linear magnification, anisotropic minification, linear mip filtering, maximum
anisotropy `4`, and LOD bias `-1`. Its vertex lighting is ambient plus
directional sun and opposing anti-sun, followed by base `MODULATE2X` and the
active CHFD fog. The global six-card pass switches min/mag to point while
retaining the restored linear mip filter and alpha reference `8`. `CRTTree`
selects that helper's secondary buffer, whose lighting flag is off and whose
white factor feeds `MODULATE2X` before fog. The fast batch uses point min/mag,
disables mip filtering, and keeps reference `8`. Both atlas paths retain the
default wrap address mode. A copied Steam read found `0x008554FC = 1`, selecting
the fast batch's same unlit white-factor `MODULATE2X` branch before fog.
Independent BC2 decoding found only `0` and `255` alpha in the atlas.

The four close meshes are not cards: their exact converted topology is
`674/499`, `411/270`, `586/396`, and `598/396` vertices/triangles for
`pinesnow0..3`. They select three exact 256×256 BC2 snowy bark/needle textures.
The authored placement data also proves overlap: only 15 pine owners lie inside
the **selected 30-unit Medium-detail** boundary at player start, while 616 lie
inside the 70-unit boundary this reconstruction used before 2026-07-27. The
nearest six form a cluster only 3.51–5.21 units from the start.

> **Corrected 2026-07-28 — labels only, both counts unchanged.** The two
> sentences above previously read "only 15 pine owners lie inside *the proof
> profile's* 30-unit boundary at player start, while 616 lie inside *the
> selected* 70-unit high-quality boundary". That called `70.0` the selected
> value after the 2026-07-27 defaults-rule correction had already moved the
> manifest to `30.0` — see the `CRTTree__Init` paragraph above. MEASURED: both
> counts were re-derived for this correction and neither moved — 15 pine owners
> within 30 units and 616 within 70, computed over the 1,481 `pines` entries in
> `StaticWorld/level100-static-world.json` against the player start
> `(73_904/256, 62_272/256) = (288.6875, 243.25)`
> (`../../../OnslaughtRebuild.Core/Level100Terrain.cs:20-21`), whose six nearest
> distances are `3.51/4.28/4.35/4.39/4.73/5.21`. What changed is which of the
> two numbers the reconstruction actually uses.

One retail tree contributes either mesh or
six-face imposter work depending on range, plus the separate fast-card pass;
nearby tree owners can also overlap in the image.

The current slice preserves those owners separately. It renders the exact four
meshes at or inside the manifest's selected **30-unit** cutoff and all six fixed
imposter faces outside it, then adds the camera-facing fast standing card for
every placement and the `VIEW`-4 horizontal card only above the strict 20-unit
camera/ground delta. The static-world manifest (schema
`onslaught.level100-static-world.v14`) explicitly pins fast-standing-view
reconstruction phase `0`; the client maps placement ordinal plus that phase
across `VIEW` 0..3 and checks all 1,481 assignments and their
`371/370/370/370` counts. This is a deterministic reconstruction choice, not a
claim about Steam owner identity. Steam's exact tree allocation/view sequence
and the two-bit phase consumed by its address-derived selector remain the
precise unresolved runtime boundary.
The height gate adapts the retained Level 100 HFLD ground sampler for the
released `CStaticShadows` query; exact `CStaticShadows` interpolation has not
been independently equated to that adapter. Falling-tree retention,
`CDXTrees::HideTree`, other user-selected quality distances, and pixel-level
fixed-function/Godot sampler equivalence remain outside this slice.

> **Corrected 2026-07-28 — two corrections to the paragraph above.**
>
> 1. It previously read "renders the exact four meshes at or inside the
>    manifest's selected **70-unit** cutoff". The manifest selects `30.0`, and
>    has since the 2026-07-27 defaults-rule correction described in the
>    `CRTTree__Init` paragraph. MEASURED: `pineBillboards.meshQualityDistance` in
>    `StaticWorld/level100-static-world.json` reads `30.0`; the client consumes
>    that field rather than a constant
>    (`../../../OnslaughtRebuild.Godot/Level100StaticWorldAsset.cs:129` and
>    `:411`), and
>    `../../../OnslaughtRebuild.Client.Tests/Level100PineRepresentationTests.cs`
>    asserts the manifest's bits equal `0x41F00000`, which is `30.0f`. The
>    sentence stated the reconstruction's own cutoff and stated it wrongly; this
>    is exactly the lab value that the project's defaults rule (no lab-derived
>    value becomes an authored default) exists to keep out of authored behaviour.
> 2. It previously read "**Manifest v7** explicitly pins fast-standing-view
>    reconstruction phase `0`". There is no manifest v7 — it existed briefly and
>    was superseded seven revisions ago. The claim itself is unchanged and still
>    true: `fastStandingViewPhase` reads `0`. Only the version label was stale,
>    so the text now names the schema string, which is greppable when it next
>    moves. MEASURED: the manifest declares
>    `onslaught.level100-static-world.v14`, and three tracked consumers reject
>    anything else —
>    `../../../OnslaughtRebuild.Client/Level100ActorDefinitionManifest.cs:39`,
>    `../../../OnslaughtRebuild.Godot/Level100StaticWorldAsset.cs:727`, and
>    `../../../tools/materialize_retail_assets.py:1093` and `:3057`.

The 33 static records select 24 mesh types: nearby facilities and turrets,
houses, city/tall buildings, an airfield, docks, hangar, radar, solar pod, and
four iceberg types. Every instance is placed through the common retail
`(X, Y, Z-down)` to Godot mapping. Existing collision remains intentionally
limited to the separately observed Control Tower and Tank Factory envelopes.

The Federation base turret in the supplied comparison is the WRES object named
`Turret 03`, not a Firing Range Target Tank. Its object ordinal is `3`, WRES
thing type is `8` (`CUnitInitThing`), definition is exactly `SAT Turret`, and
its zero-based `default physics.dat` Unit-statement index is `58`. The serialized
behavior child `5` resolves through the released factories to behavior `4` and
`CCannon::Init`. WRES authors position `(252.5, 261.25, -0.0)` with zero
yaw/pitch/roll. Its exact `m_ft_sam.msh.aya` conversion has 16 parts:
`base -> turretbase -> support -> barrel -> Emit01..08`, with `Emit09..12`
parented directly to `base`. The converted vertical bounds are
`-0.22822660952806473..0.7794696986675262`, so the mesh lower edge is
`0.22822660952806473` below its authored pivot.

Stuart's `CThing::Init` clips that authored pivot through `MAP.Collide`, then
through `MAP.GetWaterLevel` for things that cannot go underwater. The Steam
body at `0x004F34A0` follows the same order: the `CCannon` vtable's `+0xB0`
slot returns `1`, the released HFLD sampler is called at `0x0047EB80`, and its
`+0xC4` slot returns `0` before the water comparison against `0x006FBDFC`.
At this X/Y, HFLD unit `-10485` produces terrain Z
`-9.599889755249023`; water Z is `-8.84000015258789`, so the released initial
transform is position `(252.5, 261.25, -9.599889755249023)` with identity
orientation.

**The seating law, recovered and applied uniformly.** `CThing::Init` seats a thing
at `max(authored Z, terrain, water)` and reads **no mesh extent whatsoever**. Both
clamps are bare `FSTP` stores of the compared value — the terrain clamp at
`0x004F3529` (a float store to stack local +0x14, fed by the sampler through
an indirect call through vtable slot 20) and the water clamp at `0x004F3559`
(a float store to field +0x24 of esi). Nothing between the sample and the store adjusts
for geometry.

So the reconstruction applies one rule to **all 33 authored statics and all 1,481
pines**: seat at `max(PlayerStartElevation - retailZ, terrain, water)`, with no
mesh-extent term. `Level100StaticWorldAsset.Load` implements it and
`Level100PineRepresentationTests.NoMeshDerivedTermIsAddedToTheReleasedStaticClamp`
pins it. `SAT Turret` is **not special-cased** — the authored skirt sits below
terrain because the authored pivot does, which is what the released law produces
for every static, turret or otherwise.

Pines inherit the same law by direct evidence rather than by generalisation.
`CTree::Init` at `0x004F6080` is RTTI-confirmed: the `CTree` vtable `0x005DD9D8`
carries it in slot `+0x24`, and the complete object locator at `0x00615630` names
`.?AVCTree@@`. It **overwrites the authored Z with the height sample** before
seating — an address computation (EBX+4) into edx at `0x004F61C4`, a load of constant 0x006FADC8 into ecx at `0x004F61C7`,
a direct call to 0x0047EB80 at `0x004F61DD`, a float store to field +0xC of ebx at `0x004F61F4` — and
there is **no `FADD`, `FSUB` or `FMUL` between that call and that store**; the
window holds only `lea`, `mov`, `mov`, `push`, so the sampled height reaches the
field unmodified. It then calls `CThing::Init` at `0x004F6363`. Both gates are
inherited and both fire: vtable `+0xB0` `ClipToGround` → `0x004014A0`
(a load of constant 1 into eax, then return) and `+0xC4` `CanGoUnderWater` → `0x00405930`
(a zeroing of eax, then return), so the water clamp applies to pines too.

> **Superseded 2026-07-29.** This paragraph previously read: *"The client therefore
> places every object whose definition is exactly `SAT Turret` with its pivot on
> that support (Godot Y `-0.40011024475097656`), preserving the authored lower
> skirt below terrain. The other static types retain their existing converted
> lower-bound clearance; their individual released grounding relationships are not
> generalized from this turret."* Both sentences described behaviour the code no
> longer has. The `-min(vertexZ)` lift was removed from all 33 statics when the
> seating law was recovered, and has since been removed from the pines — the last
> place it survived. The old caution against generalising from one turret is
> overtaken: the law is now read directly from `CTree`'s own body.
>
> All byte evidence above is from the pristine specimen
> (a private pristine copy), sha256
> `74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750`, and every
> address was re-disassembled with `tools/disasm_va.py` when this correction was
> written. Never read these bytes from the installed Steam `BEA.exe`, which is
> deliberately patched.

Level 100's `HFLD` selects water level `-8.84000015258789`, color `#21213D`,
and texture index zero. The active Steam path renders a 25×25 camera-following
grid with two animated `caustic00` stages, authored `reflection00` imagery, and
the exact `sunreflect`/`sunblob` stages. Steam disables texture stage 3 before
the main grid draw; the released wave operations apply only to the authored
shoreline passes. The sun textures shape an
alpha-tested `#E8E8FF` patch scaled from camera height. The nested `SURF`
payload supplies 514 three-contour records; the client reproduces the first
shore pass and the later unfogged `SRCALPHA`/`ONE` wave pass in released order.
Controlled copied-runtime observation measured the caustic phase at `1` radian
per second and both wave scrolls at `0.06` texture cycles per second. The
optional advanced-water path remained inactive, so this implementation does
not claim dynamic scene reflection/refraction.

The PC renderer uses packed ambient plus directional sun and opposing anti-sun
from `CHFD`. Directional channels are divided by 256 and the base texture stage
uses `MODULATE2X`; the client reproduces that fixed-function equation for the
current static meshes, Aquila exterior, and range targets. It preserves
all six `TEXR` slots and implements the active released DOT3-lighting,
camera-space reflection, and alpha-overlay passes with each texture's serialized
`TEXB` parameters. Released material modes disabled by the live Level 100
renderer remain disabled.

The slot-to-mode mapping is not encoded in the serialized material record; the
slot ordinal *is* the mode. `CDXMeshVB__Load` (`0x0054E160`) reads the six
`TEXR` words into the group record at `+0x20`..`+0x34`, storing `0` only for
the literal `0xFFFFFFFF` and otherwise `texture_table + index * 0x24`. The
`0x24` stride is the serialized `CMST` per-texture entry, whose observed layout
on every Level 100 mesh is two pointers, a frame count of `1` at `+0x08`, five
frame-track pointers at `+0x0C`..`+0x1C`, and a static strength float at
`+0x20`. Those five tracks are the five `TEXB` floats in order: strength,
offset U, offset V, scale U, scale V; the `+0x20` float equals `TEXB[0]` for
all 154 texture records in the corpus. `CMeshRenderer__RenderMeshWithLayerPasses`
(`0x0054D530`) then writes the layer ordinal to `CTexture +0x88` and the
frame-sampled offset/scale to `+0x8C`..`+0x98` before calling
`CVBufTexture__RenderModePass` (`0x005588F0`), which switches on `+0x88`. Each
layer is its own `DrawIndexedPrimitives` over the same geometry, not an extra
sampler inside one draw.

Mode `1` sets stage-0 `COLOROP=D3DTOP_DOTPRODUCT3` (`0x18`) with
`ARG1=TEXTURE`, `ARG2=TFACTOR`, clears `D3DRS_ZWRITEENABLE`, and publishes the
model-space light vector through `D3DRS_TEXTUREFACTOR`. It does not set
`D3DRS_ALPHABLENDENABLE`, `SRCBLEND`, or `DESTBLEND`, so its framebuffer
combination is inherited state and is not established by static evidence.

Mode disablement is serialized in the binary, not merely observed: the head of
`CDXMeshVB__Load` stores `0x02000000` at `0x009C63A4` and `0x00000200` at
`0x009C63A8`, so the six-byte per-layer flag array `0x009C63A4[0..5]`
initializes to `0,0,0,2,0,0`/`...,2` — layer `3` and layer `5` carry the
`2` ("disabled") value that the layer loop's `!= 2` guard rejects. Mode `5`
additionally returns false from `RenderModePass`, which gates the draw. Layers
`1`, `2`, and `4` start at `0` and are device-validated on first use. Each layer
is further skipped unless `round(global_alpha * strength)` — halved through
`(secondary_percentage * alpha) / 100` for layers other than `0` — exceeds
`10`; the four distinct Level 100 strengths `0.2`, `0.3`, `0.5`, and `1.0`
produce `51`, `76`, `128`, and `255`, so no Level 100 pass is gated out by
strength.

Measured slot occupancy over all 423 material groups of the 28 converted Level
100 meshes: slot `0` is occupied 423 times, slot `2` 397 times, slots `1` and
`3` three times each, and slots `4` and `5` never. The three slot-1/slot-3
groups are `f-city1` part 0 group 2, `fb_aircraft_factory` part 6 group 2, and
`FB_Solar_Pod` part 1 group 0; each carries the signature `(0,0,0,0,-,-)`, so
the base texture is repeated across the base, DOT3, reflection, and disabled
projected slots. They account for 8 of 268, 1 of 2,162, and 22 of 698 triangles
respectively — 2.99%, 0.05%, and 3.15% of their meshes, not whole buildings.
Every other group in the corpus is `(base,-,reflection,-,-,-)`. All 154 `TEXB`
records carry offset `(0,0)`; all carry scale `(1,1)` except `FB_Docks` texture
1, whose `(0,0)` scale reaches only slot 2, where mode 2 replaces the texture
transform with its own fixed reflection matrix.

The nearby compared facility is base-world ordinal 1, `Tank Factory`, backed by
`m_fb_tank_factory.msh.aya`. Its four material assignments are exactly
`(0,-,1,-,-,-)`, `(2,-,1,-,-,-)`, `(4,-,3,-,-,-)`, and
`(6,-,5,-,-,-)`: the ordinary base texture is in slot 0, the matching
`meshtex\Chrome3.tga` record is in reflection slot 2, and the other slots are
`0xFFFFFFFF`. Every Chrome3 `TEXB` record has strength
`0.19999998807907104`, zero offset, and unit scale. The valid texels in every
Chrome3 mip are opaque, and all 2,457 facility vertices have diffuse alpha
`255`. `CMeshRenderer__RenderMeshWithLayerPasses` (`0x0054D530`) therefore
rounds `255 * strength` to `51`; the binary initializes the secondary-pass
percentage to `100`, so `CVBufTexture__SetupSecondaryBlend` (`0x00558EF0`)
publishes `D3DRS_TEXTUREFACTOR = 0x33FFFFFF` on this path.

Mode 2 is a later draw of the same geometry, not an emissive texture mixed into
the base draw. It retains stage 0 `COLOROP=MODULATE2X` with
`TEXTURE,DIFFUSE`, stage 0 `ALPHAOP=MODULATE` with `TEXTURE,DIFFUSE`, and then
uses stage 1 `MODULATE` with `CURRENT,TFACTOR`; the white texture-factor RGB
leaves color unchanged while its byte alpha scales source alpha. The framebuffer
blend is `SRCALPHA,INVSRCALPHA`. In normalized notation, the compared facility
therefore uses `Cr=saturate(2*Tchrome.rgb*Dlit.rgb)`,
`Ar=Tchrome.a*Dlit.a*(51/255)`, and
`C=Ar*Cr+(1-Ar)*Cbase`. Texture-stage results saturate to `[0,1]`; the
application-provided factor is exactly 8-bit, while further internal combiner
fractional precision is device-owned rather than encoded by BEA. The client
accordingly quantizes the factor and saturates the operation without inventing
additional byte-rounding steps.

`CVBufTexture__RenderModePass` (`0x005588F0`) selects
`D3DTSS_TCI_CAMERASPACEREFLECTIONVECTOR`, `COUNT2`, and the released matrix
`diag(0.5,-0.5,1,1)` with translation `(0.5,0.5,0)`, producing
`(0.5*R.x+0.5,-0.5*R.y+0.5)` from the per-vertex camera-space reflection
vector. The draw inherits the world stage-0 sampler: wrapping U/V, linear
magnification and mip interpolation, anisotropic minification capped at 4, and
mip LOD bias `-1`. The D3D9 fixed-function path has no sRGB texture read/write state, so its
texture arithmetic is on encoded channel values; `retail_output` performs only
the transfer required by Godot's active output contract. Released exponential
fog is applied after the texture cascade and before target blending. Applying
that same fog once after the client's source-alpha composition is algebraically
identical for these coplanar passes because both draws share depth, fog factor,
and fog color.

The Core origin is the released player-one start `(288.6875, 243.25)` in the
world's horizontal X/Y plane. The current slice consumes:

| Thing | Relative X/Y | Retail yaw or radius |
| --- | --- | --- |
| Player-one start | `(0, 0)` | yaw `0.509829998` |
| Target Zone 1 | `(-43.1875, 33.5)` | radius `5` |
| Firing Range | `(-69.6875, 72.75)` | radius `5` |
| Target Tank 1 | `(-67.76434, 78.28299)` | yaw `-0.0523363` |
| Target Tank 2 | `(-78.75, 80.0625)` | yaw `-2.1535792` |
| Target Tank 3 | `(-71.875, 84.6875)` | yaw `2.4043305` |
| Target Warehouse | `(-86.3125, 83.5625)` | yaw `-1.9708606` |

One no-input control and two fresh, uninterrupted fixed-yaw forward holds per
facility establish the two route contacts consumed by Core. Steam repeated a
`2.5736`-unit centre separation at the Control Tower while removing inward
velocity and retaining tangent motion, so the walker visibly slid around it.
At the Tank Factory, transient samples reached `8.3586..8.4267` units before
the head-on response settled repeatedly at `8.4333` with zero planar velocity.
Both observations held raw walker state `2`, stable yaw, and the expected
`0.15`-unit released update speed before contact. Core rounds the stable
envelopes to `2.574` and `8.434`; they include the released single-player
BattleEngine radius and are not general building bounds, arbitrary collision,
or destruction behavior.

Two fresh uninterrupted read-only runs repeated Steam's six-second full pan.
`CPlayer__GotoPanView` (`0x004D2C10`) transforms local camera points
`(0,10,-4.3)`, `(5,0,1.3)`, `(0,-9,-1.3)`, and `(0,-2.5,0)` by the stationary
Aquila orientation and passes them to its order-three clamped quadratic spline.
Both runs began at `(283.807220, 251.978271, -16.411499)`, handed off from the
pan camera to the first-person camera after 5.95 seconds, and entered playing
state after six seconds. The presentation consumes that path and keeps the
exterior Aquila visible while the pan camera suppresses the cockpit and HUD.
After the handoff, five uninterrupted read-only samples held the same
first-person `CThingCamera`, position, yaw, and horizontal forward column
`(-0.488029, 0.872827)`.

One idle control and two fresh repetitions at that same start bound vertical
aim without moving the Aquila. Steam stored pitch and its inertial velocity on
the player-one BattleEngine rather than on an independent camera: the first
input was exactly `1/117` radian, coast retained `0.8`, and repeated held-input
endpoints were `+0.5321228` and `-1.0911411..-1.0912496`. Two player-owned
Pulse Cannon rounds then repeated the crosshair-derived unit direction within
`0.00119` per component. These bounds apply to the authored start slope;
terrain-relative limiting, mouse inversion and sensitivity settings other than
the copied test setting `1.5`—which is below the retail slider's selectable
`{3, 6, …, 63}` values—auto-aim, and
vertical target collision are not claimed.

`TargetZone1.msl` and `FiringRange.msl` each request a 0.5-second wait before
posting their event. `LevelScript.msl` activates Target Zone 1 first, then makes
the Firing Range the objective after `Reached Target Zone 1`.

## First tutorial handoff

Core tick zero is the observed retail pan start at game time `3.0`. Two fresh,
uninterrupted app-owned Level 100 runs repeated the following message boundaries
within one 50 ms retail sample; the retained intervals are half-open:

| Message | Core ticks |
| --- | --- |
| HUD introduction | `182..351` |
| Threat circle | `357..567` |
| Scanner | `573..756` |
| Message log | `762..926` |
| Technician status | `932..998` |
| Movement controls | `1004..1220` |
| Reach Target Zone 1 | `1226..1387` |
| Objective scanner | `1393..1530` |

The released Battle Engine power flag at offset `0x580` changed from `0` to `1`
at Core tick `1000`; its flight flag at `0x58C` and both initial weapon gates
remained disabled. At tick `1223`, the unique object at Target Zone 1's authored
position changed its `CThing` flags at offset `0x2C` from `0x0002` to `0x0022`,
setting the released objective bit `0x20`. The current slice consumes those
gates, exact English text, and exact voice clips.

Two later uninterrupted runs delivered the same copied `Movement/Left` then
`Forward` input to player one. Target Zone 1's objective flag remained set
until its radius-5 volume overlapped Steam's single-player Battle Engine radius
of `0.4`; the last outside/first inside samples were `5.44/5.29` and
`5.54/5.39` world units. Both runs then atomically cleared Target Zone 1,
marked the Firing Range, and installed message ID `4458134` (`TUTORIAL_02`)
after the same 11 released 20 Hz updates. Core maps that observed dispatch to
10 ticks at 20 Hz. The client uses the exact 5.393900-second English voice,
subtitle, and shipped 16x16 objective marker with the released radar transform.
One clean control and three fresh uninterrupted Firing Range runs then used a
predeclared read-only observer over Steam's objective list, HUD weapon state,
player/weapon gates, and message ID. All three accepted runs repeated the same
sequence: the range objective cleared; the player deactivated; `TUTORIAL_03`,
`HUD_05`, `TUTORIAL_PULSE_CANNON`, `TUTORIAL_OPEN_FIRE`, and
`TUTORIAL_PULSE_CANNON_2` appeared in script order; four exact target pointers
became objectives at Open Fire; and one second later the player and Pulse
Cannon alone reactivated. The copied `Fire` binding changed the live selected
weapon state, independently proving that input reached player one. Runtime
positions and vtables identified three `m_f_pulsetank_training.msh` targets and
one `m_m_warehouse.msh` target at the coordinates above.

The sequence in Core preserves the released script's explicit one- and
two-second pauses, exact Ogg lengths, and the message post-roll/handoff already
established by the opening tutorial. It does not convert variable wall-clock
memory-scan latency into simulation timing. The exact overlap-to-Firing Range
event boundary was not separately sampled, so its 0.5-second dispatch remains
the released script delay rather than a new runtime measurement.

Two fresh app-owned copies isolated the Warehouse with the exact compiled
LevelScript count byte changed from `4` to `1`. Twelve normal direct hits removed
its objective in both runs. Player power then dropped to zero; after the
one-second script pause and exact `tutorial_vulcan_cannon.ogg` duration, Steam
reactivated the player with Pulse disabled and Vulcan enabled while adding three
already-moving Target Truck objectives. Core consumes the weapon/message gate
and released `FollowWaypointWait` commands through the materialized paths and
bounded unobstructed CGroundVehicle owner. Steam's occupancy/path-grid
adjustment and the exact trajectory/arrival tick remain unproven. Target Truck
contact/destruction and Vulcan behavior are not implemented.

A no-fire control and fresh isolated copied-runtime runs followed each of the
three Target Tank pointers and their player-owned normal rounds. Releasing at
the first active charge bucket (`10`) created definition-speed-`35` projectiles
that moved exactly `1.75` units per released 20 Hz update. Each tank began with
life `6` and no shield; direct mesh hits repeated the exact
`6 → 4.2 → 2.4 → 0.6 → -1.2` sequence, set the destroyed flag, and removed that
target from Steam's objective set on shot four. One separate glancing mesh-part
hit removed `1.0`. The released damage call receives a mesh-part index, so the
differing multiplier is not generalized. Core represents only the demonstrated
direct-contact path: speed `1750` millimetres per 20 Hz tick, ordered `0.8`
round and `1.0` immediate-explosion whole-body stores, the retained mesh's
rounded `1.45`-unit horizontal bound, and independent four-hit removal for the
three tanks. The exact explosion mesh-part carrier for segmented targets remains
open. A same-return capture of released
`CBattleEngine::GetLaunchPosition` resolved cockpit emitter `Gun` index 1 to
`-0.005619` right, `+0.080066` forward, and `+0.259300` up in the live
BattleEngine basis; Core consumes the rounded millimetre transform. The
speed-`35` record in the released physics data names `Mech Pulse Bolt Medium`.
Its five-entry particle descriptor references four unique textures:
Blue Spark 2, Blue Trail, Halo, and Energy Trail. The presentation uses those
exact archives with the descriptor's `0.25`-unit primary sprite radius,
`0.3`-unit halo radius, `0.25`-unit energy-trail radius, and `0.08`-unit trail
start width. The Pulse ribbon now retains five fixed-step positions and
reconstructs simulation updates skipped between rendered frames. Both Vulcan
rounds use the distinct released three-point `Bullet.tga` trail at `0.02`
total width instead of inheriting the Pulse sprites. Exact strip joins, caps,
UV scrolling, the emitted secondary sprite, pulsation, color evolution, and
pixel parity remain open.

The exact released `data/ParticleSets/MainSet.par` (SHA-256
`A51FE4419B55E1AF132E31C6B3CD8133C937745D8F4AB691EB5A0D81017DED06`)
names the retained small-impact and medium tank-explosion layers. The
presentation consumes the unambiguous bright sprite animation, scale, and
lifetime values directly.

`ParticleSets/` now holds all three shipped archives, materialized verbatim, and
`OnslaughtRebuild.Client.ParticleSetFile` decodes them at runtime. The in-level
sun is the first consumer: `Level100SunAsset` resolves the `Sun Sprite`
descriptor and draws it from the decoded texture, blend mode, radius and colour
range. The small-impact and tank-explosion presentations above still run on
constants transcribed from the same file rather than on resolved plans; moving
them over is separate work. Tank smoke's mode-1 blend cannot yet be reproduced
without an opaque card and is omitted, along with descriptor color ranges,
debris, wreck geometry, and the other subordinate emitters. The three PCM files
currently presented by the Pulse exercise were
decoded from exact `data/sounds/sounds_english_pc.xap` (SHA-256
`658C15E3BAB844D65DD3C07C4AC880F16F741C0EA116F48C603449BBD4DDA8B7`)
records 35, 106, and 102 respectively. Their `PSMP` names, declared decoded
sizes, high-nibble-first IMA-ADPCM framing, and resulting WAV hashes were
validated before retention; Godot validates the PCM envelope again at load.

## Dependency-inverted contact and destruction payload

`level100-contact-owners.json` schema v4 de-duplicates the 33 WRES instance
rows into 24 reusable static definitions and separately retains four target
definitions — Target Tank, Target Truck, Warehouse and Target Drone. Its 362
parts preserve names, hierarchy links,
float segment-value bits, BBOX metadata, and a deterministic
millimetre-quantized projection of transformed vertices and expanded triangle
strips.

> **Corrected 2026-07-28 — three superseded values, one unchanged.** This
> paragraph previously read "schema **v3**", "the **Target Tank and Warehouse**
> definitions" and "Its **349** parts". MEASURED by loading
> `../../../OnslaughtRebuild.Core/Assets/Level100/level100-contact-owners.json`:
> `schema = onslaught.level100-contact-owners.v4`, `partCount = 362`,
> `targetDefinitionCount = 4` with the four named `Target Tank`, `Target Truck`,
> `Warehouse` and `Target Drone`. The producer agrees at
> `../../../tools/materialize_retail_assets.py`, which emits
> `"schema": "onslaught.level100-contact-owners.v4"`. The schema bump and the
> two extra targets landed together in commit `9688ee0b` and this paragraph was
> not carried along. The de-duplication claim is **unchanged and still exact**:
> `instanceCount = 33` into `definitionCount = 24`. This file is regenerated by
> the materializer, so re-read it rather than trusting these numbers. BBOX is broadphase metadata only; Core reports actor hits from the
swept-sphere projected-mesh path. The pinned
medium-pulse radius is the released `0x3D8F5C29` float rounded to `70`
millimetres. This is a hash-verified deterministic Core input, not a claim of
bit-identical retail collision geometry.

Contact does not instantiate the retained WRES metadata. Simulation supplies
the canonical registry's stable actor ID, definition/mesh binding, active
state, full pose/basis, velocity, health and lifecycle. The per-actor
destruction component implements the Warehouse's extent-weighted segment
health, `5.0` core multiplier, active-intact initial-subtree sum and strict
`30%` terminal test. Steam's CRT-random child-cascade phase and debris bounce
remain unresolved, so Core emits only typed impact and terminal presentation
effects and supplies no synthetic cascade timing or trajectory.

The presentation consumes the exact facility and target texture-pass signatures
and preserves their mesh-group assignments. Higher-layer references follow the
released mode dispatch rather than being guessed as generic metallic maps. The nine retained
render meshes represent intact facilities and targets. All three Target Tanks
have the bounded damage/deactivation path above plus the retained shot, impact,
and medium-destruction sound and primary particle layers. Two fresh isolated
copied-retail runs required exactly twelve first-bucket direct hits to remove
the 28-segment Warehouse objective, then repeated the released Vulcan handoff.
That fixed-aim observation remains a bounded comparison; Core now contacts the
retained hierarchy through the deterministic projection and applies the
evidenced per-segment health and terminal rules. Godot presents Pulse impacts
and terminal destruction from ordered typed Core events, including events from
intermediate steps in a rendered frame. It does not yet hide detached segments
or present rubble/debris. Unmeasured mesh-part damage multipliers, the exact
Target Truck trajectory/arrival law, and Vulcan muzzle/impact presentation
remain open. Static targets and dynamically spawned Target Trucks are keyed by
canonical actor ID, select the exact visual through the actor definition/mesh
binding, and consume the canonical full pose on every rendered frame rather
than retaining authored static positions.
Actor/structure collision,
steep-slope response, facility animation, complete
world population, and complete Level 100 mission behavior are not established by
this slice. Objective markers use the shipped HUD asset; no synthetic target or
world-space beacon geometry is retained.
