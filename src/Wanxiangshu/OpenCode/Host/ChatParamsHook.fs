namespace Wanxiangshu.OpenCode

open System
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity

/// PROMPT-006 / EMR-009: chat.params is an observation barrier, not a routing
/// authority. chat.message / internal SendPrompt must already have established the
/// lease and projected model+variant before the provider reaches this hook.
module ChatParamsHook =

    let private textField (value: obj) (name: string) =
        if isNull value || isNull value?(name) then None
        else
            let text = string value?(name)
            if String.IsNullOrWhiteSpace text then None else Some(text.Trim())

    let private extractModel (input: obj) =
        let rawModel: obj = input?model
        let message: obj = input?message
        let messageModel: obj = if isNull message then null else message?model

        let provider =
            textField rawModel "providerID"
            |> Option.orElseWith (fun () -> textField messageModel "providerID")

        let modelId =
            textField rawModel "modelID"
            |> Option.orElseWith (fun () -> textField messageModel "modelID")

        let variant =
            textField messageModel "variant"
            |> Option.orElseWith (fun () -> textField rawModel "variant")

        match provider, modelId with
        | Some providerID, Some modelID ->
            Some
                { providerID = providerID
                  modelID = modelID
                  variant = variant }
        | _ -> None

    let private currentModel (input: obj) =
        if isNull input then None else extractModel input

    let private isManagedName (agent: string) =
        ManagedAgent.requiredNames |> List.contains agent

    let private checkObservedProvider sessionId agent model =
        match SessionExecutionBinding.validateObservedProvider sessionId agent model with
        | Ok true -> ()
        | Ok false ->
            invalidOp (
                sprintf
                    "PROMPT-006: managed provider run '%s' was not recognized as a bound session"
                    agent
            )
        | Error error -> invalidOp error

    let private validateModel (sessionId: SessionId) (agent: string) (input: obj) =
        match currentModel input with
        | None ->
            invalidOp (
                sprintf
                    "PROMPT-006: managed provider run '%s' has no observable provider/model binding"
                    agent
            )
        | Some model -> checkObservedProvider sessionId agent model

    let private trySessionAndAgent (input: obj) =
        if isNull input || isNull input?sessionID || isNull input?agent then None
        else
            let sessionText = string input?sessionID
            let agent = (string input?agent).Trim()
            if String.IsNullOrWhiteSpace sessionText || not (isManagedName agent) then None
            else Some(SessionId.create (sessionText.Trim()), agent)

    let private handleInput (input: obj) =
        match trySessionAndAgent input with
        | Some(sessionId, agent) -> validateModel sessionId agent input
        | None -> ()

    let create () : obj =
        box (fun (input: obj) (_output: obj) -> handleInput input)
