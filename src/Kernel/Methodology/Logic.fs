module Wanxiangshu.Kernel.Methodology.Logic

open Wanxiangshu.Kernel.Methodology.Schema

let private batch1: MethodologyEntry list =
    [ { methodologyId = "first_principles"
        shortDefinition = "Reduce the problem to irreducible facts and rebuild from them."
        triggerWhen = "When inherited assumptions, frameworks, or copy-paste patterns obscure what must be true."
        noteDescription =
          "problem_statement, assumptions_to_strip, atomic_facts, rebuild_steps, irreducible_core, discarded_shortcuts, open_questions, workspace_anchors"
        meditatorRole =
          "Strip inherited narratives until only workspace-anchored facts remain, then rebuild a minimal architecture story."
        outputSections =
          [ "Stripped assumption ledger"
            "Atomic fact table"
            "Rebuild chain"
            "Irreducible core statement"
            "Discarded shortcuts and why"
            "Next executable actions for the parent agent" ] }
      { methodologyId = "axiomatization"
        shortDefinition =
          "State primitive terms, allowed operations, invariants, forbidden states, and derivation rules explicitly; then solve only inside that declared system."
        triggerWhen =
          "When definitions drift, hidden assumptions make reasoning unstable, or multiple teams talk past each other."
        noteDescription =
          "system_name, primitive_terms, allowed_operations, invariants, forbidden_states, derivation_rules, scope_boundary, consistency_checks, known_ambiguities"
        meditatorRole = "Freeze vocabulary and legal moves so downstream edits cannot smuggle undefined behavior."
        outputSections =
          [ "Primitive term glossary"
            "Operation table with preconditions"
            "Invariant list"
            "Forbidden states"
            "Derivation rules applied to current task"
            "Consistency checks"
            "Next actions" ] }
      { methodologyId = "deduction"
        shortDefinition = "Derive necessary conclusions from accepted premises."
        triggerWhen =
          "When premises are already agreed (tests, types, docs, user rules) and you need forced implications."
        noteDescription =
          "accepted_premises, target_claim, inference_steps, final_conclusion, premises_not_used, counterarguments, formalization_sketch, testable_corollaries"
        meditatorRole = "Chain truth-preserving steps from agreed premises to a conclusion the parent can act on."
        outputSections =
          [ "Premise ledger"
            "Inference chain"
            "Final conclusion"
            "Unused premises"
            "Corollaries and tests"
            "Next actions" ] } ]

let private batch2: MethodologyEntry list =
    [ { methodologyId = "induction"
        shortDefinition = "Infer a general rule from repeated cases or patterns."
        triggerWhen = "When you have multiple concrete instances and need a guarded generalization for the codebase."
        noteDescription =
          "observed_cases, shared_pattern, proposed_rule, supporting_evidence, exceptions_seen, confidence_bounds, predictions, anti_pattern"
        meditatorRole = "Generalize from repeated workspace evidence without overclaiming beyond the sample."
        outputSections =
          [ "Case table"
            "Pattern statement"
            "Proposed rule"
            "Exception handling"
            "Predictions to verify"
            "Next actions" ] }
      { methodologyId = "abduction"
        shortDefinition = "Generate the best causal hypothesis for surprising evidence, then seek discriminating tests."
        triggerWhen = "When debugging, diagnosing, investigating, or explaining outcomes that violate expectations."
        noteDescription =
          "surprising_evidence, context_anchor, hypothesis, discriminating_tests, alternative_hypotheses, expected_observations_if_true, ruled_out_paths, stop_rule"
        meditatorRole =
          "Propose the best explanation for surprise and spell discriminating checks—not treat guess as fact."
        outputSections =
          [ "Evidence summary"
            "Primary hypothesis"
            "Alternatives"
            "Discriminating test plan"
            "Expected observations"
            "Next actions" ] } ]

let private batch3: MethodologyEntry list =
    [ { methodologyId = "reductio_ad_absurdum"
        shortDefinition = "Assume the negation and derive a contradiction."
        triggerWhen = "When proving an approach, invariant, or design choice cannot hold."
        noteDescription =
          "claim_to_refute, assumed_negation, derivation_toward_contradiction, contradiction, facts_used, positive_alternative, limits_of_argument"
        meditatorRole = "Assume the unwanted design and drive it into a workspace-anchored contradiction."
        outputSections =
          [ "Negation setup"
            "Derivation"
            "Contradiction"
            "Positive alternative"
            "Next actions" ] }
      { methodologyId = "falsification"
        shortDefinition = "Formulate hypotheses with clear failure conditions; search counterexamples."
        triggerWhen = "When a design claim risks becoming unfalsifiable narrative."
        noteDescription = "claim, failure_conditions, search_attempts, verdict, surviving_scope, popper_note, new_tests"
        meditatorRole = "Try to kill the claim before shipping it."
        outputSections =
          [ "Claim"
            "Failure conditions"
            "Search log"
            "Verdict and revised scope"
            "Next actions" ] } ]

let entries: MethodologyEntry list = batch1 @ batch2 @ batch3
