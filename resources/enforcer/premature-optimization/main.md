# premature-optimization — Main

## What To Do Now
Return to the simplest correct design unless a measured bottleneck or explicit resource budget justifies the added complexity. The measured constraint (SLO, profile, hard budget) is who owns the performance invariant that every nontrivial optimization points to the scarcity it satisfies.

## Why This Matters
Speculative optimization hardens assumptions about where cost lives. Those assumptions often outlive the workload that inspired them, while the complexity they introduced remains real in every test, refactor, and failure path.

## Repair Strategy
Define the performance objective, benchmark/profile the simple path, and optimize the dominant measured cost only. Keep the optimization isolated enough that its proof and rollback remain local.

## Decision Branches
- If there is no measured bottleneck or stated budget, remove the speculative machinery and restore the simple design.
- If a profiled cost or hard SLO is already binding, optimize that cost only and keep the proof local.

## Common Wrong Fixes
- Defend complexity with "this might scale better" instead of a measurement.
- Ignore known asymptotic limits when the workload already makes them binding.
- Optimize a cold path because it looks expensive in the source, not in the profile.

## Verification
Measure before and after under representative load and confirm the improvement is material to the stated budget. The invariant is that every nontrivial performance complexity points to a constraint it satisfies.

## Done When
Every nontrivial performance complexity can point to the constraint it satisfies and the measurement that proves it earns its maintenance cost.
