# misleading-name — Main

## What To Do Now
Align the name at the root-cause owner: who owns the identifier must make its implied contract equal the implementation—rename to the real guarantee, or strengthen the implementation if the stronger name is the intended contract.

## Why This Matters
Readers treat names as cached understanding. A misleading name poisons that cache: every call site starts reasoning from a false premise and only discovers the mismatch when inspecting implementation or debugging a failure.

## Repair Strategy
Name the actual domain fact, owner, and guarantee in plain terms. Update public types, tests, docs, and call sites together so the vocabulary converges rather than preserving misleading aliases.

## Decision Branches
- If the stronger name is the intended contract, strengthen the implementation until the claim is true.
- If the implementation is the intended contract, rename so the name matches that weaker guarantee.

## Common Wrong Fixes
- Do not add a comment saying “despite the name…” or keep the old name for compatibility without a real external obligation. Explanations do not cancel the false premise the identifier keeps broadcasting.
- Do not prefix `Real` or `Actual` onto the same lie (`RealDurableStore` that is still in-memory).
- Do not rename only in one layer while public APIs keep broadcasting the false claim.

## Verification
A new reader should be able to predict the implementation’s meaningful guarantees from the name and type without learning a caveat. The invariant: the name’s implied contract equals the implementation’s real contract.

## Done When
Names function as reliable compressed contracts rather than historical labels that must be mentally corrected at every use.
