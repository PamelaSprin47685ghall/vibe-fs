module Wanxiangshu.Methodology.BayesianUpdate

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "bayesian_update"
        "Update belief strength as evidence arrives; avoid all-or-nothing after one test."
        "When multiple hypotheses compete (host bug vs kernel bug vs stale build)."
        [ reqStr
              "hypothesis_set"
              "Mutually exclusive or overlapping hypotheses with short labels."
          reqArr
              "prior_weights"
              2
              "Before new evidence: qualitative or numeric priors and why."
          reqArr
              "new_evidence"
              2
              "Fresh observations this turn."
          reqArr
              "likelihood_sketch"
              2
              "P(evidence|hypothesis) qualitative: which hypothesis each evidence favors."
          reqStr
              "posterior_summary"
              "Ranking after update; no false precision required."
          optArr
              "decisive_experiment"
              1
              "Next evidence that would swing posterior most."
          optStr
              "discarded_hypotheses"
              "Hypotheses effectively ruled out." ]
        "Qualitative Bayesian update over competing engineering hypotheses."
        [ "Priors"
          "Evidence"
          "Likelihood notes"
          "Posterior"
          "Decisive experiment"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema