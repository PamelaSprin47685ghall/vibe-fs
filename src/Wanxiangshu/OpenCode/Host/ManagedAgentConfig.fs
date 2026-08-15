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

/// Managed agent Host-config gate. This owns only catalog presence and Wanxiangshu-owned
/// non-model fields. Physical model routing is exclusively owned by ModelRouting.
module ManagedAgentConfig =

    type ManagedAgentBinding = { Agent: ManagedAgent }

    type ManagedAgentInventory =
        { Bindings: Map<string, ManagedAgentBinding> }

    type ConfigGateError =
        | MissingAgentMap
        | MissingManagedAgent of string
        | LegacyAgentPresent of string
        | InvalidManagedAgent of string * detail: string

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

    let private validateBookkeeperPresence (agents: obj) (name: string) : Result<unit, ConfigGateError> =
        agentEntry agents name
        |> Result.requireSome (MissingManagedAgent name)
        |> Result.map ignore

    let private validateRoleBinding
        (agents: obj)
        (name: string)
        : Result<string * ManagedAgentBinding, ConfigGateError> =
        result {
            let! managed =
                ManagedAgent.tryParse name
                |> Result.requireSome (InvalidManagedAgent(name, "failed to parse required name"))

            do!
                agentEntry agents name
                |> Result.requireSome (MissingManagedAgent name)
                |> Result.map ignore

            return name, { Agent = managed }
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
        ManagedAgent.requiredNames
        |> List.traverseResultM (validateRequiredName agents)
        |> Result.map (List.choose id >> Map.ofList)

    let validate (config: obj) : Result<ManagedAgentInventory, string> =
        result {
            let! agents = requireAgents config
            do! rejectLegacy agents
            let! bindings = collectBindings agents
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

    /// Best-effort bindings for the Error path: role knowledge only. Enough to write
    /// owned fields so AGENT-007's fail-closed first layer survives a catalog error.
    let private tryRoleBinding (agents: obj) (name: string) : (string * ManagedAgentBinding) option =
        match agentEntry agents name, ManagedAgent.tryParse name with
        | Some _, Some managed -> Some(name, { Agent = managed })
        | _ -> None

    let private roleBindings (agents: obj) : Map<string, ManagedAgentBinding> =
        ManagedAgent.requiredNames |> List.choose (tryRoleBinding agents) |> Map.ofList

    let configureFromHostConfig (config: obj) : Result<ManagedAgentInventory, string> =
        match validate config with
        | Error err ->
            // AGENT-007 fail-closed: a catalog validation failure must not silently
            // drop every permission/mode/prompt write. Apply what the config names.
            let agents = if isNull config then null else config?agent
            applyOwnedFields config { Bindings = if isNull agents then Map.empty else roleBindings agents }
            Error err
        | Ok inventory ->
            applyOwnedFields config inventory
            Ok inventory
