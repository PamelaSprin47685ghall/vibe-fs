module Wanxiangshu.Methodology.UserIntentClarification

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "user_intent_clarification"
        "Resolve ambiguous goals before optimizing the wrong target."
        "When user request could mean schema-only, full wiring, or design discussion."
        [ reqStr
              "user_request_quote"
              "Paraphrase or quote of what user asked."
          reqArr
              "interpretations"
              3
              "Plausible readings with different deliverables."
          reqArr
              "disambiguating_questions"
              2
              "Questions to ask user if still blocked—prefer concrete either/or."
          reqStr
              "assumed_intent"
              "Intent you will proceed under if user silent, with risk stated."
          reqArr
              "success_criteria_per_interpretation"
              2
              "How you would know each interpretation is satisfied."
          optStr
              "misinterpretation_cost"
              "What goes wrong if wrong interpretation."
          optArr
              "clarified_out_of_scope"
              1
              "What user did not ask for but might be assumed." ]
        "Make interpretations explicit before large automated work."
        [ "Interpretations"
          "Questions"
          "Working assumption"
          "Success criteria"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema