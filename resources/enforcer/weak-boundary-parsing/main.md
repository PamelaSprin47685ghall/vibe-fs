# weak-boundary-parsing — Main

## What To Do Now
Parse and validate at the edge into domain types. Downstream code accepts only those types. Reject invalid payloads before business logic.

## Repair Strategy
Introduce schema validation at ingress. Replace `dict`/`JsonElement` plumbing with typed models. Centralize codecs.

## Decision Branches
If multiple ingress points share a shape, share one codec. If partial payloads are allowed, model optionality explicitly in the type.

## Wrong Fixes
Passing raw JSON through many layers. Re-validating differently in each feature. Trusting query strings as ints without parse.

## Verification
Invalid payloads fail at the boundary; domain functions never see raw untrusted shapes.

## Done When
Ingress is normalized once; internal code is strongly typed against the result.

## Scope and Authority
Untrusted and cross-language ingress boundaries.
