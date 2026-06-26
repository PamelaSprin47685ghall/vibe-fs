module Wanxiangshu.Methodology.QuotientSpace

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "quotient_space"
        "Quotient by equivalence: solve on classes, map back to concrete cases."
        "When many objects differ only in irrelevant detail (paths, formatting, host wrapper noise)."
        [ reqStr
              "raw_objects"
              "Concrete instances that feel distinct but may be equivalent."
          reqStr
              "equivalence_relation"
              "When two instances are considered the same (canonical tool name, normalized path)."
          reqArr
              "equivalence_classes"
              2
              "Representative per class and what varies within class."
          reqStr
              "problem_on_quotient"
              "Simplified problem stated per class."
          reqArr
              "lift_map"
              2
              "How to apply class-level solution to each representative."
          optArr
              "class_counterexamples"
              1
              "Pairs that look similar but must not be merged."
          optStr
              "canonicalization_function"
              "Existing or needed normalize function in kernel." ]
        "Collapse irrelevant variation via explicit equivalence before solving."
        [ "Equivalence definition"
          "Class representatives"
          "Quotient-level solution"
          "Lift map"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema