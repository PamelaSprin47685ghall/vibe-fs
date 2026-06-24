module VibeFs.Methodology.TranscendentalArgument

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "transcendental_argument"
        "Ask what must already exist for an undeniable fact or behavior to be possible."
        "When a capability clearly works and you need preconditions (replay, caps, review)."
        [ reqStr
              "undeniable_fact"
              "Fact hard to dispute (tests pass, plugin loads, message replays)."
          reqArr
              "necessary_preconditions"
              3
              "Structures that must exist (event log, ToolSpec SSOT, Fable compile)."
          reqStr
              "dependency_chain"
              "How preconditions enable the fact."
          reqArr
              "missing_precondition_tests"
              1
              "What would break if a precondition were removed."
          optStr
              "philosophical_limit"
              "What this argument does not prove (implementation detail)."
          optArr
              "engineering_implications"
              2
              "What not to delete in refactor." ]
        "Reverse-engineer prerequisites from stable facts."
        [ "Undeniable fact"
          "Precondition chain"
          "Break tests"
          "Implications"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema