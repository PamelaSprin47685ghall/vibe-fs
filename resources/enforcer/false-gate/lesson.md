# false-gate — Lesson

## Opinion

### Definition

A gate is false when “green” is compatible with the exact defect the gate claims to forbid.

The usual failure is not a bad assertion in isolation. It is a broken implication:

> gate is green ⇒ guarded property holds

If the left side can be true while the right side is false, the gate is theater. A ceremonial gate is worse than no gate because it converts absence of evidence into institutional confidence.

### Governing Principle

A guard earns trust only by demonstrating that a reachable violation can force it red.

This sounds obvious, yet entire CI systems routinely violate it: a glob matches nothing; a grep scans yesterday's directory; a command exits 0 after printing an error; a wrapper drops exit status; a test asserts that setup succeeded rather than that behavior was correct; a baseline silently absorbs the new violation; a “check” prints warnings while the pipeline badges it as enforcement.

The pathology is contemporary because automation makes these failures look authoritative. A green badge has visual weight even when the machinery underneath checked nothing.

### Trigger When

Trigger when any ordinary path lets a known in-scope violation survive while the standard gate remains green, including:

- subject discovery can return zero relevant files and that is treated as success;
- the detector reads a stale, wrong, generated, or incomplete surface;
- failure output is produced but exit status remains successful;
- the CI wrapper swallows or overwrites the detector's non-zero status;
- the assertion is tautological or proves only that the test harness ran;
- exclusions/baselines make newly introduced violations invisible without an explicit reviewable admission;
- a check is described operationally as a gate even though it is only advisory.

### Do Not Trigger When

- The check is explicitly advisory and nobody presents green as proof of compliance.
- A known-bad fixture demonstrably turns the exact production entry point red, and the changed scope is still subject to that path.
- The gate can fail for the claimed property but its tests are too weak to prove enough behavior; that is usually `coverage-theater`.
- The detector correctly fails and a caller later ignores that failure; `tool-error-ignored` is the sharper diagnosis.

### Distinguish From

`coverage-theater` has a live measuring instrument pointed at an impoverished proposition. `false-gate` has no dependable implication from green to the advertised property at all.

`missing-architecture-gate` means no guard exists. Here the danger is subtler: a guard exists, so people stop looking.

Tie-break with one question: can you place a known violation inside the advertised scope and still get green through the normal entry point? If yes, this rule owns the wound.

### Decision Procedure

Plant the smallest representative violation. Do not call the detector through a private helper; use the same entry point humans and CI trust.

Then verify the whole chain:

1. discovery finds the subject;
2. detection recognizes the violation;
3. the detector returns failure;
4. wrappers preserve failure;
5. the pipeline is red.

Any broken link makes the gate false for that property.

### Examples

- positive: `npm run check` scans `src/**/*.ts`, but the project moved to `packages/*/src`; zero files match and CI stays green.
- positive: a script prints `FAIL: forbidden import` but exits 0 because the failure count is never returned.
- positive: a baseline file is regenerated on every run, so new debt is automatically grandfathered.
- near-miss: an advisory complexity report always exits 0 and is consistently described as advisory.
- counterexample: a committed bad fixture is enabled in a self-test and the same CI command deterministically turns red.

## Advice

### What To Do Now

Make the gate prove that it can reject the thing it claims to forbid.

Create a minimal known-bad fixture inside the advertised scope and run the normal entry point. Keep repairing discovery, matching, assertions, exit propagation, and CI wiring until that fixture makes the gate red.

Then remove or isolate the fixture without removing the self-test that proves the guard still has teeth.

### Why This Matters

Teams do not merely consume gate output; they **delegate vigilance** to gates. Once a check is called “required,” humans reasonably stop re-proving its property on every change.

That delegation is only safe if green means something.

A false gate is therefore not a weak test. It is a broken organizational contract. It says “you may trust this property” while the machinery is incapable of earning that trust. The prettier the CI badge and the more universal the script name, the more dangerous the lie.

### Repair Strategy

Trace the implication from property to pipeline, end to end:

- **subject discovery:** prove the intended files/cases are actually enumerated;
- **detector:** prove the known violation is recognized;
- **failure semantics:** prove recognition produces non-zero / rejected state;
- **wrapper:** prove shell/npm/task runners preserve that failure;
- **pipeline:** prove CI marks the job red;
- **scope drift:** prove future directory or extension changes cannot silently reduce the set to zero without a test noticing.

Fail closed only where empty scope is itself evidence of misconfiguration. Do not blindly make every empty set an error; some checks legitimately have no subjects. The property decides.

### Decision Branches

- **Zero relevant subjects but subjects should exist:** repair discovery and add a sentinel assertion proving the expected scope is non-empty.
- **Violation is discovered but not classified:** repair the detector or rule boundary.
- **Detector fails locally but CI is green:** repair exit/status propagation; do not touch detection logic.
- **Baseline/exceptions swallow the new violation:** make admission explicit and reviewable, or make the check advisory. Never auto-grandfather debt while calling the result enforcement.
- **The check is intentionally advisory:** rename/document it so nobody can mistake green for a compliance guarantee.

### Common Wrong Fixes

- Add more logging. Better narration does not create enforcement.
- Widen a glob until today’s files happen to appear, without a failing fixture that prevents tomorrow’s drift.
- Add `|| true`, `continue-on-error`, soft error conversion, or a wrapper that always exits 0 “temporarily.” Temporary false gates become permanent very quickly.
- Test the detector through an internal helper while production uses a different wrapper path.
- Assert only that “some files were scanned.” A scanner can inspect the right files and still be unable to recognize the forbidden state.
- Regenerate a baseline automatically and call the disappearing delta “ratcheting.” That is debt laundering.

### Verification

Through the exact standard entry point, prove both directions with representative fixtures:

- known bad → red;
- known valid → green.

Then break each critical link deliberately where practical: wrong path, detector violation, non-zero child status. The pipeline must not silently convert those failures into green.

The invariant is simple:

> Green is meaningful because a relevant red state has been demonstrated through the same path.

### Done When

A reasonable maintainer can answer “what concrete defect makes this gate red?” with a committed example, and the normal CI/local command proves the answer.

Until then, remove the word “gate.”

### Nudge

Do not ask whether the gate ran.

Ask whether the defect can make it fail.

A green light that has never demonstrated red is just decoration with permissions.
