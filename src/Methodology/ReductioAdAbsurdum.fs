module Wanxiangshu.Methodology.ReductioAdAbsurdum

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "reductio_ad_absurdum"
        "Assume the negation and derive a contradiction."
        "When proving an approach, invariant, or design choice cannot hold."
        [ reqStr
              "claim_to_refute"
              "The proposition you want to show false (e.g. single schema for all 54 tools without generation)."
          reqStr
              "assumed_negation"
              "Precise assumption for reductio."
          reqArr
              "derivation_toward_contradiction"
              3
              "Steps from negation toward conflict with known facts or invariants."
          reqStr
              "contradiction"
              "The explicit contradiction (violates test, type, user rule, physics of JS runtime)."
          reqArr
              "facts_used"
              2
              "Anchored facts that make the contradiction unavoidable."
          optStr
              "positive_alternative"
              "What becomes necessary once the negation is dead."
          optArr
              "limits_of_argument"
              1
              "Scope where reductio does not apply." ]
        "Assume the unwanted design and drive it into a workspace-anchored contradiction."
        [ "Negation setup"
          "Derivation"
          "Contradiction"
          "Positive alternative"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema