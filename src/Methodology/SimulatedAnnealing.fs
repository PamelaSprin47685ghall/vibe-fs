module Wanxiangshu.Methodology.SimulatedAnnealing

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "simulated_annealing"
        "Accept worse interim states early to escape local optima; cool into refinement."
        "When greedy refactor or fix order gets stuck in a local minimum."
        [ reqStr
              "objective_function"
              "What you are improving (test pass rate, architecture gate count, latency)."
          reqStr
              "current_state"
              "Stuck local optimum description."
          reqArr
              "neighbor_moves"
              3
              "Small mutations that might worsen short-term score."
          reqStr
              "acceptance_policy"
              "When to accept worse moves (exploration phase rules)."
          reqStr
              "cooling_schedule"
              "How exploration narrows into refinement this session."
          optArr
              "best_so_far"
              1
              "Best state encountered."
          optStr
              "termination"
              "When to stop annealing and commit." ]
        "Plan exploration vs exploitation when incremental edits plateau."
        [ "Objective"
          "Neighbor moves"
          "Annealing schedule"
          "Commit criteria"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema