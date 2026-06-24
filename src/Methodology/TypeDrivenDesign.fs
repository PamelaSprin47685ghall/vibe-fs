module VibeFs.Methodology.TypeDrivenDesign

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "type_driven_design"
        "Encode domain boundaries and illegal states in types."
        "Before implementing hooks or tools still passing Dyn obj through business logic."
        [ reqStr
              "domain_slice"
              "Boundary to type (methodology args, review verdict, tool permission)."
          reqArr
              "illegal_states_today"
              2
              "Bad combos currently representable (null task id, empty intents)."
          reqArr
              "algebraic_model"
              3
              "DU/records: cases, fields, smart constructors."
          reqStr
              "encoding_plan"
              "Where types live (Kernel) and where decode happens (Shell/codec)."
          reqArr
              "operations_as_functions"
              2
              "Pure functions on types replacing boolean flags."
          optArr
              "compiler_guarantees"
              1
              "What mistakes become compile errors."
          optStr
              "migration_from_dyn"
              "Steps to shrink Dyn surface." ]
        "Design types so illegal states are unwritable before coding handlers."
        [ "Illegal state inventory"
          "Algebraic model"
          "Codec boundary"
          "Migration steps"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema