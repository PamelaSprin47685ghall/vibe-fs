# exception-driven-control-flow — Enforcer

## Definition
Exception-driven control flow uses stack unwinding to represent an ordinary branch the program expects to take as part of normal operation.

## Governing Principle
Exceptions deliberately erase local control structure: they jump across frames until some handler recognizes them. That power is appropriate when normal reasoning has broken down. Using it for absence, iteration, branching, or expected retry turns ordinary policy into non-local control flow, so a reader cannot discover possible outcomes from the function’s type or immediate syntax.

## Trigger When
Trigger when code intentionally throws and catches exceptions to express common alternatives such as “not found,” loop termination, optional parsing, branch selection, or routine retry.

## Do Not Trigger When
Do not trigger when an exceptional condition truly violates assumptions required to continue and the ordinary caller cannot reasonably recover as part of its domain flow.

## Distinguish From
expected-failure-as-exception focuses on foreseeable business failures. This rule is broader: even non-business ordinary branches become exceptions. stringly-typed-error concerns parsing prose after failure.

## Decision Procedure
Ask whether callers are expected to encounter and handle the outcome during normal operation. If yes, make it an explicit return case or branch rather than hidden stack control.

## Nudge
Reserve non-local control for genuinely exceptional failure. Represent ordinary alternatives where callers can see them—in the type and local control flow.
