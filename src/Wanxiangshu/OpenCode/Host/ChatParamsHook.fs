namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity

/// Observe real user binding and reject provider-side drift.
///
/// `input.message.agent/model` is the request binding. Top-level `input.agent/model`
/// may describe title/compaction and is never user authority. Internal sends install
/// a scoped expected binding before Host dispatch; parented sessions fall back to
/// their frozen base binding. Any mismatch aborts the provider run.
module ChatParamsHook =

    let private nonEmpty (value: string) =
        if String.IsNullOrWhiteSpace value then
            None
        else
            Some(value.Trim())

    let private readString (source: obj) (name: string) =
        if isNull source || isNull (source?(name)) then
            None
        else
            nonEmpty (string (source?(name)))

    let private sessionIdOf (source: obj) =
        readString source "sessionID"
        |> Option.orElseWith (fun () -> readString source "sessionId")
        |> Option.orElseWith (fun () -> readString source "session")
        |> Option.map SessionId.create

    let private currentModel (source: obj) : OpencodeModel option =
        if isNull source || isNull source?model then
            None
        else
            let model = source?model

            if emitJsExpr model "typeof $0 === 'string'" then
                let text = string model

                match text.IndexOf '/' with
                | index when index > 0 && index < text.Length - 1 ->
                    Some
                        { providerID = text.Substring(0, index)
                          modelID = text.Substring(index + 1)
                          variant = None }
                | _ -> None
            else
                let providerId =
                    readString model "providerID"
                    |> Option.orElseWith (fun () -> readString model "providerId")

                let modelId =
                    readString model "modelID"
                    |> Option.orElseWith (fun () -> readString model "modelId")
                    |> Option.orElseWith (fun () -> readString model "id")

                let variant = readString model "variant"

                match providerId, modelId with
                | Some p, Some m ->
                    Some
                        { providerID = p
                          modelID = m
                          variant = variant }
                | _ -> None

    let private userMessageBinding (source: obj) =
        if isNull source || isNull source?message then
            None
        else
            match readString source?message "agent", currentModel source?message with
            | Some agent, Some model -> Some(agent, model)
            | _ -> None

    let create () : obj =
        box (fun (inputObj: obj) (_outputObj: obj) ->
            match sessionIdOf inputObj with
            | None -> ()
            | Some sessionId ->
                match userMessageBinding inputObj with
                | None when SessionExecutionBinding.requiresProviderBindingProof sessionId ->
                    invalidOp "PROMPT-006: chat.params input.message has no agent/model binding"
                | None -> ()
                | Some(agent, model) ->
                    match SessionExecutionBinding.validateObservedProvider sessionId agent model with
                    | Error error -> invalidOp error
                    | Ok true -> ()
                    | Ok false -> SessionExecutionBinding.observeUserFacing sessionId agent model)
