module Wanxiangshu.Kernel.Methodology.MathematicalReasoning

open Wanxiangshu.Kernel.Methodology.Schema

let private batch1: MethodologyEntry list =
    [ { methodologyId = "invariance"
        shortDefinition = "Find what cannot change under allowed operations, rewrites, or state transitions."
        triggerWhen =
          "When refactoring, replaying history, or parallelizing work risks breaking silent conservation laws."
        noteDescription =
          "system_under_study, allowed_operations, candidate_invariants, invariant_evidence, violation_symptom, non_invariants, enforcement_mechanism"
        meditatorRole = "List conserved quantities under planned edits and tie each to evidence."
        outputSections =
          [ "Operation set"
            "Invariant table"
            "Violation symptoms"
            "Enforcement"
            "Next actions" ] }
      { methodologyId = "symmetry_analysis"
        shortDefinition = "Exploit equivalence of cases; inspect symmetry breaking for bugs."
        triggerWhen = "When Mux/Opencode, read/write, or dual code paths should behave mirror-wise."
        noteDescription =
          "symmetry_group, equivalent_cases, symmetry_breakers, observed_asymmetry, collapse_plan, canonical_side, regression_tests"
        meditatorRole = "Separate legitimate symmetry breaking from accidental host drift."
        outputSections =
          [ "Symmetry map"
            "Observed asymmetry"
            "Collapse plan"
            "Regression tests"
            "Next actions" ] }
      { methodologyId = "dimensional_reduction"
        shortDefinition = "Project to a lower-dimensional view, reason there, lift conclusions cautiously."
        triggerWhen = "When full state space is too large: long sessions, 54 tools, entire monorepo."
        noteDescription =
          "full_state_description, projection, dropped_dimensions, reasoning_in_slice, lift_risks, minimal_reproduction, follow_up_projections"
        meditatorRole = "Reason in a deliberate slice and document lift hazards."
        outputSections =
          [ "Projection definition"
            "In-slice reasoning"
            "Lift risks"
            "Minimal reproduction"
            "Next actions" ] } ]

let private batch2: MethodologyEntry list =
    [ { methodologyId = "perturbation_continuity"
        shortDefinition =
          "Vary one variable slightly from an easy case to see what survives and where behavior phases-changes."
        triggerWhen = "When a hard bug sits near a working configuration (flag off, smaller input, older branch)."
        noteDescription =
          "easy_baseline, hard_case, perturbations, surviving_properties, phase_change_point, bisection_plan, rollback_strategy"
        meditatorRole = "Bisect from easy to hard via single-variable perturbations."
        outputSections =
          [ "Baseline vs hard case"
            "Perturbation log"
            "Phase change"
            "Bisection plan"
            "Next actions" ] }
      { methodologyId = "pigeonhole_principle"
        shortDefinition = "Use counts and capacities to prove collision, overflow, or coverage must occur."
        triggerWhen = "When exact placement is unknown but pigeonhole forces a conclusion (tools, slots, ports, ids)."
        noteDescription =
          "items, slots, counting_argument, forced_conclusion, evidence_counts, mitigations, observable_signature"
        meditatorRole = "Make counting contradiction explicit for resource or namespace limits."
        outputSections =
          [ "Items vs slots"
            "Counting proof"
            "Forced conclusion"
            "Mitigations"
            "Next actions" ] }
      { methodologyId = "duality"
        shortDefinition = "Solve the mirrored problem when the shadow formulation is easier."
        triggerWhen = "When direct problem is hard: producer/consumer, read/write, command/event, primal/dual search."
        noteDescription =
          "primal_problem, dual_problem, correspondence_map, dual_solution_sketch, pullback_steps, duality_gap, examples_in_repo"
        meditatorRole = "Work the shadow problem and map results back to implementation."
        outputSections = [ "Primal–dual map"; "Dual solution"; "Pullback plan"; "Next actions" ] } ]

let private batch3: MethodologyEntry list =
    [ { methodologyId = "quotient_space"
        shortDefinition = "Quotient by equivalence: solve on classes, map back to concrete cases."
        triggerWhen = "When many objects differ only in irrelevant detail (paths, formatting, host wrapper noise)."
        noteDescription =
          "raw_objects, equivalence_relation, equivalence_classes, problem_on_quotient, lift_map, class_counterexamples, canonicalization_function"
        meditatorRole = "Collapse irrelevant variation via explicit equivalence before solving."
        outputSections =
          [ "Equivalence definition"
            "Class representatives"
            "Quotient-level solution"
            "Lift map"
            "Next actions" ] }
      { methodologyId = "category_mapping"
        shortDefinition =
          "Preserve structure and morphisms while moving into a stronger domain (graphs, types, events)."
        triggerWhen = "When relationships matter more than object internals."
        noteDescription =
          "source_domain, target_category, object_mapping, morphism_mapping, structural_property_to_preserve, diagram_commutes_where, target_tooling"
        meditatorRole = "Functorial map from current mess to a structured domain without dropping laws."
        outputSections =
          [ "Object map"
            "Morphism map"
            "Preserved structure"
            "Enforcement"
            "Next actions" ] }
      { methodologyId = "renormalization"
        shortDefinition = "Coarse-grain micro-detail; keep scale-relevant variables; find stable macro structure."
        triggerWhen = "When micro implementation noise obscures macro behavior (54 files, hook spaghetti)."
        noteDescription =
          "micro_level, macro_question, coarse_graining_map, relevant_variables, universal_pattern, micro_corrections, documentation_level"
        meditatorRole = "Summarize micro complexity into macro laws for decision-making."
        outputSections =
          [ "Coarse-graining"
            "Macro variables"
            "Stable pattern"
            "When to re-zoom"
            "Next actions" ] } ]

let entries: MethodologyEntry list = batch1 @ batch2 @ batch3
