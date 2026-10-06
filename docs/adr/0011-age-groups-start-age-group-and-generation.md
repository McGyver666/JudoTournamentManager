# 11. Age groups, start age group, and category generation

Status: Accepted

## Context

Category generation previously combined multiple age groups in one run and selected an athlete's
age group by preset order. Overlapping ranges could therefore produce inconsistent natural groups,
and registrations could not explicitly start in a higher age group. Adult presets with open birth-year
bounds were also excluded by bounded year filters.

## Decision

- Tournament category presets are the source of truth for age groups. A preset may have no standard
  weight limits; this means categories are grouped from registered athlete weights.
- The natural age group is the matching gender preset with the smallest minimum age, then the
  smallest maximum age. A missing minimum age counts as the lowest, a missing maximum age as the
  highest. A registration may optionally select a compatible preset age group as its
  start age group. Its effective age group is the selected group, otherwise the natural group.
- Each generation request selects exactly one age group and a gender scope. Athlete grouping uses
  one target size and one maximum weight gap for the run; generated categories inherit the preset's
  birth-year range.
- Applying generation replaces only categories for that age group and gender, in one transaction,
  and is audited as `CategoriesGenerated`. The operation is rejected before deletion if any affected
  category is locked, has a draw, or has fights.
- Changing a registration's start age group validates the selected preset, clears a now-incompatible
  unlocked category assignment, and records the previous and next value with the operator in audit.
- Category assignment checks effective age group, gender and the upper weight limit. Category
  birth-year bounds are only a plausibility hint: auto-assignment ignores them and manual
  assignment asks the operator for confirmation in the UI. Placing a lighter athlete in a heavier
  class and assigning before weigh-in stay possible.
- The default preset set includes weightless U9 (ages 6-8, 120-second matches) for both genders,
  and the adult presets `Männer`/`Frauen` start at age 17 without an upper limit. Defaults are
  seeded for new tournaments and on reset; existing tournaments do not get U9. A one-time migration
  corrects only adult presets that still carry the earlier faulty seed ("at most 17 years").
- Age-group rules are evaluated only in the backend. Preset warnings come from
  `GET category-presets/warnings`; API errors of this feature carry a `messageKey` so the frontend
  can localize them.

## Consequences

- Registrations persist the optional start age group in a nullable column, so older database backups
  restore with the natural age group. Backups include the category presets; restoring a backup
  without presets seeds the defaults for the tournament year, because presets are the only source
  of age groups.
- Auto and manual category assignment require the effective age group before checking gender and
  weight. Category year bounds remain a plausibility check.
- The preset editor reports registrations without a matching age group, presets that can never be
  natural, and categories whose age group has no matching preset.
- The generator preview lists affected registrations, proposed categories, and categories to replace.