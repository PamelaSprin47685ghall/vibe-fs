module VibeFs.Methodology.SearchSpaceExploration

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "search_space_exploration"
        "Model candidates as a space or graph; choose traversal strategy."
        "When many design or fix options exist and ad-hoc picking is unsafe."
        [ reqStr
              "search_goal"
              "What you are optimizing or finding (minimal diff, passing test, schema design)."
          reqArr
              "state_nodes"
              3
              "Representative states in the space (configs, branches, tool sets)."
          reqArr
              "moves"
              2
              "Edges: edit file, add test, register tool, revert."
          reqStr
              "traversal_strategy"
              "BFS, DFS, beam, greedy—chosen deliberately with stop rule."
          reqArr
              "pruned_branches"
              1
              "Branches ruled out with reason."
          optStr
              "heuristic"
              "Ranking function for candidates."
          optArr
              "frontier_snapshot"
              1
              "Current best candidates and scores." ]
        "Make the design space explicit and document traversal and pruning."
        [ "State graph sketch"
          "Traversal strategy"
          "Pruning log"
          "Frontier"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema