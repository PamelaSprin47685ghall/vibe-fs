module Wanxiangshu.Methodology.Generalization

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "generalization"
        "Widen the problem to expose the underlying structure."
        "When a local fix hides a missing abstraction or repeated pattern across modules."
        [ reqStr
              "local_symptom"
              "The narrow bug or request that triggered the turn."
          reqStr
              "widened_view"
              "The larger class of problems this symptom belongs to (cross-host, all tools, all sessions)."
          reqArr
              "structural_invariants"
              2
              "What stays true when you widen (kernel purity, event history as truth)."
          reqArr
              "variation_dimensions"
              2
              "Axes along which instances differ (Mux vs Opencode, sync vs async)."
          reqStr
              "proposed_abstraction"
              "The abstraction that captures the widened view (module, type, pipeline stage)."
          reqArr
              "instances_covered"
              2
              "Concrete places the abstraction must cover; cite paths."
          optArr
              "instances_excluded"
              1
              "Where widening would be wrong; keep scope honest."
          optStr
              "refactor_slice"
              "Smallest slice to validate the abstraction before full rollout." ]
        "Lift from local patch to structural abstraction with explicit coverage and exclusions."
        [ "Widened problem statement"
          "Abstraction proposal"
          "Coverage map"
          "Excluded cases"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema