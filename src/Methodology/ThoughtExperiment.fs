module VibeFs.Methodology.ThoughtExperiment

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "thought_experiment"
        "Push idealized or extreme scenarios through rules to test concept boundaries."
        "When real execution is costly or dangerous (data loss, prod hook, 8192-word payloads)."
        [ reqStr
              "scenario_setup"
              "Idealized world assumptions (infinite history, zero latency, malicious LLM)."
          reqStr
              "rule_under_test"
              "Policy or code rule you are exercising mentally."
          reqArr
              "scenario_steps"
              3
              "Sequence of events in the thought experiment."
          reqStr
              "derived_outcome"
              "What happens if rules are applied consistently."
          reqArr
              "boundary_insights"
              2
              "Contradictions or edge cases revealed."
          optStr
              "mapping_to_real"
              "Which parts of scenario are realistic in vibe-fs."
          optArr
              "real_tests_inspired"
              1
              "Concrete tests inspired by the experiment." ]
        "Stress rules in imagination before paying execution cost."
        [ "Scenario"
          "Steps"
          "Outcome"
          "Insights"
          "Inspired tests"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema