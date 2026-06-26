module Wanxiangshu.Methodology.AnalysisSynthesis

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "analysis_synthesis"
        "Analyze backward from the desired result to known facts, then synthesize forward into a plan."
        "When the goal is clear but construction path is not; large features or refactors."
        [ reqStr
              "target_result"
              "What must exist at the end (behavior, files, tests)."
          reqArr
              "backward_analysis"
              3
              "Working backward: each bullet states a required condition and whether it is already true in repo."
          reqArr
              "known_facts"
              2
              "Anchored facts from code, tests, docs—no speculation."
          reqArr
              "synthesis_steps"
              3
              "Forward build order: each step produces an artifact the next step needs."
          reqStr
              "integration_point"
              "Where synthesized pieces plug into existing Kernel/Shell/Opencode/Mux boundaries."
          optArr
              "risks_in_synthesis"
              1
              "Ordering risks, double work, or host divergence."
          optStr
              "validation_milestone"
              "Mid-point check before full synthesis completes." ]
        "Split backward feasibility analysis from forward construction schedule."
        [ "Backward condition table"
          "Known facts"
          "Forward synthesis plan"
          "Integration point"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema