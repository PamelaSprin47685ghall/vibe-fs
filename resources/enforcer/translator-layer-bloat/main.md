# translator-layer-bloat — Main

## What To Do Now
Delete forwarding-only layers and connect callers to the real owning contract. Retain an intermediate layer only where it enforces a distinct invariant or translation.

## Why This Matters
Every layer creates a cognitive toll: another file, name, stack frame, test double, and place where ownership might live. That toll is repaid only when crossing the layer changes what callers are allowed to know or assume.

## Repair Strategy
Classify each method as transformation, policy, lifecycle ownership, or pass-through. Move real behavior to the boundary that owns it and collapse pure relay methods. Rename surviving layers after the invariant they protect rather than generic orchestration nouns.

## Decision Branches
If removing the layer loses no invariant, information boundary, or failure model, delete it.
If the layer owns a real translation or policy, name that invariant and make it the layer’s visible contract.

## Common Wrong Fixes
- Generate forwarding boilerplate or hide empty hops behind interfaces.
- Rename `Manager` to `Facade` without adding a semantic job.
- Push the same pass-through into a new “orchestrator” one package over.

## Verification
Invariant: every surviving hop must change knowledge, representation, authority, or lifecycle. Removing any remaining layer should destroy a contract or invariant; otherwise the layer still has no semantic reason to exist.

## Done When
Every architectural hop changes knowledge, representation, authority, or lifecycle in a way that justifies the distance it adds.
