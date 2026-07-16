module Wanxiangshu.Kernel.Methodology.ProblemTransformation

open Wanxiangshu.Kernel.Methodology.Schema

let private batch1: MethodologyEntry list =
    [ { methodologyId = "analogy"
        shortDefinition = "Transfer a known solution structure from a genuinely similar problem."
        triggerWhen =
          "When a canonical template elsewhere in the repo or domain likely shares topology with the current task."
        noteDescription =
          "source_domain, target_domain, shared_structure, transferred_tactics, mismatch_risks, similarity_argument, anti_analogies, adaptation_checklist"
        meditatorRole = "Map structure from a proven neighbor problem and stress-test mismatches before copying code."
        outputSections =
          [ "Source–target mapping"
            "Transferred tactics"
            "Mismatch risks"
            "Adaptation checklist"
            "Next actions" ] }
      { methodologyId = "specialization"
        shortDefinition = "Inspect simple, concrete, boundary, or extreme cases before generalizing."
        triggerWhen = "Before designing a general API, algorithm, or refactor covering many inputs."
        noteDescription =
          "general_problem, concrete_instances, boundary_cases, extreme_cases, lessons_per_instance, generalization_blockers, minimal_general_form"
        meditatorRole = "Ground design in named concrete and boundary instances before writing generic code."
        outputSections =
          [ "Instance catalog"
            "Boundary and extreme notes"
            "Per-instance lessons"
            "Minimal general form"
            "Next actions" ] }
      { methodologyId = "generalization"
        shortDefinition = "Widen the problem to expose the underlying structure."
        triggerWhen = "When a local fix hides a missing abstraction or repeated pattern across modules."
        noteDescription =
          "local_symptom, widened_view, structural_invariants, variation_dimensions, proposed_abstraction, instances_covered, instances_excluded, refactor_slice"
        meditatorRole = "Lift from local patch to structural abstraction with explicit coverage and exclusions."
        outputSections =
          [ "Widened problem statement"
            "Abstraction proposal"
            "Coverage map"
            "Excluded cases"
            "Next actions" ] }
      { methodologyId = "working_backwards"
        shortDefinition = "Start from the desired end state and derive prerequisites."
        triggerWhen = "When the goal is clear but the path is muddy; integration tests or UX outcomes are known."
        noteDescription =
          "desired_end_state, acceptance_signals, prerequisite_chain, current_position, blocking_gaps, parallel_tracks, first_forward_step"
        meditatorRole = "Reverse-plan from acceptance signals to today's executable first step."
        outputSections =
          [ "End state definition"
            "Prerequisite chain"
            "Gap analysis"
            "First forward step"
            "Next actions" ] } ]

let private batch2: MethodologyEntry list =
    [ { methodologyId = "analysis_synthesis"
        shortDefinition =
          "Analyze backward from the desired result to known facts, then synthesize forward into a plan."
        triggerWhen = "When the goal is clear but construction path is not; large features or refactors."
        noteDescription =
          "target_result, backward_analysis, known_facts, synthesis_steps, integration_point, risks_in_synthesis, validation_milestone"
        meditatorRole = "Split backward feasibility analysis from forward construction schedule."
        outputSections =
          [ "Backward condition table"
            "Known facts"
            "Forward synthesis plan"
            "Integration point"
            "Next actions" ] }
      { methodologyId = "auxiliary_construction"
        shortDefinition = "Introduce a helper representation that exposes a hidden relation between known and unknown."
        triggerWhen = "When direct attack fails and a lemma, adapter, IR, or invariant would bridge facts to target."
        noteDescription =
          "known_side, unknown_target, auxiliary_object, exposed_relation, construction_steps, discharge_steps, placement, failure_modes"
        meditatorRole = "Design a minimal bridge object and plan to discharge it after the target is reachable."
        outputSections =
          [ "Known vs unknown"
            "Auxiliary design"
            "Construction and discharge"
            "Placement recommendation"
            "Next actions" ] }
      { methodologyId = "equivalent_transformation"
        shortDefinition = "Convert the problem into an equivalent form where reasoning or implementation is easier."
        triggerWhen = "When the current representation is noisy: control flow, JSON blobs, implicit state."
        noteDescription =
          "source_representation, target_representation, equivalence_claim, transformation_steps, preserved_properties, lost_detail, verification"
        meditatorRole = "Justify a representation change and list preserved observables before rewriting."
        outputSections =
          [ "Source vs target representation"
            "Equivalence argument"
            "Transformation steps"
            "Verification plan"
            "Next actions" ] } ]

let private batch3: MethodologyEntry list =
    [ { methodologyId = "decomposition_recombination"
        shortDefinition = "Split the object into parts and reconnect them in a better structure."
        triggerWhen = "When a module, tool surface, or workflow is too entangled to edit safely."
        noteDescription =
          "whole_artifact, parts, interfaces_between_parts, recombined_shape, migration_slices, coupling_to_cut, architecture_test_hooks"
        meditatorRole = "Propose a part graph and migration slices that respect vibe-fs layering."
        outputSections =
          [ "Decomposition map"
            "Interface contracts"
            "Recombined architecture"
            "Migration slices"
            "Next actions" ] }
      { methodologyId = "model_problem_transfer"
        shortDefinition = "Transfer a solution skeleton from a canonical template when topology matches."
        triggerWhen = "When the task resembles a well-known pattern: plugin adapter, state machine, codec boundary."
        noteDescription =
          "canonical_template, current_problem, shared_unknowns, shared_constraints, transfer_steps, assumption_failures, reference_implementation, checklist"
        meditatorRole = "Map current work onto a canonical repo pattern and flag broken assumptions."
        outputSections =
          [ "Template mapping"
            "Transfer steps"
            "Failed assumptions"
            "Checklist"
            "Next actions" ] }
      { methodologyId = "constructive_method"
        shortDefinition = "Build the required object, algorithm, or witness directly."
        triggerWhen = "When existence is shown by exhibiting a concrete construction, not by contradiction."
        noteDescription =
          "object_to_construct, construction_materials, construction_steps, witness, minimality_argument, non_constructive_alternative, dependencies"
        meditatorRole = "Exhibit an explicit build plan and witness for the required artifact."
        outputSections = [ "Construction plan"; "Witness"; "Minimality"; "Next actions" ] } ]

let entries: MethodologyEntry list = batch1 @ batch2 @ batch3
