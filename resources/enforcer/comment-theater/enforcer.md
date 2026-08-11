# comment-theater — Enforcer

## Definition
Comment theater appears when prose repeats syntax, apologizes for design, or carries intent that the executable structure itself ought to make evident.

## Governing Principle
Comments and code age under different authorities: the compiler constrains one and ignores the other. Therefore every fact expressible in types, names, control structure, or tests is safer there than in prose. A comment earns its existence only when it records knowledge the program cannot naturally encode—why a constraint exists, which external fact forces it, or what tempting alternative is deliberately forbidden.

## Trigger When
Trigger when comments narrate the next line, translate poor names, explain tangled control flow, or say “this is ugly but” instead of repairing the representation.

## Do Not Trigger When
- The comment records durable rationale, protocol quirks, mathematical derivations, safety constraints, or external facts that cannot be inferred from the code alone.
- A license header, copyright, or required legal notice is not theater.
- A public API doc that states caller-facing contracts not visible from signatures alone is documentation, not narration of the next line.
- Linking to an external spec or incident is knowledge the compiler cannot carry.

## Distinguish From
`stale-documentation` concerns authoritative docs disagreeing with behavior. `status-announcement-noise` concerns progress chatter. This rule concerns comments being used as a substitute for legible structure. Tie-break: if deleting the prose would be fixed by better names, types, or control flow, this rule owns the case.

## Decision Procedure
Delete the comment mentally. If the code becomes unclear, first ask whether naming, types, or decomposition can make the same fact mechanically visible. Keep prose only when the answer is no.

## Examples
- positive: `// increment i by one` above `i += 1`, or a paragraph explaining a poor name instead of renaming.
- near-miss: a one-line note that a magic timeout is required by a named external protocol.
- counterexample: remove the narrating comments and make names, types, and structure carry the intent.

## Nudge
Do not narrate code that can speak for itself. Move intent into structure; reserve comments for durable knowledge the compiler cannot carry.
