module VibeFs.Methodology.Operationalism

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "operationalism"
        "Define concepts by observable operations that detect or change them; discard non-behavioral distinctions."
        "When vague terms (done, stable, registered) need testable meaning."
        [ reqStr
              "vague_term"
              "Word or concept causing misalignment (parity, compatible, safe)."
          reqArr
              "observation_operations"
              3
              "Commands, tests, or grep gates that detect the concept."
          reqArr
              "mutation_operations"
              2
              "Operations that change the concept's presence."
          reqStr
              "equivalence_criterion"
              "Two implementations are the same iff observations match."
          reqArr
              "discarded_distinctions"
              1
              "Labels that make no observational difference."
          optStr
              "operational_spec"
              "Single paragraph spec engineers can implement."
          optArr
              "counterexamples"
              1
              "Cases where old vocabulary misled." ]
        "Replace metaphysical labels with observation/mutation specs."
        [ "Term under scrutiny"
          "Operational definition"
          "Discarded distinctions"
          "Implementable spec"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema