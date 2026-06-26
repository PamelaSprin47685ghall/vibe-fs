module Wanxiangshu.Methodology.SystemsThinking

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "systems_thinking"
        "Model feedback loops, dependencies, delays, and emergent behavior."
        "When a change in hooks, tools, or prompts ripples through sessions and review loops."
        [ reqStr
              "system_boundary"
              "What is inside the diagram (plugin, session, KG job, review FSM)."
          reqArr
              "stocks"
              2
              "Accumulations: message history, todo backlog, mutable maps."
          reqArr
              "flows"
              2
              "Rates of change: tool execute, transform, background reviewer."
          reqArr
              "feedback_loops"
              2
              "Reinforcing or balancing loops (nudge → todowrite → probe suppress)."
          reqArr
              "delays"
              1
              "Where lag causes oscillation or stale state (async bookkeeper)."
          reqStr
              "emergent_risk"
              "Behavior not visible in a single module."
          optArr
              "leverage_points"
              1
              "High-impact intervention spots."
          optStr
              "simulation_or_trace"
              "How to walk one session through the loops." ]
        "Draw loops and delays so host-side tweaks are not judged in isolation."
        [ "Stock-flow sketch"
          "Feedback loops"
          "Delays"
          "Leverage points"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema