namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Resources
open Wanxiangshu.Foundation

/// managed agent config gate: validate Host-final opencode.json agent inventory.
/// Does not invent missing agents, fill models, or read model env vars.
module ManagedAgentConfig =

    type ManagedAgentBinding = { Agent: ManagedAgent; Model: string }

    type ManagedAgentInventory =
        { Bindings: Map<string, ManagedAgentBinding> }

    type ConfigGateError =
        | MissingAgentMap
        | MissingManagedAgent of string
        | MissingModel of string
        | EmptyModel of string
        | DuplicatePairModel of fast: string * deep: string * model: string
        | LegacyAgentPresent of string
        | InvalidManagedAgent of string * detail: string

    let private readModel (agentObj: obj) : string option =
        if isNull agentObj then
            None
        else
            let model = agentObj?model

            if isNull model then
                None
            else
                match unbox<obj> model with
                | :? string as s -> Some s
                | other when not (isNull other) ->
                    // Host may present model as object; prefer provider/model string fields.
                    let asString = other?ToString ()

                    if not (isNull asString) then
                        let text = string asString

                        if String.IsNullOrWhiteSpace text || text = "[object Object]" then
                            let provider = other?providerID
                            let modelId = other?modelID

                            match provider, modelId with
                            | p, m when not (isNull p) && not (isNull m) -> Some(sprintf "%s/%s" (string p) (string m))
                            | _ -> None
                        else
                            Some text
                    else
                        None
                | _ -> None

    let private agentEntry (agents: obj) (name: string) : obj option =
        if isNull agents then
            None
        else
            let value = agents?(name)
            if isNull value then None else Some value

    let private formatError (err: ConfigGateError) : string =
        match err with
        | MissingAgentMap -> "Managed agents require config.agent from the Host-final opencode.json."
        | MissingManagedAgent name -> sprintf "Missing required managed agent '%s' in opencode.json agent map." name
        | MissingModel name -> sprintf "Managed agent '%s' is missing a non-empty model binding." name
        | EmptyModel name -> sprintf "Managed agent '%s' has an empty model binding." name
        | DuplicatePairModel(fast, deep, model) ->
            sprintf
                "Managed agent pair %s/%s resolves to the same model.\nConfigure two distinct models or correct opencode.json.\nShared model: %s"
                fast
                deep
                model
        | LegacyAgentPresent name -> ManagedAgentCatalog.formatLegacyNameInConfig name
        | InvalidManagedAgent(name, detail) -> sprintf "Invalid managed agent '%s': %s" name detail

    let validate (config: obj) : Result<ManagedAgentInventory, string> =
        if isNull config then
            Error(formatError MissingAgentMap)
        else
            let agents = config?agent

            if isNull agents then
                Error(formatError MissingAgentMap)
            else
                let legacyHit =
                    ManagedAgentCatalog.legacyAgentNames
                    |> Seq.tryFind (fun name ->
                        match agentEntry agents name with
                        | Some _ -> true
                        | None -> false)

                match legacyHit with
                | Some legacy -> Error(formatError (LegacyAgentPresent legacy))
                | None ->
                    // DSL-MUTABLE: algorithm-scratch — config binding fold accumulator
                    let mutable bindings = Map.empty
                    // DSL-MUTABLE: algorithm-scratch — first validation error latch for fold
                    let mutable firstError: ConfigGateError option = None

                    for name in ManagedAgent.requiredNames do
                        if firstError.IsNone then
                            if ManagedAgentCatalog.isBookkeeperName name then
                                match agentEntry agents name with
                                | None -> firstError <- Some(MissingManagedAgent name)
                                | Some entry ->
                                    match readModel entry with
                                    | None -> firstError <- Some(MissingModel name)
                                    | Some model when String.IsNullOrWhiteSpace model ->
                                        firstError <- Some(EmptyModel name)
                                    | Some _ -> ()
                            else
                                match ManagedAgent.tryParse name with
                                | None -> firstError <- Some(InvalidManagedAgent(name, "failed to parse required name"))
                                | Some managed ->
                                    match agentEntry agents name with
                                    | None -> firstError <- Some(MissingManagedAgent name)
                                    | Some entry ->
                                        match readModel entry with
                                        | None -> firstError <- Some(MissingModel name)
                                        | Some model when String.IsNullOrWhiteSpace model ->
                                            firstError <- Some(EmptyModel name)
                                        | Some model ->
                                            bindings <-
                                                Map.add
                                                    name
                                                    { Agent = managed
                                                      Model = model.Trim() }
                                                    bindings

                    match firstError with
                    | Some err -> Error(formatError err)
                    | None ->
                        // DSL-MUTABLE: algorithm-scratch — pair-model validation error latch
                        let mutable pairError: ConfigGateError option = None

                        for role in ManagedAgent.allRoles do
                            if pairError.IsNone then
                                let fastName = ManagedAgent.nameOf AgentTier.Fast role
                                let deepName = ManagedAgent.nameOf AgentTier.Deep role

                                match Map.tryFind fastName bindings, Map.tryFind deepName bindings with
                                | Some fast, Some deep when fast.Model = deep.Model ->
                                    pairError <- Some(DuplicatePairModel(fastName, deepName, fast.Model))
                                | _ -> ()

                        if pairError.IsNone then
                            let fastBk = ManagedAgentCatalog.bookkeeperNameOf AgentTier.Fast
                            let deepBk = ManagedAgentCatalog.bookkeeperNameOf AgentTier.Deep

                            match agentEntry agents fastBk, agentEntry agents deepBk with
                            | Some fastEntry, Some deepEntry ->
                                match readModel fastEntry, readModel deepEntry with
                                | Some fastModel, Some deepModel when
                                    not (String.IsNullOrWhiteSpace fastModel)
                                    && not (String.IsNullOrWhiteSpace deepModel)
                                    && fastModel.Trim() = deepModel.Trim()
                                    ->
                                    pairError <- Some(DuplicatePairModel(fastBk, deepBk, fastModel.Trim()))
                                | _ -> ()
                            | _ -> ()

                        match pairError with
                        | Some err -> Error(formatError err)
                        | None -> Ok { Bindings = bindings }

    /// Apply Wanxiangshu-owned non-model fields onto Host agent entries.
    /// Never creates missing agents, never writes/overwrites model.
    /// Walks the full required 22-name inventory (not just validated bindings):
    /// AGENT-007's first layer is fail-closed, so a validation failure elsewhere
    /// in the config must not silently drop every permission write. Missing
    /// agents stay untouched (no invented agents).
    let applyOwnedFields (config: obj) (inventory: ManagedAgentInventory) : unit =
        StealthBrowserMcpConfig.apply config (StealthBrowserMcpConfig.launchFromEnvironment ())
        SphinxMcpConfig.apply config (SphinxMcpConfig.launchFromEnvironment ())

        if isNull config then
            ()
        else
            let agents = config?agent

            if isNull agents then
                ()
            else
                let keys: string array = emitJsExpr agents "Object.keys($0)"

                for name in ManagedAgent.requiredNames do
                    if Array.contains name keys then
                        match agentEntry agents name with
                        | None -> ()
                        | Some entry when ManagedAgentCatalog.isBookkeeperName name ->
                            let owned =
                                StaticTools.bookkeeperAgentConfig (PromptResources.loadBookkeeperSystem ())

                            entry?mode <- owned?mode
                            entry?permission <- owned?permission

                            if not (isNull owned?hidden) then
                                entry?hidden <- owned?hidden

                            if not (isNull owned?prompt) then
                                entry?prompt <- owned?prompt
                        | Some entry ->
                            match Map.tryFind name inventory.Bindings with
                            | None -> ()
                            | Some binding ->
                                let role = binding.Agent.Role
                                let prompts = RuntimeResources.current().Prompts

                                let owned =
                                    match role with
                                    | Role.Manager -> StaticTools.managerAgentConfig (Some prompts.ManagerSystemPrompt)
                                    | Role.Orchestrator ->
                                        StaticTools.orchestratorAgentConfig (Some prompts.OrchestratorSystemPrompt)
                                    | Role.Coder -> StaticTools.coderAgentConfig (Some prompts.CoderSystemPrompt)
                                    | Role.Inspector ->
                                        StaticTools.inspectorAgentConfig (Some prompts.InspectorSystemPrompt)
                                    | Role.DevOps -> StaticTools.devopsAgentConfig (Some prompts.DevopsSystemPrompt)
                                    | Role.Browser -> StaticTools.browserAgentConfig (Some prompts.BrowserSystemPrompt)
                                    | Role.Inquiry -> StaticTools.inquiryAgentConfig (Some prompts.InquirySystemPrompt)
                                    | Role.Reviewer ->
                                        StaticTools.reviewerAgentConfig (Some prompts.ReviewerSystemPrompt)
                                    | Role.Blogger -> StaticTools.bloggerAgentConfig prompts.BloggerSystemPrompt
                                    | Role.Distiller -> StaticTools.distillerAgentConfig prompts.DistillerSystemPrompt

                                entry?mode <- owned?mode
                                entry?permission <- owned?permission

                                if not (isNull owned?hidden) then
                                    entry?hidden <- owned?hidden

                                if not (isNull owned?prompt) then
                                    entry?prompt <- owned?prompt

                config?compaction <- createObj [ "auto" ==> false ]

                match System.Environment.GetEnvironmentVariable("WANXIANGSHU_CHAT_MAX_RETRIES") with
                | null
                | "" -> ()
                | raw ->
                    match System.Int32.TryParse raw with
                    | true, n when n >= 0 ->
                        let experimental =
                            if isNull config?experimental then
                                let created: obj = createEmpty
                                config?experimental <- created
                                created
                            else
                                config?experimental

                        experimental?chatMaxRetries <- n
                    | _ -> ()

    let private liveInventory: ManagedAgentInventory option ref = ref None

    /// Best-effort bindings for the Error path: role knowledge only, no model
    /// validation (the model checks are what failed). Enough to write owned
    /// fields so AGENT-007's fail-closed first layer survives a gate error.
    let private roleBindings (agents: obj) : Map<string, ManagedAgentBinding> =
        // DSL-MUTABLE: algorithm-scratch — best-effort role binding fold
        let mutable bindings = Map.empty

        for name in ManagedAgent.requiredNames do
            match agentEntry agents name, ManagedAgent.tryParse name with
            | Some _, Some managed -> bindings <- Map.add name { Agent = managed; Model = "" } bindings
            | _ -> ()

        bindings

    let configureFromHostConfig (config: obj) : Result<ManagedAgentInventory, string> =
        match validate config with
        | Error err ->
            // AGENT-007 fail-closed: a validation failure elsewhere in the config
            // (e.g. a duplicate model pair) must not silently drop every
            // permission/mode/prompt write. Apply what the config itself names;
            // the Host logs the gate error and keeps running.
            let agents = if isNull config then null else config?agent
            applyOwnedFields config { Bindings = if isNull agents then Map.empty else roleBindings agents }
            Error err
        | Ok inventory ->
            applyOwnedFields config inventory
            liveInventory.Value <- Some inventory

            Ok inventory

    /// Parse Host-final agent model binding into an OpencodeModel.
    ///
    /// `provider/modelID` is the ordinary opencode.json form. A bare model id
    /// keeps the current request's provider when one is already present, so a
    /// Host default of the cheap id can be overwritten without inventing a
    /// provider. Incomplete bindings (bare id, no current provider) are refused
    /// rather than filled with a Fast default.
    let tryOpencodeModel
        (inventory: ManagedAgentInventory)
        (agent: string)
        (current: OpencodeModel option)
        : OpencodeModel option =
        if String.IsNullOrWhiteSpace agent then
            None
        else
            match Map.tryFind (agent.Trim()) inventory.Bindings with
            | None -> None
            | Some binding when String.IsNullOrWhiteSpace binding.Model -> None
            | Some binding ->
                let text = binding.Model.Trim()

                match text.IndexOf '/' with
                | index when index > 0 && index < text.Length - 1 ->
                    Some
                        { providerID = text.Substring(0, index)
                          modelID = text.Substring(index + 1)
                          variant = current |> Option.bind (fun model -> model.variant) }
                | _ ->
                    match current with
                    | Some existing when not (String.IsNullOrWhiteSpace existing.providerID) ->
                        Some { existing with modelID = text }
                    | _ -> None

    /// PROMPT-006 Host resolution: Dispatcher sends `Model = None`; the transport
    /// binds `config.agent[effectiveAgent].model` before prompt_async. OpenCode
    /// otherwise treats an agent-less / history-inferred request as the default
    /// Fast model and overwrites a Deep child mid-conversation.
    let tryBoundModel (agent: string) : OpencodeModel option =
        match liveInventory.Value with
        | Some inventory -> tryOpencodeModel inventory agent None
        | None -> None
