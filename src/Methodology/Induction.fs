module VibeFs.Methodology.Induction

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "induction"
        "Infer a general rule from repeated cases or patterns."
        "When you have multiple concrete instances and need a guarded generalization for the codebase."
        [ reqArr
              "observed_cases"
              3
              "Concrete cases: file paths, test names, log lines, or user examples. One case per bullet."
          reqStr
              "shared_pattern"
              "What repeats across cases (structure, failure mode, naming, data shape)."
          reqStr
              "proposed_rule"
              "General rule in if-when-then form scoped to this project."
          reqArr
              "supporting_evidence"
              2
              "Why each case supports the rule; note weight of each case."
          reqArr
              "exceptions_seen"
              1
              "Cases that almost fit but differ; explain whether they falsify or narrow the rule."
          reqStr
              "confidence_bounds"
              "What would increase or decrease confidence (more tests, counterexample, wider sample)."
          optArr
              "predictions"
              2
              "New situations where the rule should hold; parent can verify."
          optStr
              "anti_pattern"
              "What developers should stop doing if the rule is adopted." ]
        "Generalize from repeated workspace evidence without overclaiming beyond the sample."
        [ "Case table"
          "Pattern statement"
          "Proposed rule"
          "Exception handling"
          "Predictions to verify"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema