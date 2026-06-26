module Wanxiangshu.Methodology.Axiomatization

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "axiomatization"
        "State primitive terms, allowed operations, invariants, forbidden states, and derivation rules explicitly; then solve only inside that declared system."
        "When definitions drift, hidden assumptions make reasoning unstable, or multiple teams talk past each other."
        [ reqStr
              "system_name"
              "Short name for the formal slice you are axiomatizing (e.g. review loop, todo projection, tool permission)."
          reqArr
              "primitive_terms"
              3
              "Undefined carriers of meaning in this slice. Each entry: term + one-sentence intended meaning in this repo."
          reqArr
              "allowed_operations"
              2
              "Operations agents or code may perform on those terms. Include preconditions in the same bullet."
          reqArr
              "invariants"
              3
              "Properties that must hold in every legal state (ordering, uniqueness, idempotency, conservation)."
          reqArr
              "forbidden_states"
              2
              "Explicit illegal combinations (e.g. two in_progress todos, review active without task id)."
          reqArr
              "derivation_rules"
              2
              "If A and B then C style rules grounded in files or tests; cite path when possible."
          reqStr
              "scope_boundary"
              "What this axiom system intentionally does not cover."
          optArr
              "consistency_checks"
              1
              "Executable checks (tests, grep, architecture gates) that would falsify the axiom set."
          optStr
              "known_ambiguities"
              "Terms still contested; propose disambiguation before coding." ]
        "Freeze vocabulary and legal moves so downstream edits cannot smuggle undefined behavior."
        [ "Primitive term glossary"
          "Operation table with preconditions"
          "Invariant list"
          "Forbidden states"
          "Derivation rules applied to current task"
          "Consistency checks"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema