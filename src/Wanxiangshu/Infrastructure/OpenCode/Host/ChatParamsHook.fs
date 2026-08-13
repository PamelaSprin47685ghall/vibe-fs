namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Journal
open Wanxiangshu.Kernel.Identity

/// Observe external root binding; compatibility-pin parented managed sessions.
///
/// PROMPT-006 binding authority lives at InjectedSessionPort.SendPrompt. For a
/// user-facing root this hook records the Host-resolved agent/model only when the
/// request did not originate from our own SendPrompt. Older Host builds that still
/// honor output.model also get the frozen child agent's model as an extra guard.
module ChatParamsHook =

    let private nonEmpty (value: string) =
        if String.IsNullOrWhiteSpace value then None else Some(value.Trim())

    let private readString (source: obj) (name: string) =
        if isNull source || isNull (source?(name)) then
            None
        else
            nonEmpty (string (source?(name)))

    let private agentOf (source: obj) =
        match readString source "agent" with
        | Some agent -> Some agent
        | None ->
            if isNull source || isNull source?message then
                None
            else
                readString source?message "agent"

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
            elif not (isNull model?providerID) && not (isNull model?modelID) then
                let variant =
                    if isNull model?variant then
                        None
                    else
                        nonEmpty (string model?variant)

                Some
                    { providerID = unbox<string> model?providerID
                      modelID = unbox<string> model?modelID
                      variant = variant }
            else
                None

    let private userMessageBinding (source: obj) =
        if isNull source || isNull source?message then
            None
        else
            match readString source?message "agent", currentModel source?message with
            | Some agent, Some model -> Some(agent, model)
            | _ -> None

    let private managedChildAgent (sessionId: SessionId option) =
        match sessionId with
        | None -> None
        | Some sid ->
            match
                SessionExecutionBinding.tryParent sid,
                SessionExecutionBinding.tryAgent sid
            with
            | Some _, Some agent -> nonEmpty agent
            | _ -> None

    let create
        (_journal: AgentJournal option)
        (inventoryOf: unit -> ManagedAgentConfig.ManagedAgentInventory)
        : obj =
        box (fun (inputObj: obj) (outputObj: obj) ->
            if isNull outputObj then
                ()
            else
                match sessionIdOf inputObj |> Option.orElseWith (fun () -> sessionIdOf outputObj) with
                | None -> ()
                | Some sessionId ->
                    match managedChildAgent (Some sessionId) with
                    | None ->
                        userMessageBinding inputObj
                        |> Option.iter (fun (agent, model) ->
                            SessionExecutionBinding.observeUserFacing sessionId agent model)
                    | Some agent ->
                        let current =
                            currentModel outputObj
                            |> Option.orElseWith (fun () -> currentModel inputObj)

                        let inventory = inventoryOf ()

                        match ManagedAgentConfig.tryOpencodeModel inventory agent current with
                        | Some model -> outputObj?model <- box model
                        | None ->
                            // Bare model ids cannot invent a provider (tryOpencodeModel).
                            // Still pin the bound id as a string for older Host builds;
                            // current Host ignores this field and SendPrompt remains law.
                            match Map.tryFind (agent.Trim()) inventory.Bindings with
                            | Some binding when not (String.IsNullOrWhiteSpace binding.Model) ->
                                outputObj?model <- box (binding.Model.Trim())
                            | _ -> ())
