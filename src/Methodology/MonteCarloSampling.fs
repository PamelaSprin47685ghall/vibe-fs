module VibeFs.Methodology.MonteCarloSampling

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "monte_carlo_sampling"
        "Sample plausible paths when space is too large; verify critical findings deterministically."
        "When exhaustive reasoning over sessions, tool combos, or message orderings is infeasible."
        [ reqStr
              "decision_question"
              "What you need approximate confidence about."
          reqArr
              "sample_space"
              2
              "What varies across samples (tool args, hook order, random seeds)."
          reqArr
              "samples_drawn"
              3
              "Concrete samples you will or did try (commands, scenarios)."
          reqStr
              "stability_signal"
              "Pattern that stayed stable across samples."
          reqArr
              "deterministic_followups"
              2
              "Must-verify checks after sampling (specific test, grep gate)."
          optStr
              "sample_size_rationale"
              "Why this many samples is enough for this risk level."
          optArr
              "outliers"
              1
              "Samples that broke the pattern." ]
        "Sample cheaply then nail truth with deterministic verification."
        [ "Sampling plan"
          "Stable signal"
          "Outliers"
          "Deterministic followups"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema