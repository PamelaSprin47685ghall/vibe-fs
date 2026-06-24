module VibeFs.Methodology.PerformanceAnalysis

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "performance_analysis"
        "Locate bottlenecks, asymptotics, and resource constraints."
        "When many methodology notebook tools, large backgrounds, Fable compile, or session history size matters."
        [ reqStr
              "performance_question"
              "Latency, memory, context tokens, or build time concern."
          reqStr
              "workload_model"
              "Who does work how often (per tool call, per session transform)."
          reqArr
              "hot_paths"
              2
              "Suspected bottlenecks with file or pipeline reference."
          reqArr
              "complexity_notes"
              2
              "Asymptotic or scaling behavior (O(tools×schema), history length)."
          reqStr
              "measurement_plan"
              "What to measure (build time, output bytes) without fancy APM."
          reqArr
              "optimization_candidates"
              2
              "Changes with expected impact; avoid premature micro-opt."
          optStr
              "budget"
              "Acceptable limits (tool result size, compile seconds)."
          optArr
              "anti_optimizations"
              1
              "Opts that hurt clarity and should wait." ]
        "Tie performance claims to workload and measurement."
        [ "Workload"
          "Hot paths"
          "Measurement"
          "Candidates"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema