module VibeFs.Methodology.TradeoffAnalysis

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "tradeoff_analysis"
        "Compare options across explicit constraints and costs."
        "When choosing between registration strategies, schema layout, host parity approaches."
        [ reqStr
              "decision"
              "Choice to make (54 notebook tools vs generated, per-methodology fields vs one generic note field)."
          reqArr
              "options"
              2
              "Named options with one-line summary each."
          reqArr
              "constraints"
              3
              "Hard limits: AGENTS.md, test time, context window, dual host."
          reqArr
              "cost_dimensions"
              2
              "Dimensions: dev time, token cost, maintenance, UX for LLM."
          reqStr
              "comparison_matrix"
              "Narrative or table comparing options on constraints and costs."
          reqStr
              "recommendation"
              "Preferred option with explicit accepted costs."
          optArr
              "reversible_parts"
              1
              "What you can undo cheaply if wrong."
          optStr
              "decision_deadline"
              "What happens if you defer deciding." ]
        "Compare options honestly on named constraints—not vibes."
        [ "Options"
          "Constraint table"
          "Recommendation"
          "Reversibility"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema