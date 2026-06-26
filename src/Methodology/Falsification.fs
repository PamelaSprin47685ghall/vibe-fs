module Wanxiangshu.Methodology.Falsification

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "falsification"
        "Formulate hypotheses with clear failure conditions; search counterexamples."
        "When a design claim risks becoming unfalsifiable narrative."
        [ reqStr
              "claim"
              "Strong statement to stress-test (always works, never needs X)."
          reqArr
              "failure_conditions"
              2
              "What observation would refute the claim."
          reqArr
              "search_attempts"
              3
              "Counterexample hunts: tests, edge inputs, hostile sequences."
          reqStr
              "verdict"
              "Survives, refuted, or scoped narrower."
          reqArr
              "surviving_scope"
              1
              "Honest weaker claim if refuted."
          optStr
              "popper_note"
              "What would make the claim unfalsifiable and must be avoided."
          optArr
              "new_tests"
              1
              "Tests to encode surviving scope." ]
        "Try to kill the claim before shipping it."
        [ "Claim"
          "Failure conditions"
          "Search log"
          "Verdict and revised scope"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema