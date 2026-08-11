# misleading-name — Main

## What To Do Now
Rename the identifier to match the guarantee and ownership the implementation actually provides, or strengthen the implementation if the stronger name reflects the intended contract.

## Why This Matters
Readers treat names as cached understanding. A misleading name poisons that cache: every call site starts reasoning from a false premise and only discovers the mismatch when inspecting implementation or debugging a failure.

## Repair Strategy
Name the actual domain fact, owner, and guarantee in plain terms. Update public types, tests, docs, and call sites together so the vocabulary converges rather than preserving misleading aliases.

## Wrong Fixes
Do not add a comment saying “despite the name…” or keep the old name for compatibility without a real external obligation. Explanations do not cancel the false premise the identifier keeps broadcasting.

## Verification
A new reader should be able to predict the implementation’s meaningful guarantees from the name and type without learning a caveat.

## Done When
Names function as reliable compressed contracts rather than historical labels that must be mentally corrected at every use.
