module VibeFs.Methodology.SymmetryAnalysis

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "symmetry_analysis"
        "Exploit equivalence of cases; inspect symmetry breaking for bugs."
        "When Mux/Opencode, read/write, or dual code paths should behave mirror-wise."
        [ reqStr
              "symmetry_group"
              "What should be symmetric (two hosts, two hooks, two tool names)."
          reqArr
              "equivalent_cases"
              2
              "Paired scenarios that should match outcomes."
          reqArr
              "symmetry_breakers"
              1
              "Known intentional differences (task vs todowrite naming)."
          reqStr
              "observed_asymmetry"
              "Bug or drift: what differs without justification."
          reqArr
              "collapse_plan"
              2
              "How to unify handling (shared kernel, single codec)."
          optStr
              "canonical_side"
              "Which side is SSOT when breaking symmetry is required."
          optArr
              "regression_tests"
              1
              "Parity tests to add or run." ]
        "Separate legitimate symmetry breaking from accidental host drift."
        [ "Symmetry map"
          "Observed asymmetry"
          "Collapse plan"
          "Regression tests"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema