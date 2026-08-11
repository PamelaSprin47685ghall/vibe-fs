# stringly-typed-error — Enforcer

## Definition
An error is stringly typed when program behavior depends on parsing, matching, or recognizing human-readable error prose rather than a stable closed error value.

## Governing Principle
Presentation text and control information have different audiences and different stability requirements. Prose evolves for clarity, localization, and diagnostics; control values must remain unambiguous under those changes. Parsing text couples machine semantics to editorial wording, turning punctuation and phrasing into undocumented protocol fields.

## Trigger When
Trigger when callers branch on error substrings, regexes, exact messages, localization text, or exception prose to decide retry, status, authorization, or recovery behavior.

## Do Not Trigger When
Do not trigger when strings are produced only after the caller has already matched a typed error code/case and are used solely for human display or diagnostics.

## Distinguish From
weak-boundary-parsing leaves general input shape untyped. expected-failure-as-exception chooses the wrong failure channel. This rule specifically makes human prose carry machine-control identity.

## Decision Procedure
List the program decisions derived from the message. Define one closed error case/code for each semantic distinction and format prose only after control flow has matched the case.

## Nudge
Machines need identities; humans need explanations. Branch on a typed error value and generate prose afterward—never make wording itself the protocol.
