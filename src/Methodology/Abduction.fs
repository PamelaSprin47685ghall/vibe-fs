module Wanxiangshu.Methodology.Abduction

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "abduction"
        "Generate the best causal hypothesis for surprising evidence, then seek discriminating tests."
        "When debugging, diagnosing, investigating, or explaining outcomes that violate expectations."
        [ reqStr
              "surprising_evidence"
              "What was observed that conflicted with expectation; include exact error strings or metrics if any."
          reqStr
              "context_anchor"
              "Where in the repo, session, or runtime this appeared (paths, commands, hook names)."
          reqStr
              "hypothesis"
              "Best causal explanation: if X then we would see Y. One primary hypothesis."
          reqArr
              "discriminating_tests"
              2
              "Tests or reads that distinguish this hypothesis from alternatives; must be executable by parent."
          reqArr
              "alternative_hypotheses"
              1
              "Other plausible causes ranked weaker; one line each."
          reqStr
              "expected_observations_if_true"
              "What else should be true if the primary hypothesis holds."
          optArr
              "ruled_out_paths"
              1
              "Investigations already done that eliminated simpler causes."
          optStr
              "stop_rule"
              "When to abandon this hypothesis (what counterevidence would suffice)." ]
        "Propose the best explanation for surprise and spell discriminating checks—not treat guess as fact."
        [ "Evidence summary"
          "Primary hypothesis"
          "Alternatives"
          "Discriminating test plan"
          "Expected observations"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema