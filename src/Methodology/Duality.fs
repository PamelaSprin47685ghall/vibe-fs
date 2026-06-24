module VibeFs.Methodology.Duality

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "duality"
        "Solve the mirrored problem when the shadow formulation is easier."
        "When direct problem is hard: producer/consumer, read/write, command/event, primal/dual search."
        [ reqStr
              "primal_problem"
              "The hard formulation in current task terms."
          reqStr
              "dual_problem"
              "Mirrored view (events not state, consumer not producer, validation not generation)."
          reqArr
              "correspondence_map"
              2
              "Primal entity ↔ dual entity in this repo."
          reqStr
              "dual_solution_sketch"
              "Easier solve path in dual space."
          reqArr
              "pullback_steps"
              2
              "Map dual solution back to primal artifacts."
          optStr
              "duality_gap"
              "Where correspondence is imperfect."
          optArr
              "examples_in_repo"
              1
              "Existing dual patterns (event sourcing, caps synth)." ]
        "Work the shadow problem and map results back to implementation."
        [ "Primal–dual map"
          "Dual solution"
          "Pullback plan"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema