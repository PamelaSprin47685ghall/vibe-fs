module VibeFs.Methodology.PerturbationContinuity

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "perturbation_continuity"
        "Vary one variable slightly from an easy case to see what survives and where behavior phases-changes."
        "When a hard bug sits near a working configuration (flag off, smaller input, older branch)."
        [ reqStr
              "easy_baseline"
              "Known-good or simpler case (test passes, tool count 14, no kg/)."
          reqStr
              "hard_case"
              "Failing or complex case."
          reqArr
              "perturbations"
              3
              "One-variable changes from baseline toward hard case (add field, enable dir, scale input)."
          reqArr
              "surviving_properties"
              2
              "What stays true across perturbations until failure."
          reqStr
              "phase_change_point"
              "Which perturbation first breaks behavior; hypothesize mechanism."
          optArr
              "bisection_plan"
              2
              "Ordered experiments to localize the breakpoint."
          optStr
              "rollback_strategy"
              "How to return to safe baseline while debugging." ]
        "Bisect from easy to hard via single-variable perturbations."
        [ "Baseline vs hard case"
          "Perturbation log"
          "Phase change"
          "Bisection plan"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema