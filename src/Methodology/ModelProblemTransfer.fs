module VibeFs.Methodology.ModelProblemTransfer

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "model_problem_transfer"
        "Transfer a solution skeleton from a canonical template when topology matches."
        "When the task resembles a well-known pattern: plugin adapter, state machine, codec boundary."
        [ reqStr
              "canonical_template"
              "Named pattern (e.g. ToolCatalog→ToolSchema, review replay, fuzzy iterator)."
          reqStr
              "current_problem"
              "What you are solving now in this repo."
          reqArr
              "shared_unknowns"
              2
              "Unknowns that match the template (schema gen, host obj, async queue)."
          reqArr
              "shared_constraints"
              2
              "Constraints that match (Fable, dual host, pure kernel)."
          reqArr
              "transfer_steps"
              3
              "Skeleton steps copied from template with file-level targets."
          reqArr
              "assumption_failures"
              1
              "Template assumptions that fail here and require local redesign."
          optStr
              "reference_implementation"
              "Path to the canonical code to mirror."
          optArr
              "checklist"
              2
              "Per-step done criteria." ]
        "Map current work onto a canonical repo pattern and flag broken assumptions."
        [ "Template mapping"
          "Transfer steps"
          "Failed assumptions"
          "Checklist"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema