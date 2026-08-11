# dependency-bloat — Enforcer

## Definition
Dependency bloat occurs when a new library, plugin, service, or framework imports more lifecycle, compatibility, security, and upgrade obligations than the problem requires. The root-cause is that the project borrowed an ecosystem to avoid owning a capability the platform or a small local implementation already covers.

## Governing Principle
A dependency is borrowed code plus borrowed future. Its visible API is only the first cost; the project also inherits release cadence, transitive graph, vulnerabilities, platform assumptions, configuration, and eventual migration. The correct comparison is therefore not “lines avoided today” but “essential complexity removed over the dependency’s lifetime.”

## Trigger When
Trigger when a new dependency solves behavior already available in the platform or expressible safely by a small local implementation, while adding substantial ceremony or transitive surface.

## Do Not Trigger When
- The dependency owns genuinely difficult, security-sensitive, standards-heavy, or rapidly evolving complexity that would be irresponsible to reimplement locally.
- The package is already in the graph and the change only uses an existing capability without widening the transitive surface.
- A small, well-bounded library is the platform’s intended way to access a required standard (TLS, codecs, parsers) and local code would be weaker.
- The alternative is copying a large, independently evolving specification into the repo.

## Distinguish From
`framework-tax` concerns ceremony after adoption. `incidental-complexity-dominates` concerns overall design weight. This rule is the acquisition decision itself. Tie-break: if the mistake is importing an ecosystem to avoid a few local lines, this rule owns the case; if the ecosystem is already adopted and ceremony dominates, use `framework-tax`.

## Decision Procedure
Compare the full lifetime obligations of adoption against the complexity actually removed. Count transitive dependencies, runtime requirements, upgrade risk, and domain value—not just initial code size.

## Examples
- positive: adding a date-formatting framework and its plugins to wrap a one-line platform locale call.
- near-miss: adopting a maintained TLS or protocol library instead of hand-rolling cryptography.
- counterexample: implement the small local operation, or keep only a dependency that clearly owns durable complexity.

## Nudge
Buy complexity only when it is cheaper than owning it. If the platform or a small direct implementation already solves the problem, do not import an ecosystem to avoid a few lines.
