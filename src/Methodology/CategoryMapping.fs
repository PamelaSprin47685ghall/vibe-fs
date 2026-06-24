module VibeFs.Methodology.CategoryMapping

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "category_mapping"
        "Preserve structure and morphisms while moving into a stronger domain (graphs, types, events)."
        "When relationships matter more than object internals."
        [ reqStr
              "source_domain"
              "Current messy domain (Dyn obj hooks, file soup)."
          reqStr
              "target_category"
              "Target language: state machine, graph, typed algebra, event log."
          reqArr
              "object_mapping"
              2
              "Objects → objects (tool → ToolSpec, message → DU)."
          reqArr
              "morphism_mapping"
              2
              "Operations → morphisms (execute, transform, replay)."
          reqStr
              "structural_property_to_preserve"
              "Composition, identity, ordering, functoriality in project terms."
          optArr
              "diagram_commutes_where"
              1
              "Commutative squares (host after kernel encode)."
          optStr
              "target_tooling"
              "Tests or types that enforce the mapping." ]
        "Functorial map from current mess to a structured domain without dropping laws."
        [ "Object map"
          "Morphism map"
          "Preserved structure"
          "Enforcement"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema