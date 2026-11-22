# MW5 Variant Coverage — Which Shipped Units Can Be Rendered Today

Maps the 199 unit variants in `data/units/mechs` onto the variant assets that exist in *MechWarrior 5: Mercenaries*, to answer one question: **for which of our units can `assemble` + `render` run today, and where are the gaps?**

Date: 2026-11-22 · Scope: analysis only, no code or asset changes.

> Builds on [mw5-model-extraction.md](mw5-model-extraction.md) (extraction fact base, clip inventory, licensing) and [../architecture/asset-pipeline.md](../architecture/asset-pipeline.md) (stage 1 import → stage 2 render). The measurement itself comes from `artifacts/mw5/discovery-report.json`, produced by `MW5Extractor discovery-report` against a full base + DLC install on 2026-10-27.

---

## 1. What "available" means

Export is **per variant**, not per chassis, even though the mesh is per chassis:

| Stage | Needs | Granularity |
|---|---|---|
| `assemble <variant>` | chassis body mesh + cockpit + glass + `<VARIANT>_Loadout` + `<CHASSIS>_HPS` | **variant** |
| `clips <variant>` | `<Chassis>_Stand/WalkForward/RunForward/Falling_ANI` | chassis |
| `render.py` / `sheet.py` | the assembled `.glb` | variant |

So a variant is **available** only when MW5 ships its loadout data asset. A chassis that exists without that variant still yields a body, but the weapons come from a sibling loadout — the silhouette is renderable, the *variant* is not.

Three statuses are used below:

- ✅ **available** — variant id exists in MW5 (`<VARIANT>_Loadout` present); `assemble AS7-D` works as-is.
- ⚠️ **chassis only** — the chassis is in MW5, the variant is not; renderable by borrowing a sibling loadout or hand-writing `assembly.json`.
- ❌ **absent** — the chassis is not in MW5 at all; no route without commissioning or authoring a model.

## 2. Summary

| Measure | Value |
|---|---|
| Variants in `data/units/mechs` | **199** |
| Chassis in `data/units/mechs` | **76** |
| ✅ available in MW5 | **137 (69%)** across **53** chassis |
| ⚠️ chassis only | **15** across 9 chassis |
| ❌ absent from MW5 | **47** across **23** chassis |
| MW5 chassis with no MakaMek counterpart | 51 (Clan, Star League, DLC) |
| MW5 variant assets on our 53 matched chassis | 430 (⇒ ~293 art-ready variants we do not ship) |

All 137 ✅ matches are **exact id equality** — no fuzzy or suffix matching was used, so there are no false positives. Scenario-prefixed assets (`A2M2_BJ-1`, `DLC8_VND-1R`) were accepted as the same variant and change no result.

## 3. Per-variant availability

| Chassis | MW5 folder | ✅ available | ⚠️ chassis only | ❌ absent |
|---|---|---|---|---|
| **ARC** | Archer | ARC-2K, ARC-2W | | |
| **AS7** | Atlas | AS7-D, AS7-RS | AS7-D-DC | |
| **ASN** | Assassin | ASN-101, ASN-21 | | |
| **AWS** | Awesome | AWS-8Q, AWS-8R, AWS-8T, AWS-8V | | |
| **BJ** | Blackjack | BJ-1, BJ-1DB, BJ-1DC, BJ-1X | | |
| **BL** | Blackknight | BL-7-KNT, BL-7-KNT-L | | |
| **BLR** | Battlemaster | BLR-1D, BLR-1G, BLR-1S | BLR-1G-DC | |
| **BMB** | — | | | BMB-10D |
| **BNC** | Banshee | BNC-3E, BNC-3M, BNC-3S | BNC-3MC, BNC-3Q | |
| **CDA** | Cicada | CDA-2A, CDA-2B, CDA-3C | | |
| **CGR** | Charger | CGR-1A1, CGR-1A5 | CGR-1A9, CGR-1L, CGR-SB | |
| **CHP** | Champion | CHP-2N | | |
| **CLNT** | — | | | CLNT-1-2R, CLNT-2-3T, CLNT-2-4T |
| **CN9** | Centurion | CN9-A, CN9-AH, CN9-AL | | |
| **COM** | Commando | COM-1D, COM-2D, COM-3A | | |
| **CP** | Cyclops | CP-10-Q, CP-10-Z | | |
| **CPLT** | Catapult | CPLT-A1, CPLT-C1, CPLT-C4, CPLT-K2 | | |
| **CRB** | Crab | CRB-20 | | |
| **CRD** | Crusader | CRD-3D, CRD-3K, CRD-3L, CRD-3R | | |
| **CRK** | — | | | CRK-5003-0 |
| **CTF** | Cataphract | CTF-1X, CTF-2X | | |
| **DRG** | Dragon | DRG-1C, DRG-1N | | |
| **DV** | Dervish | DV-6M | | |
| **ENF** | Enforcer | ENF-4R | | |
| **FLC** | — | | | FLC-4N |
| **FLE** | Flea | FLE-15 | FLE-4 | |
| **FLS** | — | | | FLS-7K |
| **FS9** | Firestarter | FS9-A, FS9-H, FS9-K, FS9-M | | |
| **GHR** | Grasshopper | GHR-5H | | |
| **GLT** | — | | | GLT-4L, GLT-4P |
| **GRF** | Griffin | GRF-1E, GRF-1N, GRF-1S | | |
| **HBK** | Hunchback | HBK-4G, HBK-4H, HBK-4J, HBK-4P, HBK-4SP | HBK-4N | |
| **HER** | — | | | HER-1A, HER-1B |
| **HGN** | Highlander | HGN-733, HGN-733C, HGN-733P | | |
| **HNT** | — | | | HNT-151, HNT-152 |
| **HSR** | — | | | HSR-300-D, HSR-350-D |
| **JM6** | Jagermech | JM6-A, JM6-S | | |
| **JR7** | Jenner | JR7-D, JR7-F | JR7-A | |
| **JVN** | Javelin | JVN-10F, JVN-10N | | |
| **KTO** | Kintaro | KTO-18 | | |
| **LCT** | Locust | LCT-1E, LCT-1M, LCT-1S, LCT-1V, LCT-3V | | |
| **LGB** | Longbow | LGB-0W | | |
| **LNC25** | — | | | LNC25-02 |
| **MAD** | Marauder | MAD-3D, MAD-3L, MAD-3M, MAD-3R | | |
| **MCY** | — | | | MCY-98 |
| **MON** | — | | | MON-67, MON-68 |
| **ON1** | Orion | ON1-K, ON1-V, ON1-VA | ON1-V-DC | |
| **OSR** | — | | | OSR-2C, OSR-2L, OSR-2M, OSR-3C |
| **OTL** | — | | | OTL-4D, OTL-4F |
| **OTT** | — | | | OTT-7J |
| **PNT** | Panther | PNT-8Z, PNT-9R | | |
| **PXH** | Phoenixhawk | PXH-1, PXH-1K | PXH-1D | |
| **QKD** | Quickdraw | QKD-4G, QKD-4H, QKD-5A | | |
| **RFL** | Rifleman | RFL-3C, RFL-3N, RFL-4D | | |
| **RVN** | Raven | RVN-2X, RVN-4X | | |
| **SDR** | Spider | SDR-5D, SDR-5K, SDR-5V | | |
| **SHD** | Shadowhawk | SHD-2D, SHD-2H, SHD-2K | | |
| **STG** | — | | | STG-3G, STG-3R |
| **STK** | Stalker | STK-3F, STK-3H, STK-4N | STK-4P | |
| **STN** | — | | | STN-1S, STN-3K, STN-3KA, STN-3KB |
| **TBT** | Trebuchet | TBT-5J, TBT-5N, TBT-7K | TBT-5S | |
| **TDR** | Thunderbolt | TDR-5S, TDR-5SE | | |
| **THE** | — | | | THE-F, THE-S, THE-T |
| **THG** | — | | | THG-10E |
| **UM** | Urbanmech | UM-R60, UM-R60L | | |
| **VL** | Vulcan | VL-2T, VL-5T | | |
| **VLK** | — | | | VLK-QA, VLK-QF |
| **VND** | Vindicator | VND-1AA, VND-1R, VND-1SIC, VND-1X | | |
| **VTR** | Victor | VTR-9A1, VTR-9B, VTR-9S | VTR-9A | |
| **WHM** | Warhammer | WHM-6D, WHM-6K, WHM-6L, WHM-6R | | |
| **WLF** | Wolfhound | WLF-1, WLF-1A, WLF-1B | | |
| **WSP** | — | | | WSP-1A, WSP-1D, WSP-1K, WSP-1L, WSP-1W |
| **WTH** | — | | | WTH-0, WTH-1, WTH-1S |
| **WVE** | — | | | WVE-6N |
| **WVR** | Wolverine | WVR-6K, WVR-6M, WVR-6R | | |
| **ZEU** | Zeus | ZEU-6S, ZEU-6T | | |

## 4. Gaps

### 4.1 Twenty-three chassis have no MW5 art (47 variants)

All are pre-3050 Succession-Wars designs MW5 Mercenaries never shipped: Bombardier (BMB-10D), Clint (CLNT-1-2R, -2-3T, -2-4T), Crockett (CRK-5003-0), Falcon (FLC-4N), Flashman (FLS-7K), Gladiator (GLT-4L, -4P), Hermes II (HER-1A, -1B), Hornet (HNT-151, -152), Hussar (HSR-300-D, -350-D), Lancelot (LNC25-02), Mercury (MCY-98), Mongoose (MON-67, -68), Ostscout (OSR-2C, -2L, -2M, -3C), Ostsol (OTL-4D, -4F), Ostroc (OTT-7J), Sentinel (STN-1S, -3K, -3KA, -3KB), Stinger (STG-3G, -3R), Thorn (THE-F, -S, -T), Thug (THG-10E), Valkyrie (VLK-QA, -QF), Wasp (WSP-1A, -1D, -1K, -1L, -1W), Whitworth (WTH-0, -1, -1S), Wyvern (WVE-6N).

These need a different source (commissioned/CC models, traced vector art) — MW5 cannot close this gap. Note that the pipeline's licensing story already assumes a mixed source set ([../architecture/asset-pipeline.md](../architecture/asset-pipeline.md) §8).

### 4.2 Fifteen variants are chassis-only

AS7-D-DC, BLR-1G-DC, ON1-V-DC (all dual-cockpit), BNC-3MC, BNC-3Q, CGR-1A9, CGR-1L, CGR-SB, FLE-4, HBK-4N, JR7-A, PXH-1D, STK-4P, TBT-5S, VTR-9A.

The body and all clips exist; only the loadout data asset is missing. For top-down sprites the difference is mostly weapon silhouette, so these are cheap: either borrow the closest sibling loadout and record it in `assembly.json`, or hand-author the weapon list. They should not block a batch run.

### 4.3 The reverse gap is larger than the forward one

The 53 matched chassis carry **430 MW5 variant assets**, so ~293 variants are art-ready in MW5 but absent from `data/units/mechs` — e.g. Atlas AS7-K/AS7-KR/AS7-P, Battlemaster BLR-1M/BLR-3M, Marauder MAD-2R/MAD-5D, Warhammer WHM-6RB, Locust LCT-3S, Hunchback HBK-4HP. Any future unit-list expansion into those chassis gets its art for free; no new extraction work is required.

## 5. Method and caveats

- **Source of truth** is `artifacts/mw5/discovery-report.json` (gitignored, machine-local): 104 chassis, 686 variant assets, generated by `MW5Extractor discovery-report` from `MW5Mercs-WinGDK.pak` (99 GiB, base + DLC, UE 4.26) on 2026-10-27.
- **No `chassis-map.json` exists in the repo yet.** The architecture places it in the packaging step ([../architecture/asset-pipeline.md](../architecture/asset-pipeline.md) §3, `MW5Extractor/README.md` "Notes and limits"), so the MW5-folder → chassis-code mapping in §3 and the appendix was derived by variant-id matching. It is unambiguous for all 53 matched chassis (every match is an exact variant id) but it should be committed as that file before any batch render, and it is the natural output of the planned `makamek-pipeline catalog` verb.
- **Id matching only.** A variant present in MW5 under a different id than ours (e.g. Atlas II's `AS7-D-H` vs our `AS7-*`) would be missed; none were found in the 199-unit set.
- **Availability ≠ permission.** MW5 renders are derivative works and stay in gitignored `artifacts/`; see [mw5-model-extraction.md](mw5-model-extraction.md) §5. This document maps *what can be rendered locally*, not what can be shipped.
- Regenerate with the two artifacts left in `artifacts/mw5/`: `variant-coverage.csv` (199 rows, machine-readable) and `coverage-table.md` (the §3 table).

## 6. Appendix — MW5 folder → chassis code (53 matched)

- Archer → `ARC` · Assassin → `ASN` · Atlas → `AS7` · Awesome → `AWS`
- Banshee → `BNC` · Battlemaster → `BLR` · Blackjack → `BJ` · Blackknight → `BL`
- Cataphract → `CTF` · Catapult → `CPLT` · Centurion → `CN9` · Champion → `CHP`
- Charger → `CGR` · Cicada → `CDA` · Commando → `COM` · Crab → `CRB`
- Crusader → `CRD` · Cyclops → `CP` · Dervish → `DV` · Dragon → `DRG`
- Enforcer → `ENF` · Firestarter → `FS9` · Flea → `FLE` · Grasshopper → `GHR`
- Griffin → `GRF` · Highlander → `HGN` · Hunchback → `HBK` · Jagermech → `JM6`
- Javelin → `JVN` · Jenner → `JR7` · Kintaro → `KTO` · Locust → `LCT`
- Longbow → `LGB` · Marauder → `MAD` · Orion → `ON1` · Panther → `PNT`
- Phoenixhawk → `PXH` · Quickdraw → `QKD` · Raven → `RVN` · Rifleman → `RFL`
- Shadowhawk → `SHD` · Spider → `SDR` · Stalker → `STK` · Thunderbolt → `TDR`
- Trebuchet → `TBT` · Urbanmech → `UM` · Victor → `VTR` · Vindicator → `VND`
- Vulcan → `VL` · Warhammer → `WHM` · Wolfhound → `WLF` · Wolverine → `WVR`
- Zeus → `ZEU`

Names diverge only in spelling (Blackknight, Jagermech, Shadowhawk, Phoenixhawk) or code family (Cyclops → `CP`, Urbanmech → `UM`, Vulcan → `VL`, Longbow → `LGB`). Clan chassis (`Objects/Mechs/CLANS/<Chassis>`) and the modular Atlas II have no counterpart in `data/units/mechs` today.
