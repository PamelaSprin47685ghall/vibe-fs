module Wanxiangshu.Methodology.SwarmOptimization

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "swarm_optimization"
        "Parallel candidate directions explore, share best findings, converge without early overcommit."
        "When multiple subagents, hypotheses, or design drafts can search in parallel."
        [ reqStr
              "collective_goal"
              "What the swarm optimizes (find root cause, design schema set)."
          reqArr
              "agents_or_hypotheses"
              3
              "Parallel tracks: coder intents, investigator questions, design options."
          reqStr
              "share_mechanism"
              "How findings merge (todo report, KG, parent synthesis)."
          reqArr
              "diversity_rules"
              2
              "How to keep agents from duplicating the same search."
          reqArr
              "convergence_criteria"
              2
              "When to stop spawning and pick a winner."
          optStr
              "best_candidate"
              "Current leader and evidence."
          optArr
              "retired_candidates"
              1
              "Tracks killed and why." ]
        "Orchestrate parallel exploration with explicit merge and convergence rules."
        [ "Swarm layout"
          "Sharing protocol"
          "Convergence"
          "Leader candidate"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema