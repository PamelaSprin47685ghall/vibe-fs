namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
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

    let private nonNullObj (value: obj) : obj option =
        if isNull value then None else Some value

    let private modelFromProviderFields (other: obj) : string option =
        match other?providerID, other?modelID with
        | p, m when not (isNull p) && not (isNull m) -> Some(sprintf "%s/%s" (string p) (string m))
        | _ -> None

    let private modelTextFromJsObject (other: obj) : string option =
        let asString = other?ToString ()

        if isNull asString then
            None
        elif
            String.IsNullOrWhiteSpace(string asString)
            || string asString = "[object Object]"
        then
            modelFromProviderFields other
        else
            Some(string asString)

    let private coerceModelValue (model: obj) : string option =
        match unbox<obj> model with
        | :? string as s -> Some s
        | other when not (isNull other) -> modelTextFromJsObject other
        | _ -> None

    let private readModel (agentObj: obj) : string option =
        agentObj
        |> nonNullObj
        |> Option.bind (fun agent -> agent?model |> nonNullObj)
        |> Option.bind coerceModelValue

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

    let private requireAgents (config: obj) : Result<obj, ConfigGateError> =
        if isNull config then Error MissingAgentMap
        elif isNull (config?agent) then Error MissingAgentMap
        else Ok(config?agent)

    let private rejectLegacy (agents: obj) : Result<unit, ConfigGateError> =
        match
            ManagedAgentCatalog.legacyAgentNames
            |> Seq.tryFind (fun name -> agentEntry agents name |> Option.isSome)
        with
        | Some legacy -> Error(LegacyAgentPresent legacy)
        | None -> Ok()

    let private requireNonEmptyModel (name: string) (model: string) : Result<string, ConfigGateError> =
        result {
            do! Result.requireTrue (EmptyModel name) (not (String.IsNullOrWhiteSpace model))
            return model
        }

    let private validateBookkeeperPresence (agents: obj) (name: string) : Result<unit, ConfigGateError> =
        result {
            let! entry = agentEntry agents name |> Result.requireSome (MissingManagedAgent name)
            let! model = readModel entry |> Result.requireSome (MissingModel name)
            do! requireNonEmptyModel name model |> Result.map ignore
        }

    let private validateRoleBinding
        (agents: obj)
        (name: string)
        : Result<string * ManagedAgentBinding, ConfigGateError> =
        result {
            let! managed =
                ManagedAgent.tryParse name
                |> Result.requireSome (InvalidManagedAgent(name, "failed to parse required name"))

            let! entry = agentEntry agents name |> Result.requireSome (MissingManagedAgent name)
            let! model = readModel entry |> Result.requireSome (MissingModel name)
            let! nonEmpty = requireNonEmptyModel name model

            return
                name,
                { Agent = managed
                  Model = nonEmpty.Trim() }
        }

    let private validateRequiredName
        (agents: obj)
        (name: string)
        : Result<(string * ManagedAgentBinding) option, ConfigGateError> =
        if ManagedAgentCatalog.isBookkeeperName name then
            validateBookkeeperPresence agents name |> Result.map (fun () -> None)
        else
            validateRoleBinding agents name |> Result.map Some

    let private collectBindings (agents: obj) : Result<Map<string, ManagedAgentBinding>, ConfigGateError> =
        result {
            let! entries = ManagedAgent.requiredNames |> List.traverseResultM (validateRequiredName agents)

            return entries |> List.choose id |> Map.ofList
        }

    let private rejectDuplicateRolePair
        (bindings: Map<string, ManagedAgentBinding>)
        (role: Role)
        : Result<unit, ConfigGateError> =
        let fastName = ManagedAgent.nameOf AgentTier.Fast role
        let deepName = ManagedAgent.nameOf AgentTier.Deep role

        match Map.tryFind fastName bindings, Map.tryFind deepName bindings with
        | Some fast, Some deep when fast.Model = deep.Model -> Error(DuplicatePairModel(fastName, deepName, fast.Model))
        | _ -> Ok()

    let private validateRolePairs (bindings: Map<string, ManagedAgentBinding>) : Result<unit, ConfigGateError> =
        ManagedAgent.allRoles
        |> List.traverseResultM (rejectDuplicateRolePair bindings)
        |> Result.map ignore

    let private readEntryModel (agents: obj) (name: string) : string option =
        agentEntry agents name |> Option.bind readModel

    let private rejectDuplicateTrimmedModels
        (fastName: string)
        (deepName: string)
        (models: (string * string) option)
        : Result<unit, ConfigGateError> =
        match models with
        | Some(fastModel, deepModel) when
            not (String.IsNullOrWhiteSpace fastModel)
            && not (String.IsNullOrWhiteSpace deepModel)
            && fastModel.Trim() = deepModel.Trim()
            ->
            Error(DuplicatePairModel(fastName, deepName, fastModel.Trim()))
        | _ -> Ok()

    let private validateBookkeeperPair (agents: obj) : Result<unit, ConfigGateError> =
        let fastBk = ManagedAgentCatalog.bookkeeperNameOf AgentTier.Fast
        let deepBk = ManagedAgentCatalog.bookkeeperNameOf AgentTier.Deep

        match readEntryModel agents fastBk, readEntryModel agents deepBk with
        | Some fastModel, Some deepModel -> rejectDuplicateTrimmedModels fastBk deepBk (Some(fastModel, deepModel))
        | _ -> Ok()

    let validate (config: obj) : Result<ManagedAgentInventory, string> =
        result {
            let! agents = requireAgents config
            do! rejectLegacy agents
            let! bindings = collectBindings agents
            do! validateRolePairs bindings
            do! validateBookkeeperPair agents
            return { Bindings = bindings }
        }
        |> Result.mapError formatError

    let private assignOwnedFields (entry: obj) (owned: obj) : unit =
        entry?mode <- owned?mode
        entry?permission <- owned?permission

        if not (isNull owned?hidden) then
            entry?hidden <- owned?hidden

        if not (isNull owned?prompt) then
            entry?prompt <- owned?prompt

    let private ownedConfigForRole (role: Role) : obj =
        let prompts = RuntimeResources.current().Prompts

        match role with
        | Role.Manager -> StaticTools.managerAgentConfig (Some prompts.ManagerSystemPrompt)
        | Role.Orchestrator -> StaticTools.orchestratorAgentConfig (Some prompts.OrchestratorSystemPrompt)
        | Role.Coder -> StaticTools.coderAgentConfig (Some prompts.CoderSystemPrompt)
        | Role.Inspector -> StaticTools.inspectorAgentConfig (Some prompts.InspectorSystemPrompt)
        | Role.DevOps -> StaticTools.devopsAgentConfig (Some prompts.DevopsSystemPrompt)
        | Role.Browser -> StaticTools.browserAgentConfig (Some prompts.BrowserSystemPrompt)
        | Role.Inquiry -> StaticTools.inquiryAgentConfig (Some prompts.InquirySystemPrompt)
        | Role.Reviewer -> StaticTools.reviewerAgentConfig (Some prompts.ReviewerSystemPrompt)
        | Role.Blogger -> StaticTools.bloggerAgentConfig prompts.BloggerSystemPrompt
        | Role.Distiller -> StaticTools.distillerAgentConfig prompts.DistillerSystemPrompt

    let private applyRoleBinding (entry: obj) (inventory: ManagedAgentInventory) (name: string) : unit =
        match Map.tryFind name inventory.Bindings with
        | None -> ()
        | Some binding -> assignOwnedFields entry (ownedConfigForRole binding.Agent.Role)

    let private applyAgentOwnedFields (agents: obj) (inventory: ManagedAgentInventory) (name: string) : unit =
        match agentEntry agents name, ManagedAgentCatalog.isBookkeeperName name with
        | None, _ -> ()
        | Some entry, true ->
            assignOwnedFields entry (StaticTools.bookkeeperAgentConfig (PromptResources.loadBookkeeperSystem ()))
        | Some entry, false -> applyRoleBinding entry inventory name

    let private applyPresentAgent
        (agents: obj)
        (keys: string array)
        (inventory: ManagedAgentInventory)
        (name: string)
        : unit =
        if Array.contains name keys then
            applyAgentOwnedFields agents inventory name

    let private parseNonNegativeInt (raw: string) : int option =
        match System.Int32.TryParse raw with
        | true, n when n >= 0 -> Some n
        | _ -> None

    let private ensureExperimental (config: obj) : obj =
        if isNull config?experimental then
            let created: obj = createEmpty
            config?experimental <- created
            created
        else
            config?experimental

    let private applyChatMaxRetries (config: obj) : unit =
        match System.Environment.GetEnvironmentVariable("WANXIANGSHU_CHAT_MAX_RETRIES") with
        | null
        | "" -> ()
        | raw ->
            parseNonNegativeInt raw
            |> Option.iter (fun n -> (ensureExperimental config)?chatMaxRetries <- n)

    let private applyAgentsOwnedFields (config: obj) (agents: obj) (inventory: ManagedAgentInventory) : unit =
        let keys: string array = emitJsExpr agents "Object.keys($0)"

        for name in ManagedAgent.requiredNames do
            applyPresentAgent agents keys inventory name

        config?compaction <- createObj [ "auto" ==> false ]
        applyChatMaxRetries config

    /// Apply Wanxiangshu-owned non-model fields onto Host agent entries.
    /// Never creates missing agents, never writes/overwrites model.
    /// Walks the full required 22-name inventory (not just validated bindings):
    /// AGENT-007's first layer is fail-closed, so a validation failure elsewhere
    /// in the config must not silently drop every permission write. Missing
    /// agents stay untouched (no invented agents).
    let applyOwnedFields (config: obj) (inventory: ManagedAgentInventory) : unit =
        StealthBrowserMcpConfig.apply config (StealthBrowserMcpConfig.launchFromEnvironment ())
        SphinxMcpConfig.apply config (SphinxMcpConfig.launchFromEnvironment ())

        if isNull config then ()
        elif isNull (config?agent) then ()
        else applyAgentsOwnedFields config (config?agent) inventory

    let private liveInventory: ManagedAgentInventory option ref = ref None

    /// Best-effort bindings for the Error path: role knowledge only, no model
    /// validation (the model checks are what failed). Enough to write owned
    /// fields so AGENT-007's fail-closed first layer survives a gate error.
    let private tryRoleBinding (agents: obj) (name: string) : (string * ManagedAgentBinding) option =
        match agentEntry agents name, ManagedAgent.tryParse name with
        | Some _, Some managed -> Some(name, { Agent = managed; Model = "" })
        | _ -> None

    let private roleBindings (agents: obj) : Map<string, ManagedAgentBinding> =
        ManagedAgent.requiredNames |> List.choose (tryRoleBinding agents) |> Map.ofList

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

    let private inheritProviderModel (text: string) (current: OpencodeModel option) : OpencodeModel option =
        match current with
        | Some existing when not (String.IsNullOrWhiteSpace existing.providerID) ->
            Some { existing with modelID = text }
        | _ -> None

    let private modelFromBindingText (text: string) (current: OpencodeModel option) : OpencodeModel option =
        match text.IndexOf '/' with
        | index when index > 0 && index < text.Length - 1 ->
            Some
                { providerID = text.Substring(0, index)
                  modelID = text.Substring(index + 1)
                  variant = current |> Option.bind (fun model -> model.variant) }
        | _ -> inheritProviderModel text current

    let private bindingModel
        (inventory: ManagedAgentInventory)
        (agent: string)
        (current: OpencodeModel option)
        : OpencodeModel option =
        match Map.tryFind (agent.Trim()) inventory.Bindings with
        | Some binding when not (String.IsNullOrWhiteSpace binding.Model) ->
            modelFromBindingText (binding.Model.Trim()) current
        | _ -> None

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
            bindingModel inventory agent current

    /// PROMPT-006 Host resolution: Dispatcher sends `Model = None`; the transport
    /// binds `config.agent[effectiveAgent].model` before prompt_async. OpenCode
    /// otherwise treats an agent-less / history-inferred request as the default
    /// Fast model and overwrites a Deep child mid-conversation.
    let tryBoundModel (agent: string) : OpencodeModel option =
        match liveInventory.Value with
        | Some inventory -> tryOpencodeModel inventory agent None
        | None -> None
