namespace Wanxiangshu.Next.Session

open Wanxiangshu.Next.Kernel.Identity

type PromptPurpose =
    | ContinueTodo
    | RetryTurn
    | SwitchModel
    | ReviewChanges
    | RunChild
    | ReturnToParent

module PromptPurpose =

    let toStableString (purpose: PromptPurpose) : string =
        match purpose with
        | ContinueTodo -> "ContinueTodo"
        | RetryTurn -> "RetryTurn"
        | SwitchModel -> "SwitchModel"
        | ReviewChanges -> "ReviewChanges"
        | RunChild -> "RunChild"
        | ReturnToParent -> "ReturnToParent"

type Model =
    { ProviderId: string
      ModelId: string
      Variant: string option }

module Model =

    let create (providerId: string) (modelId: string) (variant: string option) : Model =
        { ProviderId = providerId
          ModelId = modelId
          Variant = variant }

    let ofString (s: string) : Result<Model, string> =
        if System.String.IsNullOrWhiteSpace(s) then
            Error "Model string cannot be empty"
        else
            let parts = s.Split('/')

            if parts |> Array.exists System.String.IsNullOrWhiteSpace then
                Error $"Model string contains empty segment: '{s}'"
            else
                match parts with
                | [| p; m |] ->
                    Ok
                        { ProviderId = p
                          ModelId = m
                          Variant = None }
                | [| p; m; v |] ->
                    Ok
                        { ProviderId = p
                          ModelId = m
                          Variant = Some v }
                | _ -> Error $"Invalid model string format (expected provider/model or provider/model/variant): '{s}'"

    let toStableString (m: Model) : string =
        match m.Variant with
        | Some v -> sprintf "%s/%s/%s" m.ProviderId m.ModelId v
        | None -> sprintf "%s/%s" m.ProviderId m.ModelId

type PromptKey =
    private
        { SessionId: SessionId
          TurnId: TurnId
          Purpose: PromptPurpose
          Model: Model option
          Attempt: int
          TriggerMessageId: MessageId option
          PayloadHash: string }

module PromptKey =

    let create
        (sessionId: SessionId)
        (turnId: TurnId)
        (purpose: PromptPurpose)
        (model: Model option)
        (attempt: int)
        (triggerMessageId: MessageId option)
        (payloadHash: string)
        : PromptKey =
        { SessionId = sessionId
          TurnId = turnId
          Purpose = purpose
          Model = model
          Attempt = attempt
          TriggerMessageId = triggerMessageId
          PayloadHash = payloadHash }

    let sessionId (pk: PromptKey) = pk.SessionId
    let turnId (pk: PromptKey) = pk.TurnId
    let purpose (pk: PromptKey) = pk.Purpose
    let model (pk: PromptKey) = pk.Model
    let attempt (pk: PromptKey) = pk.Attempt
    let triggerMessageId (pk: PromptKey) = pk.TriggerMessageId
    let payloadHash (pk: PromptKey) = pk.PayloadHash

    let private escapeField (s: string) : string = System.Uri.EscapeDataString(s)

    let asString (pk: PromptKey) : string =
        let sId = escapeField (SessionId.value pk.SessionId)
        let tId = escapeField (TurnId.value pk.TurnId)
        let purp = escapeField (PromptPurpose.toStableString pk.Purpose)

        let mdl =
            match pk.Model with
            | Some m -> escapeField (Model.toStableString m)
            | None -> "default"

        let att = escapeField (string pk.Attempt)

        let trig =
            match pk.TriggerMessageId with
            | Some mId -> escapeField (MessageId.value mId)
            | None -> "none"

        let payload = escapeField pk.PayloadHash
        sprintf "%s:%s:%s:%s:%s:%s:%s" sId tId purp mdl att trig payload
