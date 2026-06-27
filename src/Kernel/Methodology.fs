module Wanxiangshu.Kernel.Methodology

open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.MethodologyCatalog

let selectMethodologyToolName = "select_methodology"

let methodologyToolResultText (methodologies: string list) =
    match methodologies with
    | [] -> invalidArg "methodologies" "methodologyToolResultText requires at least one methodology"
    | _ ->
        let joined = String.concat ", " methodologies
        $"Great! Now please explain how to apply [{joined}] to the work step."

let todoResultText (methodologies: string list) : string =
    match methodologies with
    | [] -> "Todos updated."
    | _ ->
        let joined = String.concat ", " methodologies
        $"Great! Now please explain how to apply [{joined}] to the work step."

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

let methodologyCatalog = Wanxiangshu.Kernel.MethodologyCatalog.methodologyCatalog

let methodologyToolNames : string array =
    methodologyEnumValues |> List.map (fun id -> "methodology_" + id) |> Array.ofList

let selectMethodologyFieldDescription =
    "Required when calling this tool: record `select_methodology` with one or more methodology names that must guide the next work step. Choose by definitions, not by keyword vibes.\n\n"
    + methodologyCatalog
