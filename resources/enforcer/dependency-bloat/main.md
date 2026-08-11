# dependency-bloat — Main

## What To Do Now
Remove or avoid the dependency unless it eliminates complexity the project would otherwise have to own materially and permanently.

## Why This Matters
A package can delete twenty local lines while adding thousands of indirect assumptions. The source diff hides that asymmetry. Every dependency expands the set of external decisions that can force your project to change, so adoption should purchase real leverage, not merely syntactic convenience.

## Repair Strategy
State the capability needed, inspect what the existing platform already provides, and estimate the smallest direct implementation. Keep the dependency only when it clearly owns complexity better than the repository can.

## Decision Branches
- If the platform or a few local lines already solve the need, remove the new dependency.
- If the library owns hard, security-sensitive, or standards-heavy work, keep it and do not reimplement.
- If ceremony after adoption is the remaining pain, that is `framework-tax`, not this acquisition decision.

## Common Wrong Fixes
- Do not justify adoption by popularity, download count, or initial brevity alone.
- Do not reimplement genuinely hard cryptography, protocols, or standards merely to avoid a dependency.
- Do not vendor an entire unused framework “for later” while using one helper.
- Do not replace one bloated package with another of equal transitive weight.

## Verification
After the choice, the implementation should expose the domain operation with minimal lifecycle/configuration surface and no unnecessary transitive machinery. The invariant is that the project pays an external dependency tax only where that purchase removes more durable complexity than it introduces.

## Done When
The project pays an external dependency tax only where that purchase removes more durable complexity than it introduces.
