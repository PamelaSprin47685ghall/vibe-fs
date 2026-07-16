module Wanxiangshu.Kernel.Methodology.CriticalInquiry

open Wanxiangshu.Kernel.Methodology.Schema

let private batch1: MethodologyEntry list =
    [ { methodologyId = "conceptual_analysis"
        shortDefinition = "Clarify meanings, category boundaries, scope; remove category mistakes."
        triggerWhen = "When terms collide (tool vs wrapper vs agent vs session vs task)."
        noteDescription =
          "confused_concept, senses_disambiguated, category_boundaries, scope_fix, category_mistakes_found, recommended_vocabulary, glossary_entries"
        meditatorRole = "Disambiguate vocabulary before structural changes."
        outputSections =
          [ "Disambiguation table"
            "Category boundaries"
            "Scope fix"
            "Vocabulary"
            "Next actions" ] }
      { methodologyId = "dialectical_analysis"
        shortDefinition = "Thesis, antithesis, tension, dependency, resolution—not one-sided causality."
        triggerWhen = "When opposing forces shape design (DRY vs 54 files, kernel purity vs host Dyn)."
        noteDescription =
          "thesis, antithesis, tensions, dependencies, synthesis_path, frozen_decision, tradeoffs_accepted"
        meditatorRole = "Mediate real opposing design forces instead of picking a slogan."
        outputSections =
          [ "Thesis vs antithesis"
            "Tensions"
            "Synthesis"
            "Accepted tradeoffs"
            "Next actions" ] }
      { methodologyId = "hermeneutic_circle"
        shortDefinition = "Iterate part and whole until local and global meaning stabilize."
        triggerWhen = "Understanding large codepaths, README+implementation together, PRD+tests."
        noteDescription =
          "whole_artifact, part_focus, part_to_whole_updates, whole_to_part_updates, stabilized_reading, remaining_tension, reading_order"
        meditatorRole = "Alternate local detail and global architecture until coherent."
        outputSections =
          [ "Iteration log"
            "Stabilized reading"
            "Remaining tension"
            "Reading order"
            "Next actions" ] }
      { methodologyId = "deconstruction"
        shortDefinition = "Inspect hidden binaries, excluded voices, unstable centers in framing."
        triggerWhen = "When PRD, AGENTS, or design docs assume hierarchy that hides alternatives."
        noteDescription =
          "text_or_design, binary_oppositions, excluded_middle, unstable_center, internal_contradictions, reframe, actionable_extractions"
        meditatorRole = "Expose rhetorical structure so decisions are not captive to false binaries."
        outputSections =
          [ "Binaries and exclusions"
            "Contradictions"
            "Reframe"
            "Actionable requirements"
            "Next actions" ] } ]

let private batch2: MethodologyEntry list =
    [ { methodologyId = "simplification"
        shortDefinition = "Remove accidental complexity until only essential problem remains."
        triggerWhen = "When solution path is cluttered with frameworks, flags, duplicate adapters."
        noteDescription =
          "overcomplicated_surface, accidental_parts, essential_core, simplification_moves, invariants_preserved, simplification_metric, deferred_complexity"
        meditatorRole = "Peel accidental layers without violating essential invariants."
        outputSections =
          [ "Accidental inventory"
            "Essential core"
            "Simplification moves"
            "Preserved invariants"
            "Next actions" ] }
      { methodologyId = "tradeoff_analysis"
        shortDefinition = "Compare options across explicit constraints and costs."
        triggerWhen = "When choosing between registration strategies, schema layout, host parity approaches."
        noteDescription =
          "decision, options, constraints, cost_dimensions, comparison_matrix, recommendation, reversible_parts, decision_deadline"
        meditatorRole = "Compare options honestly on named constraints—not vibes."
        outputSections =
          [ "Options"
            "Constraint table"
            "Recommendation"
            "Reversibility"
            "Next actions" ] }
      { methodologyId = "risk_analysis"
        shortDefinition = "Identify failure modes, blast radius, irreversible decisions."
        triggerWhen = "Before large registration change or permission matrix edits."
        noteDescription =
          "proposed_change, failure_modes, blast_radius, irreversible_steps, risk_ranking, mitigations, residual_risk, monitoring"
        meditatorRole = "Map failure modes and mitigations before irreversible edits."
        outputSections =
          [ "Failure modes"
            "Blast radius"
            "Mitigations"
            "Residual risk"
            "Next actions" ] }
      { methodologyId = "security_review"
        shortDefinition = "Reason adversarially about trust boundaries and abuse paths."
        triggerWhen = "When tools execute code, read files, spawn subagents, or accept huge backgrounds."
        noteDescription =
          "trust_boundary, assets, threat_actors, abuse_paths, existing_controls, gap_summary, hardening_actions, out_of_scope"
        meditatorRole = "Adversarial pass on tool and subagent boundaries."
        outputSections = [ "Boundary map"; "Abuse paths"; "Control gaps"; "Hardening"; "Next actions" ] } ]

let private batch3: MethodologyEntry list =
    [ { methodologyId = "performance_analysis"
        shortDefinition = "Locate bottlenecks, asymptotics, and resource constraints."
        triggerWhen =
          "When many methodology notebook tools, large backgrounds, Fable compile, or session history size matters."
        noteDescription =
          "performance_question, workload_model, hot_paths, complexity_notes, measurement_plan, optimization_candidates, budget, anti_optimizations"
        meditatorRole = "Tie performance claims to workload and measurement."
        outputSections = [ "Workload"; "Hot paths"; "Measurement"; "Candidates"; "Next actions" ] }
      { methodologyId = "user_intent_clarification"
        shortDefinition = "Resolve ambiguous goals before optimizing the wrong target."
        triggerWhen = "When user request could mean schema-only, full wiring, or design discussion."
        noteDescription =
          "user_request_quote, interpretations, disambiguating_questions, assumed_intent, success_criteria_per_interpretation, misinterpretation_cost, clarified_out_of_scope"
        meditatorRole = "Make interpretations explicit before large automated work."
        outputSections =
          [ "Interpretations"
            "Questions"
            "Working assumption"
            "Success criteria"
            "Next actions" ] }
      { methodologyId = "thought_experiment"
        shortDefinition = "Push idealized or extreme scenarios through rules to test concept boundaries."
        triggerWhen = "When real execution is costly or dangerous (data loss, prod hook, oversized payloads)."
        noteDescription =
          "scenario_setup, rule_under_test, scenario_steps, derived_outcome, boundary_insights, mapping_to_real, real_tests_inspired"
        meditatorRole = "Stress rules in imagination before paying execution cost."
        outputSections = [ "Scenario"; "Steps"; "Outcome"; "Insights"; "Inspired tests"; "Next actions" ] }
      { methodologyId = "transcendental_argument"
        shortDefinition = "Ask what must already exist for an undeniable fact or behavior to be possible."
        triggerWhen = "When a capability clearly works and you need preconditions (replay, caps, review)."
        noteDescription =
          "undeniable_fact, necessary_preconditions, dependency_chain, missing_precondition_tests, philosophical_limit, engineering_implications"
        meditatorRole = "Reverse-engineer prerequisites from stable facts."
        outputSections =
          [ "Undeniable fact"
            "Precondition chain"
            "Break tests"
            "Implications"
            "Next actions" ] } ]

let entries: MethodologyEntry list = batch1 @ batch2 @ batch3
