# manual-toil-repeat — Enforcer

## Definition
Manual toil repeats when a deterministic mechanical procedure is performed by hand again despite having stable inputs, steps, and outputs suitable for automation.

## Governing Principle
Repetition reveals an algorithm. Once the same human sequence is executed several times, the organization is using attention as an interpreter for a program it has refused to write down. Human execution adds variance and consumes scarce judgment on work whose defining feature is that judgment is no longer needed.

## Trigger When
Trigger when the same mechanical edit, generation, validation, packaging, migration, or operational procedure is repeatedly executed by humans with materially identical rules.

## Do Not Trigger When
- Do not trigger when each instance genuinely requires contextual judgment that cannot be stated as stable input/output rules, or when automation cost clearly exceeds expected repetition.
- Do not trigger for a first-time procedure still being discovered (no stable algorithm yet).
- Do not trigger when a maintained tool already exists and the human work is supervising exceptions it cannot encode.

## Distinguish From
leftover-scaffolding is temporary tooling that should disappear or be promoted. serial-investigation concerns independent research steps. This rule is recurring deterministic labor itself. Tie-break: if the work is investigation with unknown next questions, use serial-investigation; if the steps are a known algorithm repeated by hand, use this rule.

## Decision Procedure
Write the procedure as inputs → deterministic steps → outputs. If that description is stable and has recurred enough to create error or attention cost, the root-cause is humans interpreting an unwritten program: encode it as a maintained tool or project check. Prefer this over leftover-scaffolding when the defect is recurring labor, not an artifact that should disappear.

## Examples
- positive: Every release, a human copies the same five files, edits the same version strings, and runs the same checklist.
- near-miss: A one-off migration is still being discovered; the next instance will differ.
- counterexample: A maintained command takes the same inputs and produces the same outputs; humans review exceptions only.

## Nudge
When work becomes algorithmic, stop spending human judgment to execute it. Encode the procedure once and let people supervise exceptions rather than repeat mechanics.
