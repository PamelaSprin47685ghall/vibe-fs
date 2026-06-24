module VibeFs.Methodology.RiskAnalysis

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "risk_analysis"
        "Identify failure modes, blast radius, irreversible decisions."
        "Before large registration change, KG writes, or permission matrix edits."
        [ reqStr
              "proposed_change"
              "What you might ship."
          reqArr
              "failure_modes"
              3
              "Ways it fails (context overflow, oversized notebook backgrounds, test flake)."
          reqArr
              "blast_radius"
              2
              "Who/what breaks: Mux only, all hosts, user sessions."
          reqArr
              "irreversible_steps"
              1
              "Hard-to-rollback actions (published npm, migrated history)."
          reqStr
              "risk_ranking"
              "Ordered risks by severity × likelihood qualitative."
          reqArr
              "mitigations"
              2
              "Per top risk: guard, feature flag, gate, phased rollout."
          optStr
              "residual_risk"
              "Risk accepted after mitigations."
          optArr
              "monitoring"
              1
              "Signals to watch post-change." ]
        "Map failure modes and mitigations before irreversible edits."
        [ "Failure modes"
          "Blast radius"
          "Mitigations"
          "Residual risk"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema