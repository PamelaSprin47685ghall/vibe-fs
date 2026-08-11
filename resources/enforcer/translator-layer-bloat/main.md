# translator-layer-bloat — Main

## What To Do Now
Delete forwarding-only layers and connect callers to the real owning contract. Retain an intermediate layer only where it enforces a distinct invariant or translation.

## Why This Matters
Every layer creates a cognitive toll: another file, name, stack frame, test double, and place where ownership might live. That toll is repaid only when crossing the layer changes what callers are allowed to know or assume.

## Repair Strategy
Classify each method as transformation, policy, lifecycle ownership, or pass-through. Move real behavior to the boundary that owns it and collapse pure relay methods. Rename surviving layers after the invariant they protect rather than generic orchestration nouns.

## Wrong Fixes
Do not generate forwarding boilerplate or hide it behind interfaces. Automation can make empty indirection cheaper to write without making it cheaper to understand.

## Verification
Removing any surviving layer should demonstrably destroy a contract or invariant; otherwise the layer still has no semantic reason to exist.

## Done When
Every architectural hop changes knowledge, representation, authority, or lifecycle in a way that justifies the distance it adds.
