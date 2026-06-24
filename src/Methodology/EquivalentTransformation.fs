module VibeFs.Methodology.EquivalentTransformation

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "equivalent_transformation"
        "Convert the problem into an equivalent form where reasoning or implementation is easier."
        "When the current representation is noisy: control flow, JSON blobs, implicit state."
        [ reqStr
              "source_representation"
              "How the problem is stated today (code shape, protocol, message flow)."
          reqStr
              "target_representation"
              "Equivalent form: events, graph, types, algebra, tables."
          reqStr
              "equivalence_claim"
              "Why solutions in target map 1:1 to source (preserved observables)."
          reqArr
              "transformation_steps"
              2
              "Mechanical rewrite steps safe for this codebase."
          reqArr
              "preserved_properties"
              2
              "Invariants that must survive the transform (ordering, idempotency, permissions)."
          optArr
              "lost_detail"
              1
              "Information dropped in transform and how to recover if needed."
          optStr
              "verification"
              "Test or diff strategy proving equivalence." ]
        "Justify a representation change and list preserved observables before rewriting."
        [ "Source vs target representation"
          "Equivalence argument"
          "Transformation steps"
          "Verification plan"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema