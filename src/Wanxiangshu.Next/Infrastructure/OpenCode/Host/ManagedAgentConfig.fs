namespace Wanxiangshu.Next.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Tools

/// 0.5.0 config gate: validate Host-final opencode.json agent inventory.
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
                    let mutable bindings = Map.empty
                    let mutable firstError: ConfigGateError option = None

                    for name in ManagedAgent.requiredNames do
                        if firstError.IsNone then
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
                        let mutable pairError: ConfigGateError option = None

                        for role in ManagedAgent.allRoles do
                            if pairError.IsNone then
                                let fastName = ManagedAgent.nameOf AgentTier.Fast role
                                let deepName = ManagedAgent.nameOf AgentTier.Deep role

                                match Map.tryFind fastName bindings, Map.tryFind deepName bindings with
                                | Some fast, Some deep when fast.Model = deep.Model ->
                                    pairError <- Some(DuplicatePairModel(fastName, deepName, fast.Model))
                                | _ -> ()

                        match pairError with
                        | Some err -> Error(formatError err)
                        | None -> Ok { Bindings = bindings }

    /// Apply Wanxiangshu-owned non-model fields onto Host agent entries.
    /// Never creates missing agents, never writes/overwrites model.
    let applyOwnedFields (config: obj) (inventory: ManagedAgentInventory) : unit =
        if isNull config then
            ()
        else
            let agents = config?agent

            if isNull agents then
                ()
            else
                for KeyValue(name, binding) in inventory.Bindings do
                    match agentEntry agents name with
                    | None -> ()
                    | Some entry ->
                        let role = binding.Agent.Role

                        let owned =
                            match role with
                            | Role.Manager -> StaticTools.managerAgentConfig ()
                            | Role.Orchestrator -> StaticTools.orchestratorAgentConfig ()
                            | Role.Coder -> StaticTools.coderAgentConfig ()
                            | Role.Inspector -> StaticTools.inspectorAgentConfig ()
                            | Role.DevOps -> StaticTools.devopsAgentConfig ()
                            | Role.Browser -> StaticTools.browserAgentConfig ()
                            | Role.Meditator -> StaticTools.meditatorAgentConfig ()
                            | Role.Reviewer -> StaticTools.reviewerAgentConfig ()
                            | Role.Blogger -> StaticTools.bloggerAgentConfig ()
                            | Role.Executor -> StaticTools.executorAgentConfig ()

                        // Copy Wanxiangshu-owned keys without touching model.
                        entry?mode <- owned?mode
                        entry?permission <- owned?permission

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

    let private loggedSource = ref false

    let configureFromHostConfig (config: obj) : Result<ManagedAgentInventory, string> =
        match validate config with
        | Error err -> Error err
        | Ok inventory ->
            applyOwnedFields config inventory

            if not loggedSource.Value then
                loggedSource.Value <- true
                printfn "Model configuration source: OpenCode config.agent"
                printfn "Legacy model environment variables are ignored."

            Ok inventory
