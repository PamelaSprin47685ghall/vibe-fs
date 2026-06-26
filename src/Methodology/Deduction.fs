module Wanxiangshu.Methodology.Deduction

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "deduction"
        "Derive necessary conclusions from accepted premises."
        "When premises are already agreed (tests, types, docs, user rules) and you need forced implications."
        [ reqArr
              "accepted_premises"
              2
              "Premises everyone must accept this turn: cite source (test name, file, user message, invariant)."
          reqStr
              "target_claim"
              "The conclusion you need to establish or refute."
          reqArr
              "inference_steps"
              2
              "Each step: from which premises, which rule of inference, which intermediate conclusion. No skipped leaps."
          reqStr
              "final_conclusion"
              "The deduced statement in declarative form."
          reqArr
              "premises_not_used"
              1
              "Relevant premises you explicitly did not need (documents scope discipline)."
          optArr
              "counterarguments"
              1
              "Lines of attack against the deduction; show why they fail given accepted premises."
          optStr
              "formalization_sketch"
              "Optional propositional or type-level sketch if it clarifies the chain."
          optArr
              "testable_corollaries"
              1
              "Corollaries that should become tests or grep gates." ]
        "Chain truth-preserving steps from agreed premises to a conclusion the parent can act on."
        [ "Premise ledger"
          "Inference chain"
          "Final conclusion"
          "Unused premises"
          "Corollaries and tests"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema