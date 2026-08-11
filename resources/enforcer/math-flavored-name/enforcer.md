# math-flavored-name — Enforcer

## Definition
A math-flavored name uses symbols, single letters, or abstract algebraic vocabulary where the code has no corresponding mathematical model and the notation hides an ordinary domain concept.

## Governing Principle
Mathematical notation is powerful because a compact symbol recalls a precisely defined structure. Without that shared structure, compactness becomes ambiguity: `x`, `f`, `Δ`, or “monoid” saves characters by forcing the reader to reverse-engineer what concrete thing the symbol stands for. Compression is valuable only after meaning is stable and communal.

## Trigger When
Trigger when abstract or single-letter names appear in ordinary domain code and readers need implementation context to infer their real business meaning.

## Do Not Trigger When
Do not trigger inside genuine mathematical algorithms where notation is standard, locally defined, and maps directly to the formal model being implemented.

## Distinguish From
abbreviation-anxiety concerns private shorthand generally. misleading-name concerns a false semantic claim. This rule is specifically pseudo-mathematical compression without mathematical payoff.

## Decision Procedure
Ask whether a domain expert or algorithm reader would naturally use this symbol for the same concept. If not, name the concrete thing the code manipulates.

## Nudge
Use mathematical notation only where mathematics supplies the shared meaning. Otherwise name the domain fact directly and remove gratuitous decoding.
