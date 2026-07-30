/**
 * gate-unit-runner-cases.mjs — registration placeholder, filled by task B2 / W4.
 *
 * VERIFY-004 单测运行器：判决投喂的静默看门狗，超时即遗忘，挂死不停摆
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

export const unitRunnerCases = [];
