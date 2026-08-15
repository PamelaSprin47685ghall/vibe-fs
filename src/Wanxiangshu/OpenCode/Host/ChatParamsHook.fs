namespace Wanxiangshu.OpenCode

open System
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity

/// PROMPT-006 / EMR-009: chat.params is an observation barrier, not a routing
/// authority. chat.message / internal SendPrompt must already have established the
/// lease and projected model+variant before the provider reaches this hook.
module ChatParamsHook =

    let private textField (value: obj) (name: string) =
        if isNull value then
            None
        else
            let field: obj = value?(name)

            if isNull field then
                None
            else
                let text = string field

                if String.IsNullOrWhiteSpace text then
                    None
                else
                    Some(text.Trim())

    let private currentModel (input: obj) =
        if isNull input then
            None
        else
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

    let private isManagedName (agent: string) =
        ManagedAgent.requiredNames |> List.contains agent

    let create () : obj =
        box (fun (input: obj) (_output: obj) ->
            if not (isNull input) && not (isNull input?sessionID) && not (isNull input?agent) then
                let sessionText = string input?sessionID
                let agent = string input?agent |> fun value -> value.Trim()

                if not (String.IsNullOrWhiteSpace sessionText) && isManagedName agent then
                    let sessionId = SessionId.create (sessionText.Trim())

                    match currentModel input with
                    | None ->
                        invalidOp (
                            sprintf
                                "PROMPT-006: managed provider run '%s' has no observable provider/model binding"
                                agent
                        )
                    | Some model ->
                        match SessionExecutionBinding.validateObservedProvider sessionId agent model with
                        | Ok true -> ()
                        | Ok false ->
                            invalidOp (
                                sprintf
                                    "PROMPT-006: managed provider run '%s' was not recognized as a bound session"
                                    agent
                            )
                        | Error error -> invalidOp error)
