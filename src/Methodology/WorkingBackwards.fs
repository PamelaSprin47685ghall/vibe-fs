module VibeFs.Methodology.WorkingBackwards

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "working_backwards"
        "Start from the desired end state and derive prerequisites."
        "When the goal is clear but the path is muddy; integration tests or UX outcomes are known."
        [ reqStr
              "desired_end_state"
              "Observable done: test green, tool registered, user-visible behavior, merge criteria."
          reqArr
              "acceptance_signals"
              2
              "Binary checks that prove the end state (command, assertion, screenshot description)."
          reqArr
              "prerequisite_chain"
              3
              "Backward chain: last step before done, then its prerequisites, down to today."
          reqStr
              "current_position"
              "Where the workspace is now relative to the chain."
          reqArr
              "blocking_gaps"
              1
              "Missing artifacts, unknown APIs, or unmerged branches on the critical path."
          optArr
              "parallel_tracks"
              1
              "Work that can proceed without blocking the main chain."
          optStr
              "first_forward_step"
              "The single next forward action that moves from current_position." ]
        "Reverse-plan from acceptance signals to today's executable first step."
        [ "End state definition"
          "Prerequisite chain"
          "Gap analysis"
          "First forward step"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema