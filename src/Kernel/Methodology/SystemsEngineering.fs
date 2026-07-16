module Wanxiangshu.Kernel.Methodology.SystemsEngineering

open Wanxiangshu.Kernel.Methodology.Schema

let private batch1: MethodologyEntry list =
    [ { methodologyId = "systems_thinking"
        shortDefinition = "Model feedback loops, dependencies, delays, and emergent behavior."
        triggerWhen = "When a change in hooks, tools, or prompts ripples through sessions and review loops."
        noteDescription =
          "system_boundary, stocks, flows, feedback_loops, delays, emergent_risk, leverage_points, simulation_or_trace"
        meditatorRole = "Draw loops and delays so host-side tweaks are not judged in isolation."
        outputSections =
          [ "Stock-flow sketch"
            "Feedback loops"
            "Delays"
            "Leverage points"
            "Next actions" ] }
      { methodologyId = "root_cause_analysis"
        shortDefinition = "Trace symptoms to causal fault, not visible failure only."
        triggerWhen = "When repeated failures, flaky tests, or incident-like tool errors need depth."
        noteDescription =
          "symptom, visible_failure, why_chain, root_cause, contributing_factors, fix_target, verification_after_fix, symptom_vs_cause_guard"
        meditatorRole = "Drive why-chain to an actionable root with verification, not patch-the-symptom."
        outputSections =
          [ "Symptom vs visible failure"
            "Why chain"
            "Root cause"
            "Fix target"
            "Verification"
            "Next actions" ] }
      { methodologyId = "state_machine_reasoning"
        shortDefinition = "Enumerate legal states, transitions, and impossible states."
        triggerWhen = "When behavior is modal: review, nudge, todo in_progress discipline."
        noteDescription =
          "machine_name, states, transitions, illegal_states, current_state_guess, missing_transitions, exhaustiveness_check"
        meditatorRole = "Make FSM explicit and align implementation with illegal state elimination."
        outputSections =
          [ "State list"
            "Transition table"
            "Illegal states"
            "Gap analysis"
            "Next actions" ] } ]

let private batch2: MethodologyEntry list =
    [ { methodologyId = "type_driven_design"
        shortDefinition = "Encode domain boundaries and illegal states in types."
        triggerWhen = "Before implementing hooks or tools still passing Dyn obj through business logic."
        noteDescription =
          "domain_slice, illegal_states_today, algebraic_model, encoding_plan, operations_as_functions, compiler_guarantees, migration_from_dyn"
        meditatorRole = "Design types so illegal states are unwritable before coding handlers."
        outputSections =
          [ "Illegal state inventory"
            "Algebraic model"
            "Codec boundary"
            "Migration steps"
            "Next actions" ] }
      { methodologyId = "event_sourcing"
        shortDefinition = "Separate commands from facts; derive current state from event history."
        triggerWhen = "When mutable maps disagree with message history or replay is required."
        noteDescription =
          "command_side, event_side, events_list, fold_function, replay_requirements, snapshot_policy, correction_events, anti_patterns"
        meditatorRole = "Align command/event split with history-as-truth discipline in vibe-fs."
        outputSections =
          [ "Command vs event"
            "Event catalog"
            "Fold/replay"
            "Correction strategy"
            "Next actions" ] }
      { methodologyId = "operationalism"
        shortDefinition =
          "Define concepts by observable operations that detect or change them; discard non-behavioral distinctions."
        triggerWhen = "When vague terms (done, stable, registered) need testable meaning."
        noteDescription =
          "vague_term, observation_operations, mutation_operations, equivalence_criterion, discarded_distinctions, operational_spec, counterexamples"
        meditatorRole = "Replace metaphysical labels with observation/mutation specs."
        outputSections =
          [ "Term under scrutiny"
            "Operational definition"
            "Discarded distinctions"
            "Implementable spec"
            "Next actions" ] } ]

let private batch3: MethodologyEntry list =
    [ { methodologyId = "bayesian_update"
        shortDefinition = "Update belief strength as evidence arrives; avoid all-or-nothing after one test."
        triggerWhen = "When multiple hypotheses compete (host bug vs kernel bug vs stale build)."
        noteDescription =
          "hypothesis_set, prior_weights, new_evidence, likelihood_sketch, posterior_summary, decisive_experiment, discarded_hypotheses"
        meditatorRole = "Qualitative Bayesian update over competing engineering hypotheses."
        outputSections =
          [ "Priors"
            "Evidence"
            "Likelihood notes"
            "Posterior"
            "Decisive experiment"
            "Next actions" ] }
      { methodologyId = "test_driven_reasoning"
        shortDefinition = "Make expected behavior executable before or during implementation."
        triggerWhen =
          "When behavior can be pinned by tests (schema registry, Args.parse required fields, architecture gates)."
        noteDescription =
          "behavior_claim, executable_oracles, red_phase_plan, green_phase_plan, refactor_safeties, non_testable_residual, tdd_sequence"
        meditatorRole = "Bind reasoning to executable behavior oracles in tests/*Tests.fs."
        outputSections =
          [ "Behavior claim"
            "Oracles"
            "Red-green plan"
            "TDD sequence"
            "Next actions" ] }
      { methodologyId = "debugging_trace"
        shortDefinition = "Reproduce, isolate, instrument, verify the fault chain."
        triggerWhen = "When a failure needs systematic narrowing (Fable build, hook, integration test)."
        noteDescription =
          "failure_signature, reproduction_steps, isolation_experiments, instrumentation_points, fault_chain, verified_fix_hypothesis, ruled_out_causes, regression_guard"
        meditatorRole = "Document reproduce→isolate→instrument→verify without guessing."
        outputSections =
          [ "Reproduction"
            "Isolation log"
            "Fault chain"
            "Fix hypothesis"
            "Next actions" ] } ]

let entries: MethodologyEntry list = batch1 @ batch2 @ batch3
