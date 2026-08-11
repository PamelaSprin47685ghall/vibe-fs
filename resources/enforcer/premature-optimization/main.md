# premature-optimization — Main

## What To Do Now
Return to the simplest correct design unless a measured bottleneck or explicit resource budget justifies the added complexity.

## Why This Matters
Speculative optimization hardens assumptions about where cost lives. Those assumptions often outlive the workload that inspired them, while the complexity they introduced remains real in every test, refactor, and failure path.

## Repair Strategy
Define the performance objective, benchmark/profile the simple path, and optimize the dominant measured cost only. Keep the optimization isolated enough that its proof and rollback remain local.

## Wrong Fixes
Do not defend complexity with “this might scale better.” Future scale is not measurable evidence. Equally, do not ignore known asymptotic limits when the workload already makes them binding.

## Verification
Measure before and after under representative load and confirm the improvement is material to the stated budget.

## Done When
Every nontrivial performance complexity can point to the constraint it satisfies and the measurement that proves it earns its maintenance cost.
