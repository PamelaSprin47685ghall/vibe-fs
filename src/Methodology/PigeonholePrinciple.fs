module Wanxiangshu.Methodology.PigeonholePrinciple

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "pigeonhole_principle"
        "Use counts and capacities to prove collision, overflow, or coverage must occur."
        "When exact placement is unknown but pigeonhole forces a conclusion (tools, slots, ports, ids)."
        [ reqStr
              "items"
              "What you are placing (sessions, jobs, tools, enum values, file handles)."
          reqStr
              "slots"
              "Distinct containers or capacities (ports, registry size, unique ids)."
          reqStr
              "counting_argument"
              "Arithmetic showing items > slots or forced repetition."
          reqStr
              "forced_conclusion"
              "What must happen: collision, queue, strip, failure."
          reqArr
              "evidence_counts"
              2
              "Numbers from code or tests backing items and slots."
          optArr
              "mitigations"
              1
              "Design changes that increase slots or reduce items."
          optStr
              "observable_signature"
              "Log or error pattern proving pigeonhole fired." ]
        "Make counting contradiction explicit for resource or namespace limits."
        [ "Items vs slots"
          "Counting proof"
          "Forced conclusion"
          "Mitigations"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema