module VibeFs.Methodology.ConstructiveMethod

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "constructive_method"
        "Build the required object, algorithm, or witness directly."
        "When existence is shown by exhibiting a concrete construction, not by contradiction."
        [ reqStr
              "object_to_construct"
              "What must exist: module list, test, data structure, tool wiring."
          reqArr
              "construction_materials"
              2
              "Existing code, types, or specs used as building blocks."
          reqArr
              "construction_steps"
              3
              "Direct build steps in order."
          reqStr
              "witness"
              "How you will demonstrate the object works (test name, sample I/O)."
          optArr
              "minimality_argument"
              1
              "Why this construction is not gratuitously large."
          optStr
              "non_constructive_alternative"
              "Why you are not using existence-only argument here."
          optArr
              "dependencies"
              1
              "External packages or host APIs required." ]
        "Exhibit an explicit build plan and witness for the required artifact."
        [ "Construction plan"
          "Witness"
          "Minimality"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema