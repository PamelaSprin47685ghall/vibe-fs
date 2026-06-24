module VibeFs.Methodology.DimensionalReduction

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "dimensional_reduction"
        "Project to a lower-dimensional view, reason there, lift conclusions cautiously."
        "When full state space is too large: long sessions, 54 tools, entire monorepo."
        [ reqStr
              "full_state_description"
              "What makes the space huge (messages, files, mutable maps)."
          reqStr
              "projection"
              "The slice: one session, one module, one test failure, one tool call."
          reqArr
              "dropped_dimensions"
              2
              "What you ignore in the slice and why that is safe for this question."
          reqStr
              "reasoning_in_slice"
              "Conclusion valid inside the projection."
          reqArr
              "lift_risks"
              2
              "Ways the conclusion might fail when lifted to full state."
          optStr
              "minimal_reproduction"
              "Smallest command or test reproducing the issue."
          optArr
              "follow_up_projections"
              1
              "Other slices if the first is inconclusive." ]
        "Reason in a deliberate slice and document lift hazards."
        [ "Projection definition"
          "In-slice reasoning"
          "Lift risks"
          "Minimal reproduction"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema