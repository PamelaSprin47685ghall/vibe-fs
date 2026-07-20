module Wanxiangshu.Hosts.Opencode.ChatHooksProvenance

open Fable.Core
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Hosts.Opencode.ChatHooksDecoders

/// Append a continuation_host_accepted event recording that a user message
/// was observed for the given continuation. Does not modify memory state.
let recordContinuationUserMessage
    (workspaceRoot: string)
    (sessionId: string)
    (messageId: string)
    (continuationId: string)
    : JS.Promise<unit> =
    promise {
        let at = getTimestampMs().ToString()

        let wanEvent: WanEvent =
            { V = 2
              Session = sessionId
              Kind = eventKindContinuationHostAccepted
              At = at
              Payload = Map [ "continuationId", continuationId; "userMessageId", messageId ] }

        do! appendEventsAndCacheOrFail workspaceRoot [ wanEvent ]
    }

let recordProvenanceIfPresent
    (parts: obj)
    (msgId: string)
    (workspaceRoot: string)
    (sessionIDStr: string)
    : JS.Promise<unit> =
    promise {
        if msgId <> "" then
            match tryDecodeWanxiangshuProvenance parts with
            | Some provenance ->
                do! recordContinuationUserMessage workspaceRoot sessionIDStr msgId provenance.ContinuationId
            | None -> ()
    }
