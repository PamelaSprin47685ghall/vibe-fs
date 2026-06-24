module VibeFs.Methodology.RootCauseAnalysis

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "root_cause_analysis"
        "Trace symptoms to causal fault, not visible failure only."
        "When repeated failures, flaky tests, or incident-like tool errors need depth."
        [ reqStr
              "symptom"
              "What users or tests see (message, count mismatch, timeout)."
          reqStr
              "visible_failure"
              "Immediate failing component (assertion line, hook)."
          reqArr
              "why_chain"
              4
              "Five-whys style chain: each why backed by evidence or marked hypothesis."
          reqStr
              "root_cause"
              "Actionable fault (wrong assumption, missing gate, race)."
          reqArr
              "contributing_factors"
              1
              "Factors that amplified but are not root."
          reqStr
              "fix_target"
              "What to change so symptom cannot recur."
          optArr
              "verification_after_fix"
              2
              "Tests or traces proving root addressed."
          optStr
              "symptom_vs_cause_guard"
              "How to avoid stopping at visible_failure." ]
        "Drive why-chain to an actionable root with verification, not patch-the-symptom."
        [ "Symptom vs visible failure"
          "Why chain"
          "Root cause"
          "Fix target"
          "Verification"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema