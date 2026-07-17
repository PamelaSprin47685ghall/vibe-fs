module Wanxiangshu.Runtime.Fallback.ContinuationEventCodec

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.Fallback.Continuation
open Wanxiangshu.Kernel.FallbackKernel.Types

let private modelToPayload (model: FallbackModel) : (string * string) list =
    let variantStr = model.Variant |> Option.defaultValue ""

    [ "provider", model.ProviderID
      "modelId", model.ModelID
      "variant", variantStr ]

let private encodeContinuationEventBase session at kind payload =
    { V = 2
      Session = session
      Kind = kind
      At = at
      Payload = payload }

let private hostIdentityPairs identity =
    match identity with
    | ContinuationHostIdentity.AwaitingUserMessage -> []
    | ContinuationHostIdentity.UserMessageIdentity uid -> [ "userMessageId", uid ]
    | ContinuationHostIdentity.RunIdentity rid -> [ "runId", rid ]
    | ContinuationHostIdentity.OpaqueIdentity rid -> [ "receiptId", rid ]

let private encodeRequestedContinuation session at (req: ContinuationRequest) =
    let modeStr, promptStr =
        match req.Mode with
        | ContinuationMode.ResumeInterruptedTurn -> "ResumeInterruptedTurn", ""
        | ContinuationMode.RecoverToolCallText prompt -> "RecoverToolCallText", prompt

    let payload =
        [ "continuationId", req.ContinuationId
          "continuationOrdinal", req.ContinuationOrdinal.ToString()
          "attempt", req.Attempt.ToString()
          "humanTurnId", req.HumanTurnId
          "contextGeneration", req.ContextGeneration.ToString()
          "cancelGeneration", req.CancelGeneration.ToString()
          "agent", req.Agent
          "mode", modeStr
          "prompt", promptStr
          yield! modelToPayload req.Model
          yield!
              req.SourceHumanMessageId
              |> Option.map (fun m -> [ "sourceHumanMessageId", m ])
              |> Option.defaultValue [] ]
        |> Map.ofList

    encodeContinuationEventBase session at "continuation_requested" payload

let encodeContinuationEvent (session: string) (at: string) (evt: ContinuationEvent) : WanEvent =
    match evt with
    | ContinuationEvent.Requested req -> encodeRequestedContinuation session at req
    | ContinuationEvent.DispatchClaimed(continuationId, attempt, effectId) ->
        encodeContinuationEventBase
            session
            at
            "continuation_dispatch_claimed"
            (Map
                [ "continuationId", continuationId
                  "attempt", attempt.ToString()
                  "effectId", effectId ])

    | ContinuationEvent.HostAccepted(continuationId, identity) ->
        encodeContinuationEventBase
            session
            at
            "continuation_host_accepted"
            (("continuationId", continuationId) :: hostIdentityPairs identity |> Map.ofList)

    | ContinuationEvent.RunStarted continuationId ->
        encodeContinuationEventBase session at "continuation_run_started" (Map [ "continuationId", continuationId ])

    | ContinuationEvent.AssistantMessageObserved(continuationId, assistantMessageId) ->
        encodeContinuationEventBase
            session
            at
            "continuation_assistant_observed"
            (Map [ "continuationId", continuationId; "assistantMessageId", assistantMessageId ])

    | ContinuationEvent.Settled(continuationId, reason) ->
        encodeContinuationEventBase
            session
            at
            "continuation_settled"
            (Map [ "continuationId", continuationId; "status", reason ])

    | ContinuationEvent.Failed(continuationId, reason) ->
        encodeContinuationEventBase
            session
            at
            "continuation_failed"
            (Map [ "continuationId", continuationId; "error", reason ])

    | ContinuationEvent.Cancelled(continuationId, reason) ->
        encodeContinuationEventBase
            session
            at
            "continuation_cancelled"
            (Map [ "continuationId", continuationId; "reason", reason ])

    | ContinuationEvent.Superseded(continuationId, reason) ->
        encodeContinuationEventBase
            session
            at
            "continuation_superseded"
            (Map [ "continuationId", continuationId; "reason", reason ])
