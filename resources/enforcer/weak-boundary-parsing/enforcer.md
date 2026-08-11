# weak-boundary-parsing — Enforcer

## Definition
Boundary parsing is weak when untrusted or cross-language data enters the system without being normalized into a strong internal model, forcing deeper layers to rediscover its shape and validity repeatedly.

## Governing Principle
Ingress is the moment of maximum evidence and minimum trust. The raw payload, protocol context, schema version, and validation errors are all still available there. Delaying interpretation moves ambiguity inward while discarding provenance, so every consumer performs partial parsing under less information. A strong boundary turns an uncertain external proposition into a validated internal fact exactly once.

## Trigger When
Trigger when dictionaries, loosely typed JSON, optional-field bags, raw strings, or cross-language payloads circulate beyond their adapter and downstream code repeatedly checks fields, formats, or variants.

## Do Not Trigger When
Do not trigger when input is parsed, validated, normalized, and converted at ingress into domain types whose constructors express the guarantees internal code relies on.

## Distinguish From
type-erosion-at-boundary lets dynamic types leak inward after decoding. stringly-typed-error makes prose a control protocol. This rule concerns delayed or repeated interpretation of external input itself.

## Decision Procedure
Identify the first trusted boundary. Enumerate the external alternatives there, reject malformed values, normalize representation, and construct the strongest internal type justified by the evidence.

## Nudge
Parse where uncertainty enters. Turn external shape into internal meaning once, at the boundary, so the rest of the system reasons about facts rather than repeatedly interrogating payloads.
