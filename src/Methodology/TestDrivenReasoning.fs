module Wanxiangshu.Methodology.TestDrivenReasoning

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "test_driven_reasoning"
        "Make expected behavior executable before or during implementation."
        "When behavior can be pinned by tests (schema registry, Args.parse required fields, architecture gates)."
        [ reqStr
              "behavior_claim"
              "What should hold (54 schemas, required intent/background, tool names)."
          reqArr
              "executable_oracles"
              3
              "Tests or checks: file path + assertion sketch."
          reqStr
              "red_phase_plan"
              "What failing test or gate to add first."
          reqStr
              "green_phase_plan"
              "Minimal implementation to satisfy oracles."
          reqArr
              "refactor_safeties"
              1
              "Oracles that survive internal refactor."
          optStr
              "non_testable_residual"
              "Behavior still needing human review."
          optArr
              "tdd_sequence"
              2
              "Ordered red-green units of work." ]
        "Bind reasoning to executable oracles in tests/ArchitectureTests.fs style."
        [ "Behavior claim"
          "Oracles"
          "Red-green plan"
          "TDD sequence"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema