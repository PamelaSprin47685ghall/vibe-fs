# dependency-bloat — Main

## What To Do Now
Remove or avoid the dependency unless it eliminates complexity the project would otherwise have to own materially and permanently.

## Why This Matters
A package can delete twenty local lines while adding thousands of indirect assumptions. The source diff hides that asymmetry. Every dependency expands the set of external decisions that can force your project to change, so adoption should purchase real leverage, not merely syntactic convenience.

## Repair Strategy
State the capability needed, inspect what the existing platform already provides, and estimate the smallest direct implementation. Keep the dependency only when it clearly owns complexity better than the repository can.

## Wrong Fixes
Do not justify adoption by popularity, download count, or initial brevity alone. Do not build a local reimplementation of genuinely hard cryptography, protocols, or standards merely to avoid a dependency either.

## Verification
After the choice, the implementation should expose the domain operation with minimal lifecycle/configuration surface and no unnecessary transitive machinery.

## Done When
The project pays an external dependency tax only where that purchase removes more durable complexity than it introduces.
