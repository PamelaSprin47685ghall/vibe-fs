module VibeFs.Methodology.Relaxation

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "relaxation"
        "Temporarily weaken constraints, solve superset, project back under real constraints."
        "When hard integer, ordering, permission, or exactness constraints block search."
        [ reqStr
              "hard_problem"
              "Fully constrained problem as stated."
          reqArr
              "constraints_relaxed"
              2
              "Which constraints you drop temporarily and why that enlarges feasible set."
          reqStr
              "relaxed_solution"
              "Solution valid in superset."
          reqArr
              "projection_steps"
              2
              "How to round, validate, or trim back to real constraints."
          reqArr
              "infeasible_after_projection"
              1
              "Parts of relaxed solution that cannot project—need alternate."
          optStr
              "relaxation_cost"
              "Risk of keeping relaxed cheat in production."
          optArr
              "validation_gates"
              1
              "Tests proving projected solution respects hard rules." ]
        "Solve easier superset then honest projection with validation gates."
        [ "Relaxation map"
          "Relaxed solution"
          "Projection"
          "Validation"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema