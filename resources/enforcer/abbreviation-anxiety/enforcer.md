# abbreviation-anxiety — Enforcer

## Definition
An abbreviation is harmful when understanding the name requires a private decoding step that the domain itself does not require. The defect is not shortness; it is hidden vocabulary.

## Governing Principle
A name is an address into the reader’s existing model. Good names reuse domain language so recognition is immediate. Private acronyms allocate a second language beside the first, forcing every reader to pay translation cost on every encounter. That tax compounds: the shorter the token, the more context must be reconstructed around it.

## Trigger When
Trigger when an unfamiliar, overloaded, or locally invented abbreviation appears in a name that carries domain meaning, and a reader must expand it before reasoning about the code.

## Do Not Trigger When
- The abbreviation is universal in the relevant domain and cannot plausibly be read another way there: HTTP, URL, UUID, CPU, SQL, and similar established vocabulary.
- The token is a well-known product, protocol, or standard identifier already used as the domain’s public name, not a local compression of a longer phrase.
- The identifier is a generated or wire code whose official spelling is the abbreviation, and expanding it would invent a private synonym.
- Short local loop variables or mathematical indices carry no domain meaning and do not require a decoding glossary.

## Distinguish From
`misleading-name` concerns a false claim about meaning or guarantee. `domain-language-drift` concerns inconsistent vocabulary across contexts. This rule concerns needless decoding even when the expansion itself is correct. Tie-break: if the spelled-out meaning is right but the token still requires a private cipher, this rule owns the case.

## Decision Procedure
1. Read the identifier without its implementation.
2. Ask whether the intended expansion is immediate to a competent domain reader.
3. If not, spell the concept as the domain names it.
4. Prefer a longer stable word over a shorter private cipher.

## Examples
- positive: `prcAmt` for process amount in a billing module; a reader must invent the expansion before reasoning.
- near-miss: `httpClient` using the domain’s public protocol name; no private decoding step exists.
- counterexample: renaming `prcAmt` to `processAmount` so the identifier is the domain term itself.

## Nudge
Remove the private decoding step. Use the full domain term unless the abbreviation is already part of the domain’s public language.
