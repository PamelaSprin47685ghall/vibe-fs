module VibeFs.Methodology.DynamicProgramming

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "dynamic_programming"
        "Exploit overlapping subproblems and optimal substructure with memoized state transitions."
        "When repeated subtasks appear (schema gen per tool, replay segments, fuzzy pages)."
        [ reqStr
              "top_level_goal"
              "End computation or design you need."
          reqArr
              "subproblems"
              3
              "Reusable subtasks with clear inputs/outputs."
          reqArr
              "overlap_evidence"
              2
              "Where the same subproblem appears multiple times in this work."
          reqStr
              "state_definition"
              "DP state keys (session id, iterator, tool name)."
          reqArr
              "transitions"
              2
              "How to combine sub-solutions."
          reqStr
              "memoization_plan"
              "Where to cache (Shell store, pure Map, file cache) and invalidation."
          optArr
              "base_cases"
              2
              "Terminal subproblems with known answers."
          optStr
              "complexity_note"
              "Time/space vs naive recursion." ]
        "Factor overlapping work into memoized states appropriate for vibe-fs stores."
        [ "Subproblem decomposition"
          "State and transitions"
          "Memoization"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema