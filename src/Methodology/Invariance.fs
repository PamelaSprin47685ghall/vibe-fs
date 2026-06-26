module Wanxiangshu.Methodology.Invariance

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "invariance"
        "Find what cannot change under allowed operations, rewrites, or state transitions."
        "When refactoring, replaying history, or parallelizing work risks breaking silent conservation laws."
        [ reqStr
              "system_under_study"
              "Component or workflow (review, todo fold, KG write, tool execute)."
          reqArr
              "allowed_operations"
              2
              "Operations you permit this turn (rename, move, add tool, replay messages)."
          reqArr
              "candidate_invariants"
              3
              "Properties that should survive: event order, id uniqueness, permission matrix, YAML prefixes."
          reqArr
              "invariant_evidence"
              2
              "Tests, types, or docs that encode each invariant."
          reqStr
              "violation_symptom"
              "What you would see if an invariant broke."
          optArr
              "non_invariants"
              1
              "Things that may change freely without harming correctness."
          optStr
              "enforcement_mechanism"
              "How to guard invariants (types, architecture test, pure kernel)." ]
        "List conserved quantities under planned edits and tie each to evidence."
        [ "Operation set"
          "Invariant table"
          "Violation symptoms"
          "Enforcement"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema