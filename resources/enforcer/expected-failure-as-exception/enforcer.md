# expected-failure-as-exception — Enforcer

## Definition
An expected failure is misrepresented when a foreseeable domain outcome—unauthorized, not found, insufficient balance, invalid transition, conflict—is thrown as an exception instead of returned as part of the operation’s contract.

## Governing Principle
A function’s type should describe the worlds its caller must be prepared to inhabit. Foreseeable refusal is one of those worlds. Hiding it in an exception channel makes the signature overclaim success and lets callers accidentally ignore a required branch. Typed failure restores honesty: it turns policy from an ambient runtime surprise into an explicit obligation.

## Trigger When
Trigger when ordinary business rejection is thrown/caught as an exception or mapped to a generic exceptional channel.

## Do Not Trigger When
Do not trigger for infrastructure or programmer failures that make the requested operation impossible to reason about as an ordinary domain outcome.

## Distinguish From
exception-driven-control-flow covers ordinary branching generally. null-ambiguity hides several outcomes in absence. This rule specifically concerns expected business refusal.

## Decision Procedure
Ask whether the product can name the outcome before running the code and whether a caller has a legitimate response to it. If yes, give it a named result case.

## Nudge
Foreseeable refusal belongs in the contract. Return a closed typed outcome so every caller must confront the business branch explicitly.
