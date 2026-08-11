# comment-theater — Enforcer

## Definition
Comment theater appears when prose repeats syntax, apologizes for design, or carries intent that the executable structure itself ought to make evident.

## Governing Principle
Comments and code age under different authorities: the compiler constrains one and ignores the other. Therefore every fact expressible in types, names, control structure, or tests is safer there than in prose. A comment earns its existence only when it records knowledge the program cannot naturally encode—why a constraint exists, which external fact forces it, or what tempting alternative is deliberately forbidden.

## Trigger When
Trigger when comments narrate the next line, translate poor names, explain tangled control flow, or say “this is ugly but” instead of repairing the representation.

## Do Not Trigger When
Do not trigger for durable rationale, protocol quirks, mathematical derivations, safety constraints, or external facts that cannot be inferred from the code alone.

## Distinguish From
stale-documentation concerns authoritative docs disagreeing with behavior. status-announcement-noise concerns progress chatter. This rule concerns comments being used as a substitute for legible structure.

## Decision Procedure
Delete the comment mentally. If the code becomes unclear, first ask whether naming, types, or decomposition can make the same fact mechanically visible. Keep prose only when the answer is no.

## Nudge
Do not narrate code that can speak for itself. Move intent into structure; reserve comments for durable knowledge the compiler cannot carry.
