module Wanxiangshu.Methodology.Catalog2

open Wanxiangshu.Methodology.SchemaCommon

let schemas: MethodologySchema list = [
    buildSchema
        "model_problem_transfer"
        "Transfer a solution skeleton from a canonical template when topology matches."
        "When the task resembles a well-known pattern: plugin adapter, state machine, codec boundary."
        [ reqStr "canonical_template" "Named pattern (e.g. ToolCatalog→ToolSchema, review replay, fuzzy iterator)."
          reqStr "current_problem" "What you are solving now in this repo."
          reqArr "shared_unknowns" 2 "Unknowns that match the template (schema gen, host obj, async queue)."
          reqArr "shared_constraints" 2 "Constraints that match (Fable, dual host, pure kernel)."
          reqArr "transfer_steps" 3 "Skeleton steps copied from template with file-level targets."
          reqArr "assumption_failures" 1 "Template assumptions that fail here and require local redesign."
          optStr "reference_implementation" "Path to the canonical code to mirror."
          optArr "checklist" 2 "Per-step done criteria." ]
        "Map current work onto a canonical repo pattern and flag broken assumptions."
        [ "Template mapping"
          "Transfer steps"
          "Failed assumptions"
          "Checklist"
          "Next actions" ]
    buildSchema
        "constructive_method"
        "Build the required object, algorithm, or witness directly."
        "When existence is shown by exhibiting a concrete construction, not by contradiction."
        [ reqStr "object_to_construct" "What must exist: module list, test, data structure, tool wiring."
          reqArr "construction_materials" 2 "Existing code, types, or specs used as building blocks."
          reqArr "construction_steps" 3 "Direct build steps in order."
          reqStr "witness" "How you will demonstrate the object works (test name, sample I/O)."
          optArr "minimality_argument" 1 "Why this construction is not gratuitously large."
          optStr "non_constructive_alternative" "Why you are not using existence-only argument here."
          optArr "dependencies" 1 "External packages or host APIs required." ]
        "Exhibit an explicit build plan and witness for the required artifact."
        [ "Construction plan"
          "Witness"
          "Minimality"
          "Next actions" ]
    buildSchema
        "reductio_ad_absurdum"
        "Assume the negation and derive a contradiction."
        "When proving an approach, invariant, or design choice cannot hold."
        [ reqStr "claim_to_refute" "The proposition you want to show false (e.g. single schema for all 54 tools without generation)."
          reqStr "assumed_negation" "Precise assumption for reductio."
          reqArr "derivation_toward_contradiction" 3 "Steps from negation toward conflict with known facts or invariants."
          reqStr "contradiction" "The explicit contradiction (violates test, type, user rule, physics of JS runtime)."
          reqArr "facts_used" 2 "Anchored facts that make the contradiction unavoidable."
          optStr "positive_alternative" "What becomes necessary once the negation is dead."
          optArr "limits_of_argument" 1 "Scope where reductio does not apply." ]
        "Assume the unwanted design and drive it into a workspace-anchored contradiction."
        [ "Negation setup"
          "Derivation"
          "Contradiction"
          "Positive alternative"
          "Next actions" ]
    buildSchema
        "invariance"
        "Find what cannot change under allowed operations, rewrites, or state transitions."
        "When refactoring, replaying history, or parallelizing work risks breaking silent conservation laws."
        [ reqStr "system_under_study" "Component or workflow (review, todo fold, KG write, tool execute)."
          reqArr "allowed_operations" 2 "Operations you permit this turn (rename, move, add tool, replay messages)."
          reqArr "candidate_invariants" 3 "Properties that should survive: event order, id uniqueness, permission matrix, YAML prefixes."
          reqArr "invariant_evidence" 2 "Tests, types, or docs that encode each invariant."
          reqStr "violation_symptom" "What you would see if an invariant broke."
          optArr "non_invariants" 1 "Things that may change freely without harming correctness."
          optStr "enforcement_mechanism" "How to guard invariants (types, architecture test, pure kernel)." ]
        "List conserved quantities under planned edits and tie each to evidence."
        [ "Operation set"
          "Invariant table"
          "Violation symptoms"
          "Enforcement"
          "Next actions" ]
    buildSchema
        "symmetry_analysis"
        "Exploit equivalence of cases; inspect symmetry breaking for bugs."
        "When Mux/Opencode, read/write, or dual code paths should behave mirror-wise."
        [ reqStr "symmetry_group" "What should be symmetric (two hosts, two hooks, two tool names)."
          reqArr "equivalent_cases" 2 "Paired scenarios that should match outcomes."
          reqArr "symmetry_breakers" 1 "Known intentional differences (task vs todowrite naming)."
          reqStr "observed_asymmetry" "Bug or drift: what differs without justification."
          reqArr "collapse_plan" 2 "How to unify handling (shared kernel, single codec)."
          optStr "canonical_side" "Which side is SSOT when breaking symmetry is required."
          optArr "regression_tests" 1 "Parity tests to add or run." ]
        "Separate legitimate symmetry breaking from accidental host drift."
        [ "Symmetry map"
          "Observed asymmetry"
          "Collapse plan"
          "Regression tests"
          "Next actions" ]
    buildSchema
        "dimensional_reduction"
        "Project to a lower-dimensional view, reason there, lift conclusions cautiously."
        "When full state space is too large: long sessions, 54 tools, entire monorepo."
        [ reqStr "full_state_description" "What makes the space huge (messages, files, mutable maps)."
          reqStr "projection" "The slice: one session, one module, one test failure, one tool call."
          reqArr "dropped_dimensions" 2 "What you ignore in the slice and why that is safe for this question."
          reqStr "reasoning_in_slice" "Conclusion valid inside the projection."
          reqArr "lift_risks" 2 "Ways the conclusion might fail when lifted to full state."
          optStr "minimal_reproduction" "Smallest command or test reproducing the issue."
          optArr "follow_up_projections" 1 "Other slices if the first is inconclusive." ]
        "Reason in a deliberate slice and document lift hazards."
        [ "Projection definition"
          "In-slice reasoning"
          "Lift risks"
          "Minimal reproduction"
          "Next actions" ]
    buildSchema
        "perturbation_continuity"
        "Vary one variable slightly from an easy case to see what survives and where behavior phases-changes."
        "When a hard bug sits near a working configuration (flag off, smaller input, older branch)."
        [ reqStr "easy_baseline" "Known-good or simpler case (test passes, tool count 14, no kg/)."
          reqStr "hard_case" "Failing or complex case."
          reqArr "perturbations" 3 "One-variable changes from baseline toward hard case (add field, enable dir, scale input)."
          reqArr "surviving_properties" 2 "What stays true across perturbations until failure."
          reqStr "phase_change_point" "Which perturbation first breaks behavior; hypothesize mechanism."
          optArr "bisection_plan" 2 "Ordered experiments to localize the breakpoint."
          optStr "rollback_strategy" "How to return to safe baseline while debugging." ]
        "Bisect from easy to hard via single-variable perturbations."
        [ "Baseline vs hard case"
          "Perturbation log"
          "Phase change"
          "Bisection plan"
          "Next actions" ]
    buildSchema
        "pigeonhole_principle"
        "Use counts and capacities to prove collision, overflow, or coverage must occur."
        "When exact placement is unknown but pigeonhole forces a conclusion (tools, slots, ports, ids)."
        [ reqStr "items" "What you are placing (sessions, jobs, tools, enum values, file handles)."
          reqStr "slots" "Distinct containers or capacities (ports, registry size, unique ids)."
          reqStr "counting_argument" "Arithmetic showing items > slots or forced repetition."
          reqStr "forced_conclusion" "What must happen: collision, queue, strip, failure."
          reqArr "evidence_counts" 2 "Numbers from code or tests backing items and slots."
          optArr "mitigations" 1 "Design changes that increase slots or reduce items."
          optStr "observable_signature" "Log or error pattern proving pigeonhole fired." ]
        "Make counting contradiction explicit for resource or namespace limits."
        [ "Items vs slots"
          "Counting proof"
          "Forced conclusion"
          "Mitigations"
          "Next actions" ]
    buildSchema
        "duality"
        "Solve the mirrored problem when the shadow formulation is easier."
        "When direct problem is hard: producer/consumer, read/write, command/event, primal/dual search."
        [ reqStr "primal_problem" "The hard formulation in current task terms."
          reqStr "dual_problem" "Mirrored view (events not state, consumer not producer, validation not generation)."
          reqArr "correspondence_map" 2 "Primal entity ↔ dual entity in this repo."
          reqStr "dual_solution_sketch" "Easier solve path in dual space."
          reqArr "pullback_steps" 2 "Map dual solution back to primal artifacts."
          optStr "duality_gap" "Where correspondence is imperfect."
          optArr "examples_in_repo" 1 "Existing dual patterns (event sourcing, caps synth)." ]
        "Work the shadow problem and map results back to implementation."
        [ "Primal–dual map"
          "Dual solution"
          "Pullback plan"
          "Next actions" ]
    buildSchema
        "quotient_space"
        "Quotient by equivalence: solve on classes, map back to concrete cases."
        "When many objects differ only in irrelevant detail (paths, formatting, host wrapper noise)."
        [ reqStr "raw_objects" "Concrete instances that feel distinct but may be equivalent."
          reqStr "equivalence_relation" "When two instances are considered the same (canonical tool name, normalized path)."
          reqArr "equivalence_classes" 2 "Representative per class and what varies within class."
          reqStr "problem_on_quotient" "Simplified problem stated per class."
          reqArr "lift_map" 2 "How to apply class-level solution to each representative."
          optArr "class_counterexamples" 1 "Pairs that look similar but must not be merged."
          optStr "canonicalization_function" "Existing or needed normalize function in kernel." ]
        "Collapse irrelevant variation via explicit equivalence before solving."
        [ "Equivalence definition"
          "Class representatives"
          "Quotient-level solution"
          "Lift map"
          "Next actions" ]
    buildSchema
        "category_mapping"
        "Preserve structure and morphisms while moving into a stronger domain (graphs, types, events)."
        "When relationships matter more than object internals."
        [ reqStr "source_domain" "Current messy domain (Dyn obj hooks, file soup)."
          reqStr "target_category" "Target language: state machine, graph, typed algebra, event log."
          reqArr "object_mapping" 2 "Objects → objects (tool → ToolSpec, message → DU)."
          reqArr "morphism_mapping" 2 "Operations → morphisms (execute, transform, replay)."
          reqStr "structural_property_to_preserve" "Composition, identity, ordering, functoriality in project terms."
          optArr "diagram_commutes_where" 1 "Commutative squares (host after kernel encode)."
          optStr "target_tooling" "Tests or types that enforce the mapping." ]
        "Functorial map from current mess to a structured domain without dropping laws."
        [ "Object map"
          "Morphism map"
          "Preserved structure"
          "Enforcement"
          "Next actions" ]
    buildSchema
        "relaxation"
        "Temporarily weaken constraints, solve superset, project back under real constraints."
        "When hard integer, ordering, permission, or exactness constraints block search."
        [ reqStr "hard_problem" "Fully constrained problem as stated."
          reqArr "constraints_relaxed" 2 "Which constraints you drop temporarily and why that enlarges feasible set."
          reqStr "relaxed_solution" "Solution valid in superset."
          reqArr "projection_steps" 2 "How to round, validate, or trim back to real constraints."
          reqArr "infeasible_after_projection" 1 "Parts of relaxed solution that cannot project—need alternate."
          optStr "relaxation_cost" "Risk of keeping relaxed cheat in production."
          optArr "validation_gates" 1 "Tests proving projected solution respects hard rules." ]
        "Solve easier superset then honest projection with validation gates."
        [ "Relaxation map"
          "Relaxed solution"
          "Projection"
          "Validation"
          "Next actions" ]
    buildSchema
        "search_space_exploration"
        "Model candidates as a space or graph; choose traversal strategy."
        "When many design or fix options exist and ad-hoc picking is unsafe."
        [ reqStr "search_goal" "What you are optimizing or finding (minimal diff, passing test, schema design)."
          reqArr "state_nodes" 3 "Representative states in the space (configs, branches, tool sets)."
          reqArr "moves" 2 "Edges: edit file, add test, register tool, revert."
          reqStr "traversal_strategy" "BFS, DFS, beam, greedy—chosen deliberately with stop rule."
          reqArr "pruned_branches" 1 "Branches ruled out with reason."
          optStr "heuristic" "Ranking function for candidates."
          optArr "frontier_snapshot" 1 "Current best candidates and scores." ]
        "Make the design space explicit and document traversal and pruning."
        [ "State graph sketch"
          "Traversal strategy"
          "Pruning log"
          "Frontier"
          "Next actions" ]
    buildSchema
        "branch_and_bound"
        "Prune dominated or impossible branches using bounds."
        "When exhaustive search over refactor or config options needs disciplined pruning."
        [ reqStr "optimization_target" "What you minimize or maximize (lines changed, tools exposed, test time)."
          reqArr "branches" 2 "Major alternatives under consideration."
          reqArr "lower_bounds" 2 "Best-case cost or benefit per branch."
          reqArr "upper_bounds" 2 "Worst-case or proven infeasibility bounds."
          reqArr "pruned_branches" 1 "Branches cut because bound shows dominance or impossibility."
          reqStr "active_branch" "Branch still worth exploring."
          optArr "bound_evidence" 1 "Tests, metrics, or architecture rules supplying bounds."
          optStr "stop_condition" "When further branching is wasteful." ]
        "Rank branches with bounds and document pruning decisions."
        [ "Branch table"
          "Bounds"
          "Pruning rationale"
          "Active branch"
          "Next actions" ]
]
