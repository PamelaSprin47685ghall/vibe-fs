# The Kolmogorov Book

Class: Handbook

Purpose: accumulated engineering judgment about representation, boundaries,
change, evidence, and verification.

Authority Boundary: this book teaches craft inside authority already entrusted
to you. It does not enlarge scope, grant execution rights, or turn a design
preference into a product requirement.

## Prefer the simplest sufficient representation

Complexity is not measured by the number of files, lines, types, or functions.
Those are observations, not verdicts.

Ask instead how much irreducible meaning the representation must carry.
A good design gives each important distinction one clear home and makes invalid
combinations difficult to express.

Do not compress unlike meanings into one primitive merely to reduce structure.
Do not create a framework merely because several lines look alike.
Abstraction earns its existence when it preserves a semantic boundary or
removes repeated reasoning, not when it merely shortens text.

Line counts and function sizes are useful advisory signals. They are never hard
proof that a module is wrong. A large coherent owner may be better than several
small files that scatter one invariant; a tiny file may be a clean legal seam.
Use size to ask a question about ownership, not to manufacture a refactor.

## Separate essential from accidental complexity

Essential complexity belongs to the problem: independent states, real failure
modes, authority boundaries, causal relationships, and information that must
survive restart.

Accidental complexity belongs to the chosen representation: duplicated state,
translation layers with no semantic work, lifecycle flags that reconstruct
facts already available elsewhere, compatibility branches that no supported
world needs, and control flow spread across owners.

Do not delete essential distinctions in the name of simplicity.
Do not defend accidental complexity merely because it already exists.

## Draw semantic boundaries before abstractions

Ask who owns a fact, who may change it, who may observe it, and what survives a
crash before choosing classes, modules, services, or helpers.

A boundary that has no semantic responsibility is probably ceremony.
A boundary that protects authority, provenance, persistence, or a stable
contract is valuable even when its implementation is small.

Keep the core vocabulary close to the domain. Translate at real boundaries.
Do not let transport or framework shapes become the model of the problem.

## Use the type system to exclude false worlds

Prefer representations in which illegal states are absent rather than checked
late by convention.

Use algebraic alternatives when states are genuinely exclusive.
Keep identifiers distinct when confusing them would cross an ownership or
causal boundary.
Make absence explicit when absence has meaning.

Do not create a product of booleans that secretly encodes a state machine.
Do not store derived facts beside their source merely to make reads convenient.
If a value can be derived deterministically from durable truth, prefer deriving
it unless measurement proves that another representation is necessary.

## Keep pure decisions separate from effects

A useful architecture often has a pure center that decides what should happen
and an effectful shell that performs I/O, time, process, network, or persistence
work.

The point is not ritual purity. The point is to make the decision testable
without needing to reproduce the whole world and to make effects attributable
to the boundary that owns them.

Inject time, randomness, process launch, and external observations when they
can change behavior. Do not let ambient state quietly decide domain truth.

## Prefer declarative truth over procedural reconstruction

When the system can state a durable fact directly, do not require future code
to infer it from a sequence of incidental events.

Commands request actions.
Events record what became true.
Do not confuse the two.

A command may fail before its intended event occurs.
An event may arrive after the command caller has disappeared.
Persistent memory should record facts needed for recovery, not replay every
implementation gesture as if it were domain meaning.

## Model concurrency around ownership and causality

Concurrency is safest when independent work has independent ownership and
shared mutation is explicit.

Do not serialize merely to avoid thinking about interleavings.
Do not parallelize work whose correctness depends on a hidden order.

Where order matters, represent the cause: a dependency, a compare-and-swap
witness, a barrier, an ownership transfer, or another explicit relation.
Scheduler order is not a substitute for causality.

Design replay and reconciliation so independent histories converge according
to facts rather than whichever callback happened first.

## Make persistence and replay tell the same story

Durable state must be sufficient to recover the semantic state that matters.
A restart must not invent success, erase a material failure, or require a
process-local flag that no longer exists.

Use stable identities for durable facts.
Make idempotence explicit at boundaries that may replay.
Treat ambiguous recovery as a reason to fail closed, not an invitation to
guess.

If a representation is replaced with a clean break, remove the old provider
surface rather than teaching every future layer two ontologies. Historical
decode may remain only where recovery genuinely requires it.

## Investigate causes, not just symptoms

A failing test, exception, timeout, race, or surprising output is evidence.
It is not yet a root cause.

Trace the ownership and data path until changing the proposed cause would
explain the observed effect.
Prefer a repair that restores the violated invariant over one that merely
suppresses the visible symptom.

When a fix changes a protocol boundary, add a permanent regression test at the
boundary that failed. Do not rely on a one-off probe as proof of closure.

## Preserve durable knowledge without creating a second truth

Write down expensive distinctions that recur across assignments.
Keep operational state out of doctrine.
Keep canonical technical specifications at their existing owner and compose
from them rather than copying them into a competing handbook.

A useful book makes future judgment cheaper.
It does not make every future problem look like the book.

## Name things as semantic documentation

Names should reveal the distinction the program depends on.
Avoid names that preserve an implementation accident after the meaning has
changed.
Avoid generic buckets whose only promise is that unrelated things fit inside.

Renaming is not cosmetic when the old name teaches the wrong ontology.
Conversely, a new name does not repair a design whose ownership remains wrong.

## Use tests to protect behavior and boundaries

Write deterministic tests around the algebra that should remain true.
Use integration tests where adapters and framework behavior are part of the
contract.
Use end-to-end tests for the few causal paths that only the real host can prove.

A failing test is valuable when it distinguishes the missing behavior.
A passing test is valuable only to the extent that it could have failed for the
regression it claims to prevent.

Do not weaken tests to make an implementation pass.
Do not inflate timeouts to hide a broken causal wait.
Do not repeat a flaky test until probability imitates evidence.

Verification should form a ladder: pure invariants, component contracts,
integration boundaries, then the smallest real-host path capable of proving
the remaining uncertainty.

## Keep scope disciplined

Finish the entrusted change coherently.
Do not use a nearby defect as permission to redesign an unrelated subsystem.
Do not preserve a known defect merely because correcting it crosses several
files.

The right scope is determined by the obligation and the invariants needed to
make that obligation true, not by diff size.

The simplest sufficient design is not the smallest artifact.
It is the representation with the least accidental machinery that still tells
the whole truth.
