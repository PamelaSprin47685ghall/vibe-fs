module VibeFs.Methodology.BranchAndBound

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "branch_and_bound"
        "Prune dominated or impossible branches using bounds."
        "When exhaustive search over refactor or config options needs disciplined pruning."
        [ reqStr
              "optimization_target"
              "What you minimize or maximize (lines changed, tools exposed, test time)."
          reqArr
              "branches"
              2
              "Major alternatives under consideration."
          reqArr
              "lower_bounds"
              2
              "Best-case cost or benefit per branch."
          reqArr
              "upper_bounds"
              2
              "Worst-case or proven infeasibility bounds."
          reqArr
              "pruned_branches"
              1
              "Branches cut because bound shows dominance or impossibility."
          reqStr
              "active_branch"
              "Branch still worth exploring."
          optArr
              "bound_evidence"
              1
              "Tests, metrics, or architecture rules supplying bounds."
          optStr
              "stop_condition"
              "When further branching is wasteful." ]
        "Rank branches with bounds and document pruning decisions."
        [ "Branch table"
          "Bounds"
          "Pruning rationale"
          "Active branch"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema