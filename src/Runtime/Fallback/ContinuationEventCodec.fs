module Wanxiangshu.Runtime.Fallback.ContinuationEventCodec

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.Fallback.Continuation
open Wanxiangshu.Kernel.FallbackKernel.Types

let private modelToPayload (model: FallbackModel) : (string * string) list =
    let variantStr = model.Variant |> Option.defaultValue ""

    [ "provider", model.ProviderID
      "modelId", model.ModelID
      "variant", variantStr ]

let encodeContinuationEvent (session: string) (at: string) (evt: ContinuationEvent) : WanEvent =
    match evt with
    | ContinuationEvent.Requested req ->
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

        { V = 2
          Session = session
          Kind = "continuation_requested"
          At = at
          Payload = payload }

    | ContinuationEvent.DispatchClaimed(continuationId, attempt, effectId) ->
        { V = 2
          Session = session
          Kind = "continuation_dispatch_claimed"
          At = at
          Payload =
            Map
                [ "continuationId", continuationId
                  "attempt", attempt.ToString()
                  "effectId", effectId ] }

    | ContinuationEvent.HostAccepted(continuationId, identity) ->
        let identityPairs =
            match identity with
            | ContinuationHostIdentity.AwaitingUserMessage -> []
            | ContinuationHostIdentity.UserMessageIdentity uid -> [ "userMessageId", uid ]
            | ContinuationHostIdentity.RunIdentity rid -> [ "runId", rid ]
            | ContinuationHostIdentity.OpaqueIdentity rid -> [ "receiptId", rid ]

        { V = 2
          Session = session
          Kind = "continuation_host_accepted"
          At = at
          Payload = ([ "continuationId", continuationId ] @ identityPairs) |> Map.ofList }

    | ContinuationEvent.RunStarted continuationId ->
        { V = 2
          Session = session
          Kind = "continuation_run_started"
          At = at
          Payload = Map [ "continuationId", continuationId ] }

    | ContinuationEvent.AssistantMessageObserved(continuationId, assistantMessageId) ->
        { V = 2
          Session = session
          Kind = "continuation_assistant_observed"
          At = at
          Payload = Map [ "continuationId", continuationId; "assistantMessageId", assistantMessageId ] }

    | ContinuationEvent.Settled(continuationId, reason) ->
        { V = 2
          Session = session
          Kind = "continuation_settled"
          At = at
          Payload = Map [ "continuationId", continuationId; "status", reason ] }

    | ContinuationEvent.Failed(continuationId, reason) ->
        { V = 2
          Session = session
          Kind = "continuation_failed"
          At = at
          Payload = Map [ "continuationId", continuationId; "error", reason ] }

    | ContinuationEvent.Cancelled(continuationId, reason) ->
        { V = 2
          Session = session
          Kind = "continuation_cancelled"
          At = at
          Payload = Map [ "continuationId", continuationId; "reason", reason ] }

    | ContinuationEvent.Superseded(continuationId, reason) ->
        { V = 2
          Session = session
          Kind = "continuation_superseded"
          At = at
          Payload = Map [ "continuationId", continuationId; "reason", reason ] }
