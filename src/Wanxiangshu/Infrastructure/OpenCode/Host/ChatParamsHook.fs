namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Journal
open Wanxiangshu.Kernel.Identity

/// Pin the provider request to the bound model of the *request* agent.
///
/// OpenCode may otherwise resolve an agent-less or history-inferred request to
/// the default build / Fast model. That is an unjustified Deep → Fast
/// downgrade: Fallback and Strength already change the Agent name when a tier
/// switch is intended, so this hook follows the request agent (or, if the
/// Host omitted it, the Authority Root SelectedAgent) and never invents Fast.
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

    let private selectedAgent (journal: AgentJournal option) (sessionId: SessionId option) =
        match journal, sessionId with
        | Some durable, Some sid ->
            PromptAuthorityLedger.activeProfile sid (AgentJournal.snapshot durable).AgentProjections
            |> Option.map (fun profile -> profile.SelectedAgent)
        | _ -> None

    let create
        (journal: AgentJournal option)
        (inventoryOf: unit -> ManagedAgentConfig.ManagedAgentInventory)
        : obj =
        box (fun (inputObj: obj) (outputObj: obj) ->
            if isNull outputObj then
                ()
            else
                let requestAgent =
                    agentOf inputObj
                    |> Option.orElseWith (fun () -> agentOf outputObj)
                    |> Option.orElseWith (fun () ->
                        selectedAgent
                            journal
                            (sessionIdOf inputObj |> Option.orElseWith (fun () -> sessionIdOf outputObj)))

                match requestAgent with
                | None -> ()
                | Some agent ->
                    let current =
                        currentModel outputObj
                        |> Option.orElseWith (fun () -> currentModel inputObj)

                    let inventory = inventoryOf ()

                    match ManagedAgentConfig.tryOpencodeModel inventory agent current with
                    | Some model -> outputObj?model <- box model
                    | None ->
                        // Bare model ids cannot invent a provider (tryOpencodeModel).
                        // Still pin the bound id as a string so Host cannot keep a
                        // Fast default when the request agent is Deep.
                        match Map.tryFind (agent.Trim()) inventory.Bindings with
                        | Some binding when not (String.IsNullOrWhiteSpace binding.Model) ->
                            outputObj?model <- box (binding.Model.Trim())
                        | _ -> ())
