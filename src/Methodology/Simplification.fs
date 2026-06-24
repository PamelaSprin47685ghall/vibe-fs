module VibeFs.Methodology.Simplification

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "simplification"
        "Remove accidental complexity until only essential problem remains."
        "When solution path is cluttered with frameworks, flags, duplicate adapters."
        [ reqStr
              "overcomplicated_surface"
              "What feels heavier than the problem (duplicate MessageTransform, long Dyn chains)."
          reqArr
              "accidental_parts"
              3
              "Pieces not required by user goal or invariants—candidate removal."
          reqStr
              "essential_core"
              "What must remain for correctness."
          reqArr
              "simplification_moves"
              3
              "Concrete removals or merges with risk note each."
          reqArr
              "invariants_preserved"
              2
              "What simplification must not break."
          optStr
              "simplification_metric"
              "How you will know it is simpler (lines, tool count, modules)."
          optArr
              "deferred_complexity"
              1
              "Complexity postponed with explicit trigger to revisit." ]
        "Peel accidental layers without violating essential invariants."
        [ "Accidental inventory"
          "Essential core"
          "Simplification moves"
          "Preserved invariants"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema