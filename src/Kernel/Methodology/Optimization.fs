module Wanxiangshu.Kernel.Methodology.Optimization

open Wanxiangshu.Kernel.Methodology.Schema

let private batch1: MethodologyEntry list =
    [ { methodologyId = "relaxation"
        shortDefinition = "Temporarily weaken constraints, solve superset, project back under real constraints."
        triggerWhen = "When hard integer, ordering, permission, or exactness constraints block search."
        noteDescription =
          "hard_problem, constraints_relaxed, relaxed_solution, projection_steps, infeasible_after_projection, relaxation_cost, validation_gates"
        meditatorRole = "Solve easier superset then honest projection with validation gates."
        outputSections =
          [ "Relaxation map"
            "Relaxed solution"
            "Projection"
            "Validation"
            "Next actions" ] }
      { methodologyId = "search_space_exploration"
        shortDefinition = "Model candidates as a space or graph; choose traversal strategy."
        triggerWhen = "When many design or fix options exist and ad-hoc picking is unsafe."
        noteDescription =
          "search_goal, state_nodes, moves, traversal_strategy, pruned_branches, heuristic, frontier_snapshot"
        meditatorRole = "Make the design space explicit and document traversal and pruning."
        outputSections =
          [ "State graph sketch"
            "Traversal strategy"
            "Pruning log"
            "Frontier"
            "Next actions" ] }
      { methodologyId = "branch_and_bound"
        shortDefinition = "Prune dominated or impossible branches using bounds."
        triggerWhen = "When exhaustive search over refactor or config options needs disciplined pruning."
        noteDescription =
          "optimization_target, branches, lower_bounds, upper_bounds, pruned_branches, active_branch, bound_evidence, stop_condition"
        meditatorRole = "Rank branches with bounds and document pruning decisions."
        outputSections =
          [ "Branch table"
            "Bounds"
            "Pruning rationale"
            "Active branch"
            "Next actions" ] } ]

let private batch2: MethodologyEntry list =
    [ { methodologyId = "dynamic_programming"
        shortDefinition = "Exploit overlapping subproblems and optimal substructure with memoized state transitions."
        triggerWhen = "When repeated subtasks appear (schema gen per tool, replay segments, fuzzy pages)."
        noteDescription =
          "top_level_goal, subproblems, overlap_evidence, state_definition, transitions, memoization_plan, base_cases, complexity_note"
        meditatorRole = "Factor overlapping work into memoized states appropriate for vibe-fs stores."
        outputSections =
          [ "Subproblem decomposition"
            "State and transitions"
            "Memoization"
            "Next actions" ] }
      { methodologyId = "monte_carlo_sampling"
        shortDefinition = "Sample plausible paths when space is too large; verify critical findings deterministically."
        triggerWhen = "When exhaustive reasoning over sessions, tool combos, or message orderings is infeasible."
        noteDescription =
          "decision_question, sample_space, samples_drawn, stability_signal, deterministic_followups, sample_size_rationale, outliers"
        meditatorRole = "Sample cheaply then nail truth with deterministic verification."
        outputSections =
          [ "Sampling plan"
            "Stable signal"
            "Outliers"
            "Deterministic followups"
            "Next actions" ] }
      { methodologyId = "simulated_annealing"
        shortDefinition = "Accept worse interim states early to escape local optima; cool into refinement."
        triggerWhen = "When greedy refactor or fix order gets stuck in a local minimum."
        noteDescription =
          "objective_function, current_state, neighbor_moves, acceptance_policy, cooling_schedule, best_so_far, termination"
        meditatorRole = "Plan exploration vs exploitation when incremental edits plateau."
        outputSections =
          [ "Objective"
            "Neighbor moves"
            "Annealing schedule"
            "Commit criteria"
            "Next actions" ] }
      { methodologyId = "swarm_optimization"
        shortDefinition =
          "Parallel candidate directions explore, share best findings, converge without early overcommit."
        triggerWhen = "When multiple subagents, hypotheses, or design drafts can search in parallel."
        noteDescription =
          "collective_goal, agents_or_hypotheses, share_mechanism, diversity_rules, convergence_criteria, best_candidate, retired_candidates"
        meditatorRole = "Orchestrate parallel exploration with explicit merge and convergence rules."
        outputSections =
          [ "Swarm layout"
            "Sharing protocol"
            "Convergence"
            "Leader candidate"
            "Next actions" ] } ]

let entries: MethodologyEntry list = batch1 @ batch2
