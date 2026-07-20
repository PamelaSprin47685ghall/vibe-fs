module Wanxiangshu.Runtime.Wanxiangzhen.OrphanNotify

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope

let kindWarningSent = "wanxiangzhen_warning_sent"
let kindPromptFailed = "wanxiangzhen_prompt_failed"

let sortedOrphanIds (orphanTaskIds: string list) : string list =
    orphanTaskIds |> List.distinct |> List.sort

/// Stable idempotency key for orphan-no-PID notification (order-independent).
let idempotencyKey (orphanTaskIds: string list) : string =
    "wanxiangzhen:orphan_no_pid:" + String.concat "," (sortedOrphanIds orphanTaskIds)

let warningText (orphanTaskIds: string list) : string =
    let names = sortedOrphanIds orphanTaskIds |> String.concat ", "
    sprintf "WARNING: Orphan running tasks without PID: %s. Use /squad-kill or ignore." names

let warningSentEvent (sessionId: string) (at: string) (key: string) (warning: string) : WanEvent =
    { V = 1
      Session = sessionId
      Kind = kindWarningSent
      At = at
      Payload = Map [ "idempotencyKey", key; "warning", warning ] }

let promptFailedEvent (sessionId: string) (at: string) (key: string) (text: string) (error: string) : WanEvent =
    { V = 1
      Session = sessionId
      Kind = kindPromptFailed
      At = at
      Payload = Map [ "idempotencyKey", key; "text", text; "error", error ] }

/// Recover keys already delivered. Prefers payload.idempotencyKey; legacy rows used full warning text as key.
let recoverSentKeys (sessionId: string) (events: WanEvent list) : Set<string> =
    events
    |> List.filter (fun e -> e.Session = sessionId && e.Kind = kindWarningSent)
    |> List.choose (fun e ->
        match Map.tryFind "idempotencyKey" e.Payload with
        | Some k when k <> "" -> Some k
        | _ -> Map.tryFind "warning" e.Payload)
    |> Set.ofList
