module Wanxiangshu.Methodology.DebuggingTrace

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "debugging_trace"
        "Reproduce, isolate, instrument, verify the fault chain."
        "When a failure needs systematic narrowing (Fable build, hook, integration test)."
        [ reqStr
              "failure_signature"
              "Exact error, stack, or assertion text."
          reqStr
              "reproduction_steps"
              "Minimal steps to reproduce (command, env)."
          reqArr
              "isolation_experiments"
              3
              "What you removed or swapped to narrow cause."
          reqArr
              "instrumentation_points"
              2
              "Logs, temporary asserts, read-only traces (no permanent log spam)."
          reqStr
              "fault_chain"
              "Ordered chain from trigger to symptom."
          reqStr
              "verified_fix_hypothesis"
              "Smallest change predicted to break the chain."
          optArr
              "ruled_out_causes"
              2
              "Causes eliminated with evidence."
          optStr
              "regression_guard"
              "Test to add after fix." ]
        "Document reproduce→isolate→instrument→verify without guessing."
        [ "Reproduction"
          "Isolation log"
          "Fault chain"
          "Fix hypothesis"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema