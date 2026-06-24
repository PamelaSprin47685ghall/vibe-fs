module VibeFs.Methodology.AuxiliaryConstruction

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "auxiliary_construction"
        "Introduce a helper representation that exposes a hidden relation between known and unknown."
        "When direct attack fails and a lemma, adapter, IR, or invariant would bridge facts to target."
        [ reqStr
              "known_side"
              "What is already established in code or specs."
          reqStr
              "unknown_target"
              "What you cannot reach directly yet."
          reqStr
              "auxiliary_object"
              "The helper you introduce: type, function, file, test harness, YAML template."
          reqStr
              "exposed_relation"
              "What hidden link the auxiliary makes visible."
          reqArr
              "construction_steps"
              2
              "How to build the auxiliary minimally in this repo."
          reqArr
              "discharge_steps"
              2
              "How to eliminate the auxiliary later or prove the target via it."
          optStr
              "placement"
              "Kernel vs Shell vs Methodology vs host adapter for the auxiliary."
          optArr
              "failure_modes"
              1
              "Ways the auxiliary could become permanent accidental complexity." ]
        "Design a minimal bridge object and plan to discharge it after the target is reachable."
        [ "Known vs unknown"
          "Auxiliary design"
          "Construction and discharge"
          "Placement recommendation"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema