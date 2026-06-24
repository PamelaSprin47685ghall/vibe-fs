module VibeFs.Methodology.Renormalization

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "renormalization"
        "Coarse-grain micro-detail; keep scale-relevant variables; find stable macro structure."
        "When micro implementation noise obscures macro behavior (54 files, hook spaghetti)."
        [ reqStr
              "micro_level"
              "Fine detail drowning analysis (every Dyn call, every test line)."
          reqStr
              "macro_question"
              "What behavior matters at session/plugin scale."
          reqArr
              "coarse_graining_map"
              2
              "What you average, sum, or ignore at macro level."
          reqArr
              "relevant_variables"
              3
              "Variables still predictive after coarse-graining (tool count, queue depth)."
          reqStr
              "universal_pattern"
              "Structure stable across scales (layering, event truth)."
          optArr
              "micro_corrections"
              1
              "When macro view fails—return to micro."
          optStr
              "documentation_level"
              "What belongs in README vs per-file schema." ]
        "Summarize micro complexity into macro laws for decision-making."
        [ "Coarse-graining"
          "Macro variables"
          "Stable pattern"
          "When to re-zoom"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema