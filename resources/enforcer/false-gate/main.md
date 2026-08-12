# false-gate — Main

## What To Do Now
Make the gate prove that it can reject the thing it claims to forbid.

Create a minimal known-bad fixture inside the advertised scope and run the normal entry point. Keep repairing discovery, matching, assertions, exit propagation, and CI wiring until that fixture makes the gate red.

Then remove or isolate the fixture without removing the self-test that proves the guard still has teeth.

## Why This Matters
Teams do not merely consume gate output; they **delegate vigilance** to gates. Once a check is called “required,” humans reasonably stop re-proving its property on every change.

That delegation is only safe if green means something.

A false gate is therefore not a weak test. It is a broken organizational contract. It says “you may trust this property” while the machinery is incapable of earning that trust. The prettier the CI badge and the more universal the script name, the more dangerous the lie.

## Repair Strategy
Trace the implication from property to pipeline, end to end:

- **subject discovery:** prove the intended files/cases are actually enumerated;
- **detector:** prove the known violation is recognized;
- **failure semantics:** prove recognition produces non-zero / rejected state;
- **wrapper:** prove shell/npm/task runners preserve that failure;
- **pipeline:** prove CI marks the job red;
- **scope drift:** prove future directory or extension changes cannot silently reduce the set to zero without a test noticing.

Fail closed only where empty scope is itself evidence of misconfiguration. Do not blindly make every empty set an error; some checks legitimately have no subjects. The property decides.

## Decision Branches
- **Zero relevant subjects but subjects should exist:** repair discovery and add a sentinel assertion proving the expected scope is non-empty.
- **Violation is discovered but not classified:** repair the detector or rule boundary.
- **Detector fails locally but CI is green:** repair exit/status propagation; do not touch detection logic.
- **Baseline/exceptions swallow the new violation:** make admission explicit and reviewable, or make the check advisory. Never auto-grandfather debt while calling the result enforcement.
- **The check is intentionally advisory:** rename/document it so nobody can mistake green for a compliance guarantee.

## Common Wrong Fixes
- Add more logging. Better narration does not create enforcement.
- Widen a glob until today’s files happen to appear, without a failing fixture that prevents tomorrow’s drift.
- Add `|| true`, `continue-on-error`, soft error conversion, or a wrapper that always exits 0 “temporarily.” Temporary false gates become permanent very quickly.
- Test the detector through an internal helper while production uses a different wrapper path.
- Assert only that “some files were scanned.” A scanner can inspect the right files and still be unable to recognize the forbidden state.
- Regenerate a baseline automatically and call the disappearing delta “ratcheting.” That is debt laundering.

## Verification
Through the exact standard entry point, prove both directions with representative fixtures:

- known bad → red;
- known valid → green.

Then break each critical link deliberately where practical: wrong path, detector violation, non-zero child status. The pipeline must not silently convert those failures into green.

The invariant is simple:

> Green is meaningful because a relevant red state has been demonstrated through the same path.

## Done When
A reasonable maintainer can answer “what concrete defect makes this gate red?” with a committed example, and the normal CI/local command proves the answer.

Until then, remove the word “gate.”
