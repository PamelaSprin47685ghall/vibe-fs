# dependency-bloat — Enforcer

## Definition
Dependency bloat occurs when a new library, plugin, service, or framework imports more lifecycle, compatibility, security, and upgrade obligations than the problem requires.

## Governing Principle
A dependency is borrowed code plus borrowed future. Its visible API is only the first cost; the project also inherits release cadence, transitive graph, vulnerabilities, platform assumptions, configuration, and eventual migration. The correct comparison is therefore not “lines avoided today” but “essential complexity removed over the dependency’s lifetime.”

## Trigger When
Trigger when a new dependency solves behavior already available in the platform or expressible safely by a small local implementation, while adding substantial ceremony or transitive surface.

## Do Not Trigger When
Do not trigger when the dependency owns genuinely difficult, security-sensitive, standards-heavy, or rapidly evolving complexity that would be irresponsible to reimplement locally.

## Distinguish From
framework-tax concerns ceremony after adoption. incidental-complexity-dominates concerns overall design weight. This rule is the acquisition decision itself.

## Decision Procedure
Compare the full lifetime obligations of adoption against the complexity actually removed. Count transitive dependencies, runtime requirements, upgrade risk, and domain value—not just initial code size.

## Nudge
Buy complexity only when it is cheaper than owning it. If the platform or a small direct implementation already solves the problem, do not import an ecosystem to avoid a few lines.
