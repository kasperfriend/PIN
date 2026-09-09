# Player Stats & Health — what the database defines, and what it does not

> Audit of the player side of the "everything has correct stats from the DB" work,
> against the `clientdb.sd2` of Firefall build **prod-1962** (the same file the
> server loads). Everything in this document was verified by decoding the real
> rows, not by reading this codebase's comments.

---

## 1. What the server already computes from the database (verified)

| Player stat | Source in the DB | How the server uses it |
|-------------|------------------|------------------------|
| Stat sheet (`CharacterStats.ItemAttributes`) | Sum of `dbitems::AttributeRange.base` over the chassis item, the gear (torso/head/arms/legs/reactor/OS/medical/aux) and the slotted abilities; the weapon *items* are summed separately into `WeaponA`/`WeaponB` (weapon item + its weapon-slot default-ability module) | `CharacterLoadout.CalculateItemAttributes` builds the aggregate; `ApplyLoadout` replicates it as `CharacterStats`. `dbitems::CharCreateLoadoutSlots` provides the default PvE loadout per battleframe (verified: Accord Assault = primary 86742, secondary 87741, HKM 88491, backpack 75877, …) |
| Player max-health pool | Loadout Health sum (attribute 6, same rows as the stat sheet) + `dbitems::LevelItemAttributes` attribute 6 at the frame's progression level | `RefreshMaxHealthFromLoadout` in `ApplyLoadout`: `MaxHealth = item Health sum + LevelCurve(frame level) × 3` (see §4). NPCs never go through this — their pool comes from `dbcharacter::MonsterScaling` (§5) |
| Weapon per-round damage | `dbitems::AttributeRange` attribute **954** of the weapon *item* — its `dbitems::AttributeDefinition` display name is literally "Damage Per Round" | `WeaponSim.OnFireWeaponProjectile`; fallback = the resolved `WeaponTemplates.damage_per_round` (template + `WeaponTemplateModifiers` + weapon-slot module deltas, the value NPC/turret weapons fight at); `ProjectileSim.LegacyPlaceholderDamage` (1337) only for rows with neither |
| Weapon distance falloff | The fired `dbitems::Ammo` row: `damage_decay`, `damage_decay_rangefrac`, `min_damage_frac` | `ProjectileSim` tracks metres travelled and applies `WeaponDamageMath.ApplyDamageFalloff` at impact (decay 0 = flat; otherwise full damage until `damage_decay_rangefrac` of the weapon `range`, linear taper to `min_damage_frac` × per-round at max range) |
| Jetpack energy | `dbitems::Battleframe.base_energy` / `energy_recharge_delay_ms` / `energy_recharge_per_sec`; attribute 35 (Jet Energy) is replicated through the stat sheet for the client's own model | `UpdateEnergyParamsFromBattleframe` on every loadout apply, falling back to the construction defaults when the frame row is 0 (in build prod-1962 every playable frame row has `base_energy`/`energy_recharge_per_sec` = 0 and `energy_recharge_delay_ms` = 250, so the served params stay Max 1000 / Delay 250 / Recharge 156) |
| Player level | — | `CharacterEntity.FrameProgressionLevel`, starts at 1 (battleframe progression level; no XP economy yet, see §3) |

### Example weapon damage values (build prod-1962, all from real rows)

| Weapon item | Frame | Attribute 954 | Ammo | Falloff |
|---|---|---|---|---|
| 86742 (Plasma Cannon) | Accord Assault primary | **100** | 983 | `damage_decay = 0` → flat over the whole range (100 m) |
| 87741 (Assault Rifle) | Accord Assault secondary | **11** | 58 | decay from 70% of 150 m, down to 33% (≈4) at max range |
| 86969 (R36) | Accord Recon primary | **39** | 951 | decay from 70% of 180 m, down to 33% (≈13) |
| 86851 (HMG) | Accord Dreadnaught primary | **16** | 949 | decay from 10% of 80 m, down to 30% (≈5) |
| 124651 | PvP Assault AR | **1040** | — | — |

The template `damage_per_round` of the same weapons is *not* the player's damage:
it is the generic recipe row (e.g. template 12121 for the R36 carries a 1-damage
stub, while every real R36 item carries 954 = 39). NPCs have no item attribute
ranges, which is why their projectiles fight at the template value.

---

## 2. The stat sheet is the sum of the equipped items

Verified against the real rows: `CalculateItemAttributes` covers the frame
(chassis) item plus every slotted gear/ability item, each contributing its whole
`AttributeRange` row set (all attributes the row declares, `base` values), while
the two weapons feed `WeaponA`/`WeaponB`. In the replicated stat sheet frames
differ by energy (35), jet recharge (5), HP regen (7), jump height (37) and
sprint speed (1377) — e.g. Dreadnaught 75772 vs Assault 76164: 300 vs 500 jet
energy, 100 vs 75 jet recharge, 4.5 vs 3.75 HP regen — and weapon items differ
per instance (e.g. the Accord Assault secondary rifle 87741 carries Range 957 =
125 and Spread 958 = 5 on its own weapon-array). The replicated jet `EnergyParams`
themselves are served from the `Battleframe` row's fields with defaults when they
are 0 (see the table in §1), not from attribute 35.

Health (6) is 100 on **every** battleframe in this build — per-frame *health*
differences do not exist in the DB rows (see §4). NPCs do not go through
`CharacterStats` at all: their health comes from `dbcharacter::MonsterScaling`
(see `Docs/NPC_AI.md` §5).

---

## 3. Player level: battleframe progression

Characters replicate `Level`/`EffectiveLevel` and the frame-XP panel
(`ProgressionXPRefresh.CurrentLevel`) from `CharacterEntity.FrameProgressionLevel`,
which is the progression level of the battleframe being worn (`dbitems::FrameProgressionLevel`
is the per-level XP/perk/emissive table, 50 rows, levels 1–50). A freshly equipped frame
starts at level 1; PIN has no XP economy yet, so every frame sits at 1 and the
value is only consumed by level-gated aptitude chains (`RequireLevelCommand`,
`LoadRegisterFromLevelCommand`) and the client UI.

For reference: `dbitems::Battleframe.min_progression_level`/`max_progression_level`
are not a usable source for this — of the 1,676 rows in build prod-1962, 1,648
use `-1` (the "no progression gate" sentinel, the case for the playable frames:
Accord Assault 76164 = min `-1`, max 20), and the rows that do carry a number
are gated unlocks (min 20+) rather than a starting level. The authentic starting
level of a fresh, ungated frame is 1 — the first row of the `FrameProgressionLevel`
XP table and the floor of the level bands that feed NPC difficulty both agree.

NPC difficulty deliberately does **not** read `FrameProgressionLevel`: NPCs are
leveled by their zone's band (designed content) or by `SDBUtils.DefaultNpcLevel`,
which stays a separate constant equal to the default player level so mobs in
untuned zones (12 "Nothing", Crash Down 1003, the mission pockets the live game
tuned server-side) fight on a fresh player's terms — level 1, 100 HP / 50
damage per hit, with an authored `character_spawn.json` `level` available per
spawn (see `Docs/NPC_AI.md` §5).

---

## 4. Player health pool: the DB-driven rule

Since a battleframe is what a player wears, the pool is rebuilt from the worn
items plus the frame's progression level on every `ApplyLoadout`:

> **MaxHealth = (sum of Health attribute 6 over the loadout)
> + `dbitems::LevelItemAttributes`[attribute 6][frame level] × 3**

What each piece is in the DB, all verified against real rows:

- **Item Health sum** — the loadout aggregate of attribute 6 ("Health"). The
  chassis item carries 100 on every frame (so a bare frame is 100, not 0),
  gear items add theirs (e.g. 75 on a torso/legs piece of the starter set), the
  weapons contribute through their own weapon arrays. In build prod-1962 the
  default Accord Assault loadout sums to ≈ 361.
- **Level curve** — `dbitems::LevelItemAttributes` carries exactly one curve,
  attribute 6, levels 1–50 (level 1–3 = 0, then 20, 100, 280, 610, 1060, 1930,
  2710, 4120, 6360, 9780). The server loads it through `SDBInterface`; a level
  with no row contributes 0.
- **× 3** — the only non-row constant in the rule. Nothing in the database says
  how many pool points a curve unit is worth; 3.0 is the single documented
  approximation, anchored on the one capture this codebase has: the old flat
  pool of 19,192 belonged to a level-45 character whose item Health summed to
  ≈ 1,038 and whose curve value is 6,360 — i.e. ≈ 3 × the curve. The rule
  reproduces that capture within ~5% (20,117 vs 19,192).

Consequences of the rule, as chosen:

- A fresh frame at progression level 1 has **no curve** (it is 0 below level 4),
  so its pool is exactly its item sum (≈ 360 for the default loadout). That is
  the authentic level-1 state, and NPCs in zones without a level band now fight
  on those same terms (level 1, 100 HP / 50 damage per hit). Banded zones above
  level ~12 are still meant for geared players — in the real game you reached
  them after the leveling flow, which does not exist here yet.
- `HardcodedCharacterData.MaxHealth` (19192) is no longer the player pool: it
  survives only as the construction-time default before any loadout is applied
  (and as the pool NPCs keep when even `MonsterScaling` has no row for their
  level).
- The community-documented original formula (`((Base Health + Core Health) ×
  Frame Health Bonus) × Level Modifier`) cannot be reproduced from this build's
  rows alone: `Battleframe.base_health` is 0 for playable frames and no table
  defines the frame/level multipliers. The rule above is the closest
  row-grounded shape and is deliberately not extended with invented extra
  anchors.

---

## 5. Audit summary

| Question | Answer |
|---|---|
| Is the replicated player stat sheet DB-correct? | Yes — it is the sum of the equipped items' `AttributeRange` rows (default loadouts come from `CharCreateLoadoutSlots`); the stale captured constructor seed was removed |
| Is player max health DB-derived? | Yes — item Health sum + `LevelItemAttributes` curve at the frame's progression level, × the one documented pool-scale constant (§4); NPCs keep their `MonsterScaling` pool |
| Do player weapons deal DB damage? | Yes — item attribute 954 "Damage Per Round", with template fallback and ammo-defined distance falloff |
| Is player level DB-driven? | Partially — frame progression level (1 until XP exists) is now the replicated level and the health-curve input; NPC difficulty keeps its own anchor |
| What would still need a capture? | The ×3 pool-scale constant (§4) and the module-scalar replication (`ApplyLoadout` TODO) |
