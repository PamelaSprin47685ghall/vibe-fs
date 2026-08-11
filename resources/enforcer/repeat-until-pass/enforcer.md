# repeat-until-pass — Enforcer

## Definition
Repeat-until-pass treats a lucky successful run as verification after identical or equivalent runs have already failed nondeterministically.

## Governing Principle
A repeated experiment does not become more true because one sample is favorable. If the relevant inputs are unchanged yet verdicts differ, the unresolved fact is nondeterminism. Selecting the green sample is statistical cherry-picking: it discards evidence precisely because that evidence is inconvenient. Correct verification removes the hidden variable, not the red observations.

## Trigger When
Trigger when a failing test/command is rerun until success and the eventual green run is accepted without explaining or eliminating the earlier failure.

## Do Not Trigger When
Do not trigger for a bounded retry of known transient infrastructure outside the system under test when the retry policy itself is explicit and final failure remains visible.

## Distinguish From
flaky-test-tolerated accepts unstable tests as policy. timeout-inflated-to-pass changes budgets. This rule is the act of sampling repeatedly until the desired verdict appears.

## Decision Procedure
Stop after the first unexplained nondeterministic failure. Capture inputs/environment, reproduce, find the hidden variable, and make one run deterministic before accepting green.

## Nudge
Do not choose evidence by outcome. One unexplained red invalidates a lucky green until the nondeterminism has a cause and a fix.
