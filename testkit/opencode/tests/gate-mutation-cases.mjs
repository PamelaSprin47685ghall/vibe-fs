/**
 * gate-mutation-cases.mjs — registration placeholder, filled by task P3-2 / K11.
 *
 * VERIFY-003 森林变异自检：四类历史假绿各须有一条红过的测试
 *
 * ── why this file exists empty ──────────────────────────────────────────────
 *
 * `gate-testkit.mjs` is the single registration point for every gate case, so four
 * concurrent tasks each adding an import and a spread would collide in one file. The
 * registrations are therefore made up front and the case arrays filled independently.
 *
 * An empty array is a real risk and it is why this header says so: a registered file
 * that contributes nothing looks identical, in the gate's output, to one whose cases all
 * pass. The completeness gate in W7 is what closes that hole — it asserts every item on
 * VERIFY-004's forbidden-degradation list has a named case, so a placeholder left empty
 * fails there rather than passing silently here.
 */

export const mutationCases = [];
