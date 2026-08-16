namespace Wanxiangshu.OpenCode

open System
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity

/// JS-native semantic boundary for PROMPT-006 execution binding.
/// Session ids, agent names, physical ids, and model records are plain JS
/// values; SessionExecutionBinding owns the process-local state and typed IDs.
module SessionBindingSurface =

    let private optionalText (value: obj) : string option =
        if isNull value then None
        else
            let text = string value
            if String.IsNullOrWhiteSpace text then None else Some text

    let private modelOf (value: obj) : OpencodeModel option =
        match optionalText value with
        | None -> None
        | Some _ ->
            Some
                { providerID = string value?providerID
                  modelID = string value?modelID
                  variant = optionalText value?variant }

    let private modelFreeOptions (agent: string) (overrideBinding: bool) (model: obj) : OpenCodePromptOptions =
        { Model = modelOf model
          Agent = optionalText agent
          Directory = None
          Metadata = None
          Tools = None
          BindingIntent =
            if overrideBinding then
                SessionBindingIntent.ExplicitExecutionOverride
            else
                SessionBindingIntent.Preserve }

    let private resultObject (project: 'a -> obj) (result: Result<'a, string>) : obj =
        match result with
        | Ok value -> box {| ok = true; value = project value; error = "" |}
        | Error message -> box {| ok = false; value = null; error = message |}

    let bindChild (parentId: string) (childId: string) (agent: string) : obj =
        try
            SessionExecutionBinding.bind
                (SessionId.create parentId)
                (SessionId.create childId)
                (optionalText agent)

            box {| ok = true; error = "" |}
        with ex ->
            box {| ok = false; error = ex.Message |}

    let observeUserFacingAgent (sessionId: string) (agent: string) : unit =
        SessionExecutionBinding.observeUserFacingAgent (SessionId.create sessionId) agent

    let tryAgent (sessionId: string) : string =
        SessionExecutionBinding.tryAgent (SessionId.create sessionId) |> Option.defaultValue ""

    let prepareManaged (sessionId: string) (agent: string) (overrideBinding: bool) (model: obj) : obj =
        SessionExecutionBinding.prepareManagedPrompt
            (SessionId.create sessionId)
            (modelFreeOptions agent overrideBinding model)
        |> resultObject (fun options ->
            box
                {| agent = options.Agent |> Option.defaultValue ""
                   modelProvided = options.Model.IsSome |})

    let prepareUserFacing (sessionId: string) (agent: string) (overrideBinding: bool) (model: obj) : obj =
        SessionExecutionBinding.prepareUserFacingPrompt
            (SessionId.create sessionId)
            (modelFreeOptions agent overrideBinding model)
        |> resultObject (fun options ->
            box
                {| agent = options.Agent |> Option.defaultValue ""
                   modelProvided = options.Model.IsSome |})

    let acceptPromptExecution
        (sessionId: string)
        (promptKey: string)
        (physicalUserMessageId: string)
        (agent: string)
        (model: obj)
        : unit =
        match modelOf model with
        | Some selected ->
            SessionExecutionBinding.acceptPromptExecution
                (SessionId.create sessionId)
                (PromptKey.create promptKey)
                (PhysicalUserMessageId.create physicalUserMessageId)
                agent
                selected
        | None -> invalidArg "model" "PROMPT-006 requires a provider model"

    let beginProviderAttempt (sessionId: string) (physicalUserMessageId: string) (promptKey: string) : obj =
        let physical =
            if String.IsNullOrWhiteSpace physicalUserMessageId then
                None
            else
                Some(PhysicalUserMessageId.create physicalUserMessageId)

        let prompt =
            if String.IsNullOrWhiteSpace promptKey then None else Some(PromptKey.create promptKey)

        SessionExecutionBinding.beginProviderAttempt (SessionId.create sessionId) physical prompt
        |> resultObject (fun _ -> box true)

    let validateObservedProvider (sessionId: string) (agent: string) (model: obj) : obj =
        match modelOf model with
        | None -> box {| ok = false; value = false; error = "PROMPT-006 requires a provider model" |}
        | Some selected ->
            SessionExecutionBinding.validateObservedProvider (SessionId.create sessionId) agent selected
            |> resultObject box

    let drop (sessionId: string) : unit =
        SessionExecutionBinding.drop (SessionId.create sessionId)
