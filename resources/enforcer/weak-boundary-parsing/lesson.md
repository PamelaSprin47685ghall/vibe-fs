# weak-boundary-parsing — Lesson

## Opinion

### Definition

Boundary parsing is weak when untrusted or cross-language data enters the system without being normalized into a strong internal model, forcing deeper layers to rediscover its shape and validity repeatedly.

### Governing Principle

Ingress is the moment of maximum evidence and minimum trust. The raw payload, protocol context, schema version, and validation errors are all still available there. The root-cause is delaying interpretation past the moment of maximum evidence. That move sends ambiguity inward while discarding provenance, so every consumer performs partial parsing under less information. A strong boundary turns an uncertain external proposition into a validated internal fact exactly once.

### Trigger When

Trigger when dictionaries, loosely typed JSON, optional-field bags, raw strings, or cross-language payloads circulate beyond their adapter and downstream code repeatedly checks fields, formats, or variants.

### Do Not Trigger When

- Input is parsed, validated, normalized, and converted at ingress into domain types whose constructors express the guarantees internal code relies on.
- Internal layers receive already-validated domain values and do not re-interrogate payload shape.
- The protocol owner retains raw bytes at the same boundary for signature or checksum verification, then still constructs the strong type before leaving.
- Tests drive the adapter with raw fixtures and assert typed outcomes at the boundary.

### Distinguish From

`type-erosion-at-boundary` lets dynamic types leak inward after decoding. `stringly-typed-error` makes prose a control protocol. Tie-break: if the defect is delayed or repeated interpretation of external input itself, use this rule; if decoded data remains `any`/unchecked inward, use `type-erosion-at-boundary`.

### Decision Procedure

Identify the first trusted boundary. Enumerate the external alternatives there, reject malformed values, normalize representation, and construct the strongest internal type justified by the evidence.

### Examples

- positive: handlers pass `dict` JSON through services that each call `if "email" in body`.
- near-miss: the adapter validates and returns `Email` / `Order`, and services never see the raw map.
- counterexample: retry logic parses `e.message` for `"timeout"` — that is `stringly-typed-error`.

## Advice

### What To Do Now

Parse and validate external data once at ingress, then expose a normalized strong internal type instead of passing raw payload shape deeper into the system. Repair at the adapter: who owns ingress owns the conversion from payload to domain type.

### Why This Matters

Repeated parsing means repeated uncertainty. Each downstream layer knows less about provenance yet must reconstruct the same distinctions, so validation drifts and malformed states travel farther before failing. A boundary should exchange ambiguity for guarantees.

### Repair Strategy

Decode schema/version, validate required relations, normalize units/names, and construct domain values at the adapter. Keep raw protocol forms private unless a lower layer is explicitly the protocol owner.

### Decision Branches

If raw or loosely typed payload is visible past ingress, parse and construct the strong type at the adapter.
If a layer is the protocol owner (signatures, framing), keep raw bytes there and still emit the domain type before other code runs.

### Common Wrong Fixes

- Scatter helper predicates such as `hasField` or `isValidShape` across consumers.
- Validate in the controller and still pass the original map inward.
- Treat a TypeScript interface on `any` JSON as parsing.

### Verification

Invariant: the system crosses from weak external representation to strong internal meaning at one identifiable boundary. Malformed and unsupported payloads should fail at ingress with typed outcomes; valid payloads should require no repeated shape checks once inside.

### Done When

The system crosses from weak external representation to strong internal meaning at one identifiable boundary and never has to rediscover that meaning downstream.

### Nudge

Parse where uncertainty enters. Turn external shape into internal meaning once, at the boundary, so the rest of the system reasons about facts rather than repeatedly interrogating payloads.
