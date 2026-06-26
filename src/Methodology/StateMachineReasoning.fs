module Wanxiangshu.Methodology.StateMachineReasoning

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "state_machine_reasoning"
        "Enumerate legal states, transitions, and impossible states."
        "When behavior is modal: review, nudge, KG job, todo in_progress discipline."
        [ reqStr
              "machine_name"
              "Which FSM (ReviewSession, Nudge, MagicTodo projection)."
          reqArr
              "states"
              3
              "Named states with payload each carries."
          reqArr
              "transitions"
              3
              "Event-triggered edges; cite kernel module if known."
          reqArr
              "illegal_states"
              2
              "Combinations that must be unrepresentable or rejected."
          reqStr
              "current_state_guess"
              "Where this session or workspace is now."
          optArr
              "missing_transitions"
              1
              "Gaps between spec and implementation."
          optStr
              "exhaustiveness_check"
              "How compiler or tests enforce match exhaustiveness." ]
        "Make FSM explicit and align implementation with illegal state elimination."
        [ "State list"
          "Transition table"
          "Illegal states"
          "Gap analysis"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema