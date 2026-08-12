# type-erosion-at-boundary — Main

## What To Do Now
Move parsing, validation, normalization, and unchecked operations to the adapter that owns the external protocol. Make that adapter return closed domain/application values whose construction proves the facts inward callers need.

The owner is the ingress boundary. Raw wire shape may belong there; it does not belong to every downstream policy function.

## Why This Matters
A cast does not remove uncertainty. It moves uncertainty to a place with less context.

At ingress you still know the provider, schema version, raw bytes, request identity, and failure semantics. Ten calls later, code sees only a suspicious `obj` and a convention that “this property should be here.” Failures then occur far from provenance, and every module pays again for a fact the adapter could have settled once.

Strong boundaries are a compression mechanism for reasoning. They turn “this unknown object probably has these fields in this combination” into “this is a `CompletedToolCall`.” The rest of the program can then spend attention on policy rather than transport archaeology.

## Repair Strategy
1. Identify the raw protocol owner and the exact inward boundary.
2. Decode `unknown`/dynamic values there.
3. Validate required fields, enum/case identity, cross-field invariants, and version semantics.
4. Normalize transport accidents such as casing, aliases, nullable encodings, or provider-specific status values.
5. Return closed typed cases plus any raw evidence that must be preserved separately.
6. Remove dynamic access/casts from inward callers.

## Decision Branches
- If malformed shape must be represented, keep it as a typed decode failure, not a half-decoded object.
- If the system must preserve unknown future variants, model `Unknown of RawEvidence` explicitly rather than letting every caller inspect arbitrary properties.
- If reflection is genuinely required by framework glue, keep it behind the glue boundary and expose a narrower contract inward.
- If a static primitive remains too weak after parsing, follow with `primitive-obsession`; do not conflate parsing with nominal identity.

## Common Wrong Fixes
- Rename `any` to `Payload` without changing what can be constructed.
- Put casts in a shared helper so the domain merely calls `asFoo()` everywhere.
- Validate one field while leaving contradictory combinations representable.
- Parse to a DTO where every field is optional “for compatibility,” then re-check it downstream.
- Use tests that bypass the decoder and directly manufacture impossible typed values.

## Verification
Search all inward layers for dynamic lookup, unchecked cast/unbox, wire field names, and repeated shape guards. They should disappear or be confined to explicitly generic infrastructure.

Feed malformed and unknown variants through the real ingress: they must fail or become an explicit `Unknown` case there. Valid input must emerge as a value downstream code can consume without re-validating its shape.

Invariant: **type uncertainty has one owner at ingress and cannot leak inward as a recurring proof obligation.**

## Done When
External uncertainty is localized, policy code receives facts rather than bags of possibility, and changing transport representation no longer forces semantic code to change merely because field access moved.