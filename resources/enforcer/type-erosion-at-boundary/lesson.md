# type-erosion-at-boundary — Lesson

## Opinion

### Definition

Type erosion occurs when uncertainty that belongs at an external boundary survives the boundary and keeps circulating inside code that is supposed to reason from domain facts.

The root cause is **unpaid parsing debt**: `any`, `unknown`, reflection, unchecked casts, string-key maps, generic JSON objects, or weak DTOs cross inward without being converted into a type whose constructors state what has actually been established.

### Governing Principle

External data is allowed to be uncertain. Domain policy is not required to pretend otherwise.

The mistake is not receiving weak bytes; every network, plugin, file, database row, and dynamic Host API starts that way somewhere. The mistake is failing to **spend the uncertainty at ingress**.

A good adapter consumes ambiguity. It validates shape, normalizes representation, maps external variants to internal cases, rejects malformed combinations, and returns a value downstream code can trust without reopening the wire contract.

A bad adapter merely changes the spelling: `any` becomes `Record<string,obj>`, `JsonElement`, `Map<string,string>`, or a huge “typed” DTO whose fields remain optional and unchecked. The uncertainty still owns the program; it has only acquired a type annotation.

### Trigger When

Trigger when:

- domain/application code uses `any`, dynamic property access, reflection, unchecked `unbox`/casts, or string-key lookup for semantic decisions;
- several inward callers repeat the same `typeof` / null / property-exists checks;
- policy code knows provider/JSON field names because no adapter translated them;
- a cast is justified by “we know the Host sends this shape” but no boundary proves it;
- malformed input can travel several layers before failing;
- tests construct fake dynamic bags rather than values admitted through the production constructor.

### Do Not Trigger When

- Dynamic representation is confined to a serializer/adapter and a validated domain value is returned.
- Runtime parsing is inherently necessary at ingress. Runtime validation is not a type-design failure; leaking unvalidated input inward is.
- Reflection is the actual domain capability of a generic framework boundary and no domain semantics depend on the reflected shape beyond that boundary.
- A raw payload is intentionally preserved **as evidence** while a separate typed interpretation drives control flow.

### Distinguish From

`weak-boundary-parsing` focuses on incomplete or fail-open validation of external shape. `primitive-obsession` concerns statically typed primitives that erase domain identity. `stringly-typed-error` is the special case where human prose becomes machine identity.

Tie-break: if the central defect is weak/dynamic representation leaking inward, use this rule. If parsing exists but accepts malformed/ambiguous wire shapes, use `weak-boundary-parsing`. If the value is statically a `string` but needs nominal identity, use `primitive-obsession`.

### Decision Procedure

Find the **last place that legitimately needs the raw representation**. Everything inward of that point should receive a type whose construction proves the assumptions those callers currently re-check.

For each cast or dynamic lookup, ask: what proposition are we assuming here? Move the proof of that proposition to the adapter and make it part of the returned type.

### Examples

- positive: an OpenCode hook object is passed through application services as `obj`; several modules read `sessionID` and `tool` dynamically and each assumes the same shape.
- positive: a JSON payload is deserialized to `Dictionary<string,obj>` and domain logic switches on string fields deep inside the workflow.
- near-miss: the adapter receives `unknown`, validates it with a decoder, preserves the raw payload for diagnostics, and returns `ProviderEvent` cases for policy.
- counterexample: a strong `UserId` is constructed at ingress but still wraps a string internally; representation remains primitive, semantics do not.

## Advice

### What To Do Now

Move parsing, validation, normalization, and unchecked operations to the adapter that owns the external protocol. Make that adapter return closed domain/application values whose construction proves the facts inward callers need.

The owner is the ingress boundary. Raw wire shape may belong there; it does not belong to every downstream policy function.

### Why This Matters

A cast does not remove uncertainty. It moves uncertainty to a place with less context.

At ingress you still know the provider, schema version, raw bytes, request identity, and failure semantics. Ten calls later, code sees only a suspicious `obj` and a convention that “this property should be here.” Failures then occur far from provenance, and every module pays again for a fact the adapter could have settled once.

Strong boundaries are a compression mechanism for reasoning. They turn “this unknown object probably has these fields in this combination” into “this is a `CompletedToolCall`.” The rest of the program can then spend attention on policy rather than transport archaeology.

### Repair Strategy

1. Identify the raw protocol owner and the exact inward boundary.
2. Decode `unknown`/dynamic values there.
3. Validate required fields, enum/case identity, cross-field invariants, and version semantics.
4. Normalize transport accidents such as casing, aliases, nullable encodings, or provider-specific status values.
5. Return closed typed cases plus any raw evidence that must be preserved separately.
6. Remove dynamic access/casts from inward callers.

### Decision Branches

- If malformed shape must be represented, keep it as a typed decode failure, not a half-decoded object.
- If the system must preserve unknown future variants, model `Unknown of RawEvidence` explicitly rather than letting every caller inspect arbitrary properties.
- If reflection is genuinely required by framework glue, keep it behind the glue boundary and expose a narrower contract inward.
- If a static primitive remains too weak after parsing, follow with `primitive-obsession`; do not conflate parsing with nominal identity.

### Common Wrong Fixes

- Rename `any` to `Payload` without changing what can be constructed.
- Put casts in a shared helper so the domain merely calls `asFoo()` everywhere.
- Validate one field while leaving contradictory combinations representable.
- Parse to a DTO where every field is optional “for compatibility,” then re-check it downstream.
- Use tests that bypass the decoder and directly manufacture impossible typed values.

### Verification

Search all inward layers for dynamic lookup, unchecked cast/unbox, wire field names, and repeated shape guards. They should disappear or be confined to explicitly generic infrastructure.

Feed malformed and unknown variants through the real ingress: they must fail or become an explicit `Unknown` case there. Valid input must emerge as a value downstream code can consume without re-validating its shape.

Invariant: **type uncertainty has one owner at ingress and cannot leak inward as a recurring proof obligation.**

### Done When

External uncertainty is localized, policy code receives facts rather than bags of possibility, and changing transport representation no longer forces semantic code to change merely because field access moved.

### Nudge

Dynamic data may enter the system. It should not acquire squatter's rights.

Spend uncertainty once, where provenance and raw shape are still visible. Let typed facts, not repeated casts, travel inward.
