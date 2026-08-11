# exception-driven-control-flow — Enforcer

## Definition
Exception-driven control flow uses stack unwinding to represent an ordinary branch the program expects to take as part of normal operation.

## Governing Principle
Exceptions deliberately erase local control structure: they jump across frames until some handler recognizes them. That power is appropriate when normal reasoning has broken down. Using it for absence, iteration, branching, or expected retry turns ordinary policy into non-local control flow, so a reader cannot discover possible outcomes from the function’s type or immediate syntax.

## Trigger When
Trigger when code intentionally throws and catches exceptions to express common alternatives such as “not found,” loop termination, optional parsing, branch selection, or routine retry.

## Do Not Trigger When
- The condition truly violates assumptions required to continue and the ordinary caller cannot reasonably recover as part of its domain flow.
- An adapter translates a foreign exception-based API once at the boundary into local typed outcomes.
- A programmer error (broken invariant, impossible state) is signaled exceptionally because continuation is meaningless.
- Tests use expected-exception assertions for those genuine exceptional failures, not as a stand-in for ordinary branches.

## Distinguish From
`expected-failure-as-exception` focuses on foreseeable business failures. This rule is broader: even ordinary nonbusiness branches become exceptions. `stringly-typed-error` concerns parsing prose after failure. Tie-break: if throw/catch encodes a common alternative (absence, loop end, retry) rather than a named business refusal, this rule owns the case.

## Decision Procedure
Ask whether callers are expected to encounter and handle the outcome during normal operation. If yes, make it an explicit return case or branch rather than hidden stack control.

## Examples
- positive: a parser throws `StopIteration`-like exceptions to end a loop, and callers catch them as the success path.
- near-miss: a disk I/O failure throws because the operation cannot continue as ordinary domain flow.
- counterexample: return option/result, use ordinary branching, or an iterator protocol for the expected alternative.

## Nudge
Reserve non-local control for genuinely exceptional failure. Represent ordinary alternatives where callers can see them—in the type and local control flow.
