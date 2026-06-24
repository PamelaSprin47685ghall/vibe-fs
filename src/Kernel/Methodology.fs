module VibeFs.Kernel.Methodology

open VibeFs.Kernel.Messaging
open VibeFs.Kernel.HostTools

let selectMethodologyToolName = "select_methodology"

let methodologyEnumValues: string list =
    [ "first_principles"
      "axiomatization"
      "deduction"
      "induction"
      "abduction"
      "analogy"
      "specialization"
      "generalization"
      "working_backwards"
      "analysis_synthesis"
      "auxiliary_construction"
      "equivalent_transformation"
      "decomposition_recombination"
      "model_problem_transfer"
      "constructive_method"
      "reductio_ad_absurdum"
      "invariance"
      "symmetry_analysis"
      "dimensional_reduction"
      "perturbation_continuity"
      "pigeonhole_principle"
      "duality"
      "quotient_space"
      "category_mapping"
      "relaxation"
      "search_space_exploration"
      "branch_and_bound"
      "dynamic_programming"
      "monte_carlo_sampling"
      "simulated_annealing"
      "swarm_optimization"
      "systems_thinking"
      "root_cause_analysis"
      "state_machine_reasoning"
      "type_driven_design"
      "event_sourcing"
      "operationalism"
      "bayesian_update"
      "falsification"
      "thought_experiment"
      "transcendental_argument"
      "conceptual_analysis"
      "dialectical_analysis"
      "hermeneutic_circle"
      "deconstruction"
      "renormalization"
      "simplification"
      "tradeoff_analysis"
      "risk_analysis"
      "test_driven_reasoning"
      "debugging_trace"
      "security_review"
      "performance_analysis"
      "user_intent_clarification" ]

let methodologyCatalog =
    """Select the reasoning methodologies that should guide the next work step. Use this before continuing when the task benefits from explicit structure, search-space control, proof discipline, design reasoning, decomposition, verification, or risk control. Choose all useful methodologies by their definitions, not by keyword vibes.

Methodology catalog:
Common methods can be selected with their short definitions. Uncommon methods include trigger conditions; do not avoid them merely because they are less familiar.

- first_principles: reduce the problem to irreducible facts and rebuild from them.
- deduction: derive necessary conclusions from accepted premises.
- induction: infer a general rule from repeated cases or patterns.
- analogy: transfer a known solution structure from a genuinely similar problem.
- specialization: inspect simple, concrete, boundary, or extreme cases before generalizing.
- generalization: widen the problem to expose the underlying structure.
- working_backwards: start from the desired end state and derive prerequisites.
- decomposition_recombination: split the object into parts and reconnect them in a better structure.
- constructive_method: build the required object, algorithm, or witness directly.
- reductio_ad_absurdum: assume the negation and derive a contradiction.
- search_space_exploration: model candidates as a space or graph and choose a traversal strategy.
- branch_and_bound: prune impossible or dominated branches using bounds.
- dynamic_programming: exploit overlapping subproblems and memoized state transitions.
- systems_thinking: model feedback loops, dependencies, delays, and emergent behavior.
- root_cause_analysis: trace symptoms back to the causal fault, not the visible failure.
- state_machine_reasoning: enumerate legal states, transitions, and impossible states.
- type_driven_design: encode domain boundaries and illegal states in types.
- event_sourcing: separate commands from facts and derive current state from event history.
- bayesian_update: update belief strength as evidence arrives.
- simplification: remove accidental complexity until the essential problem remains.
- tradeoff_analysis: compare options across explicit constraints and costs.
- risk_analysis: identify failure modes, blast radius, and irreversible decisions.
- test_driven_reasoning: make expected behavior executable before or during implementation.
- debugging_trace: reproduce, isolate, instrument, and verify the fault chain.
- security_review: reason adversarially about trust boundaries and abuse paths.
- performance_analysis: locate bottlenecks, asymptotics, and resource constraints.
- user_intent_clarification: resolve ambiguous goals before optimizing the wrong target.

- axiomatization: use when hidden assumptions or drifting definitions make reasoning unstable. State primitive terms, allowed operations, invariants, forbidden states, and derivation rules explicitly; then solve only inside that declared system.
- abduction: use when debugging, diagnosing, investigating, or explaining surprising evidence. Generate the best causal hypothesis: if X were true, observed Y would be expected; then seek discriminating tests rather than treating the hypothesis as proven.
- analysis_synthesis: use when a goal is clear but the path is not. First analyze backward from the desired result until reaching known facts or constructible conditions; then synthesize forward into the actual proof, algorithm, or implementation plan.
- auxiliary_construction: use when known facts and unknown target have no direct bridge. Introduce a helper line, variable, function, data structure, invariant, lemma, adapter, or intermediate representation that exposes a hidden relation.
- equivalent_transformation: use when the current representation is noisy. Convert the problem into an equivalent form, such as algebra to geometry, state updates to events, recursion to graph search, or protocol text to typed algebra.
- model_problem_transfer: use when the problem resembles a known canonical template. Identify the shared unknowns, constraints, and topology, then transfer the solution skeleton while checking which assumptions fail.
- invariance: use when a system undergoes operations, rewrites, moves, or state transitions. Find what cannot change, such as parity, ordering, conservation, type identity, ownership, permission boundary, or event prefix integrity.
- symmetry_analysis: use when many cases appear distinct but may be equivalent. Exploit symmetry to collapse cases; inspect symmetry breaking when bugs or edge cases arise from one side no longer matching the other.
- dimensional_reduction: use when the full state space is too large. Project to a lower-dimensional view, summary statistic, slice, trace, minimal reproduction, or quotient; reason there, then lift the conclusion back cautiously.
- perturbation_continuity: use when a hard case is near an easy case. Change one variable slightly, vary load, remove one constraint, or move along a continuous path to see which properties survive and where the phase change occurs.
- pigeonhole_principle: use when exact placement is unknown but counts, capacities, or partitions are known. Prove that collision, overflow, repetition, or coverage must occur because there are more items than distinguishable slots.
- duality: use when the direct problem is hard but a mirrored constraint or optimization problem is easier. Solve the shadow problem: producer/consumer, read/write, syntax/semantics, primal/dual, local/global, command/event.
- quotient_space: use when too many objects differ only in irrelevant detail. Define an equivalence relation, collapse each class to one representative, solve on classes, then map results back to concrete cases.
- category_mapping: use when structure matters more than object internals. Preserve relationships and transformations while moving the problem into a domain with stronger tools, such as graphs, types, events, algebra, or state machines.
- relaxation: use when hard constraints make direct search impossible. Temporarily weaken integer, ordering, permission, timing, or exactness constraints; solve the easier superset; then project, round, or validate back under real constraints.
- monte_carlo_sampling: use when the space is too large for exhaustive reasoning and approximate confidence is acceptable. Sample many plausible paths or cases, look for stable signals, then verify critical findings deterministically.
- simulated_annealing: use when greedy search gets trapped in local optima. Early exploration may accept worse intermediate states to escape traps; later narrow the search to refine the best candidate.
- swarm_optimization: use when multiple candidate directions can search in parallel. Let independent hypotheses, agents, examples, or solution drafts explore, share best findings, and converge without central overcommitment too early.
- operationalism: use when terms are vague or metaphysical. Define each concept by the observable operation that detects, measures, or changes it; discard distinctions that create no behavioral difference.
- falsification: use when claims risk becoming unfalsifiable stories. Formulate a hypothesis with clear failure conditions, actively search for counterexamples, and keep it only while it survives severe tests.
- thought_experiment: use when real execution is costly, dangerous, or impossible. Push an idealized or extreme scenario through the rules to test concept boundaries, contradictions, and hidden assumptions.
- transcendental_argument: use when an accepted fact is undeniable but its preconditions are hidden. Ask what structures must already exist for this fact, behavior, or experience to be possible at all.
- conceptual_analysis: use when confusion may come from language, not facts. Clarify meanings, category boundaries, and scope; remove category mistakes such as treating a process, relation, or aggregate as a separate concrete object.
- dialectical_analysis: use when opposing forces both shape the outcome. Identify thesis, antithesis, tension, dependency, and resolution path instead of flattening the system into one-sided causality.
- hermeneutic_circle: use when local meaning depends on global context and global meaning depends on local details. Iterate between part and whole until both stabilize, especially for codebase understanding and document interpretation.
- deconstruction: use when a design or text relies on hidden binaries or authority. Inspect what the framing excludes, which hierarchy it assumes, and where internal tensions make the apparent center unstable.
 - renormalization: use when micro-detail overwhelms macro behavior. Coarse-grain details, preserve only scale-relevant variables, and identify the universal structure that remains stable across levels."""

let selectMethodologyFieldDescription =
    "Required when calling this tool: record `select_methodology` with one or more methodology names that must guide the next work step. Choose by definitions, not by keyword vibes.\n\n"
    + methodologyCatalog
