module Wanxiangshu.Methodology.EventSourcing

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "event_sourcing"
        "Separate commands from facts; derive current state from event history."
        "When mutable maps disagree with message history or replay is required."
        [ reqStr
              "command_side"
              "Intents that can be rejected (tool call, user message, submit_review)."
          reqStr
              "event_side"
              "Facts once accepted (tool result, verdict YAML, KG append)."
          reqArr
              "events_list"
              2
              "Event types in this slice with payload shape."
          reqStr
              "fold_function"
              "How events reduce to current projection."
          reqArr
              "replay_requirements"
              2
              "Ordering, idempotency, version fields."
          optStr
              "snapshot_policy"
              "When snapshots are bookmarks only vs truth."
          optArr
              "correction_events"
              1
              "Compensating events instead of UPDATE."
          optStr
              "anti_patterns"
              "In-place mutation destroying audit trail." ]
        "Align command/event split with history-as-truth discipline in vibe-fs."
        [ "Command vs event"
          "Event catalog"
          "Fold/replay"
          "Correction strategy"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema