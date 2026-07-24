namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools

type SpikePluginConfig =
    { Directory: string
      Port: IOpenCodePort option }

module SpikePlugin =

    module NodePath =
        [<Import("join", "node:path")>]
        let join (a: string, b: string) : string = jsNative

    [<Import("pid", "node:process")>]
    let private processId: int = jsNative

    [<Emit("import('@opencode-ai/plugin/tool')")>]
    let private importToolModule () : Task<obj> = jsNative

    [<Emit("$0.schema.string()")>]
    let private stringSchema (tool: obj) : obj = jsNative

    [<Emit("$0($1)")>]
    let private applyTool (factory: obj) (definition: obj) : obj = jsNative

    [<Emit("(args, context) => $0(args)(context)")>]
    let private uncurriedExecute (fn: obj) : obj = jsNative

    [<Emit("Math.random().toString(36).slice(2, 8)")>]
    let private newAgentId () : string = jsNative

    [<Emit("JSON.stringify($0)")>]
    let private stringify (value: obj) : string = jsNative

    let createSpikeHost (portOpt: IOpenCodePort option) =
        let eventPort = Events.DeterministicEventPort() :> IEventObservationPort
        let sessionPort = InjectedSessionPort(portOpt, eventPort) :> ISessionHostPort
        eventPort, sessionPort

    let private hasHostEventCapability (input: obj) =
        if isNull input then
            false
        else
            let client = input?client
            not (isNull input?events) || (not (isNull client) && not (isNull client?events))

    let private createHost
        (input: obj)
        (portOpt: IOpenCodePort option)
        : Result<IEventObservationPort * ISessionHostPort * IDisposable option * (obj -> unit) option, string> =
        if hasHostEventCapability input then
            let hostEventPort = Events.HostEventPort()

            match Events.trySubscribeHostEvents input hostEventPort with
            | Error err -> Error err
            | Ok subscription ->
                let eventPort = hostEventPort :> IEventObservationPort
                let sessionPort = InjectedSessionPort(portOpt, eventPort) :> ISessionHostPort
                Ok(eventPort, sessionPort, subscription, Some(fun raw -> hostEventPort.Observe raw))
        else
            let hostEventPort = Events.HostEventPort()
            let eventPort = hostEventPort :> IEventObservationPort
            let sessionPort = InjectedSessionPort(portOpt, eventPort) :> ISessionHostPort
            Ok(eventPort, sessionPort, None, Some(fun raw -> hostEventPort.Observe raw))

    let private workspaceDirectory (input: obj) : string option =
        if isNull input || isNull input?directory then
            None
        else
            let directory = unbox<string> input?directory

            if String.IsNullOrWhiteSpace directory then
                None
            else
                Some directory

    let private createJournal (input: obj) : AgentJournal option =
        match workspaceDirectory input with
        | None -> None
        | Some workspace ->
            let directory =
                NodePath.join (NodePath.join (workspace, ".wanxiangshu-next"), "runtimes")

            let boot = Boot.boot directory
            let runtimeId = RuntimeId.create (Guid.NewGuid().ToString("N").Substring(0, 12))
            Some(AgentJournal.createFromBoot directory runtimeId processId DateTimeOffset.UtcNow boot)

    let private configureManager (config: obj) =
        if not (isNull config) then
            let agents =
                if isNull config?agent then
                    let created = createObj []
                    config?agent <- created
                    created
                else
                    config?agent

            let managerConfig = StaticTools.managerAgentConfig ()
            agents?manager <- managerConfig
            agents?build <- managerConfig
            agents?plan <- managerConfig
            agents?coder <- StaticTools.coderAgentConfig ()
            let toollessConfig = StaticTools.toollessAgentConfig ()
            agents?blogger <- toollessConfig
            agents?executor <- toollessConfig
            agents?inspector <- StaticTools.inspectorAgentConfig ()
            agents?browser <- toollessConfig
            agents?meditator <- toollessConfig
            agents?reviewer <- toollessConfig

    let private toolHooks (toolModule: obj) (sessionPort: ISessionHostPort) (journal: AgentJournal option) : obj =
        let factory = toolModule?tool
        let runtimes = Dictionary<string, HostForkRuntime>()
        let gate = obj ()

        let runtimeFor (context: obj) =
            let sessionID =
                if isNull context || isNull context?sessionID then
                    ""
                else
                    unbox<string> context?sessionID

            if String.IsNullOrWhiteSpace sessionID then
                Error "Missing sessionID"
            else
                Ok(
                    lock gate (fun () ->
                        match runtimes.TryGetValue sessionID with
                        | true, runtime -> runtime
                        | false, _ ->
                            let runtime =
                                HostForkRuntime(SessionId.create sessionID, sessionPort, ?journal = journal)

                            runtimes.[sessionID] <- runtime
                            runtime)
                )

        let textArg (args: obj) (name: string) =
            if isNull args || isNull args?(name) then
                ""
            else
                unbox<string> args?(name)

        let forkExecute (args: obj) (context: obj) =
            task {
                match runtimeFor context with
                | Error err -> return box (stringify (createObj [ "error", box err ]))
                | Ok runtime ->
                    let agent = textArg args "agent"
                    let prompt = textArg args "prompt"

                    let! result =
                        match HostSessionContext.roleOf agent with
                        | Some role -> runtime.Fork(newAgentId (), role, prompt)
                        | None -> runtime.Reuse(agent, prompt)

                    match result with
                    | Ok fork -> return box (stringify (createObj [ "agentId", box fork.AgentId ]))
                    | Error err -> return box (stringify (createObj [ "error", box err ]))
            }

        let joinExecute (_args: obj) (context: obj) =
            task {
                match runtimeFor context with
                | Error err -> return box (stringify (createObj [ "error", box err ]))
                | Ok runtime ->
                    let! result = runtime.Join()

                    match result with
                    | Ok completion ->
                        return
                            box (
                                stringify (
                                    createObj
                                        [ "agentId", box completion.AgentId
                                          "runId", box completion.RunId
                                          "outcome", box completion.Outcome ]
                                )
                            )
                    | Error error -> return box (stringify (createObj [ "error", box (error.ToString()) ]))
            }

        let listExecute (_args: obj) (context: obj) =
            task {
                match runtimeFor context with
                | Error err -> return box (stringify (createObj [ "error", box err ]))
                | Ok runtime ->
                    let agents, _ = runtime.List()

                    let result =
                        agents
                        |> List.map (fun agent ->
                            createObj
                                [ "agentId", box agent.AgentId
                                  "role", box (agent.Role.ToString())
                                  "status", box (agent.Status.ToString()) ])
                        |> List.toArray

                    return box (stringify (box result))
            }

        let definition description args execute =
            createObj
                [ "description", box description
                  "args", box args
                  "execute", uncurriedExecute (box execute) ]

        let forkArgs =
            createObj [ "agent", box (stringSchema factory); "prompt", box (stringSchema factory) ]

        createObj
            [ "fork", box (applyTool factory (definition "Fork or nudge an agent" forkArgs forkExecute))
              "join", box (applyTool factory (definition "Wait for any agent completion" (createObj []) joinExecute))
              "list", box (applyTool factory (definition "List active agents" (createObj []) listExecute)) ]

    let initSpikePlugin (input: obj) : Task<obj> =
        task {
            let portOpt = OpenCodePort.create input
            let journal = createJournal input

            match createHost input portOpt with
            | Error err -> return raise (InvalidOperationException err)
            | Ok(eventPort, sessionPort, subscription, observeEvent) ->
                let companions = Dictionary<string, CompanionHost>()
                let companionGate = obj ()
                let sessionRoles = Dictionary<string, string>()
                let mutable latestSessionId = ""

                let transform inObj outObj =
                    if
                        not (isNull inObj)
                        && isNull inObj?sessionID
                        && not (String.IsNullOrWhiteSpace latestSessionId)
                    then
                        inObj?sessionID <- latestSessionId

                    if
                        not (isNull inObj)
                        && isNull inObj?agent
                        && sessionRoles.ContainsKey latestSessionId
                    then
                        inObj?agent <- sessionRoles.[latestSessionId]

                    CompanionTransform.handleCompanionTransform companions companionGate sessionPort inObj outObj

                let hooks =
                    createObj
                        [ "projection", box Projection.projectMessages
                          "events", box eventPort
                          "sessions", box sessionPort
                          "journal", box journal
                          "hostEventsSubscription", box subscription
                          "chat.transform", box (uncurriedExecute (box transform))
                          "experimental.chat.messages.transform", box (uncurriedExecute (box transform))
                          "config", box (fun (config: obj) -> configureManager config) ]

                observeEvent
                |> Option.iter (fun observe ->
                    hooks?event <-
                        box (fun raw ->
                            let sessionId, role = HostSessionContext.read raw

                            if not (String.IsNullOrWhiteSpace sessionId) then
                                latestSessionId <- sessionId

                                role |> Option.iter (fun value -> sessionRoles.[sessionId] <- value)

                            observe raw))

                let client = if isNull input then null else input?client

                if not (isNull client) then
                    try
                        let! toolModule = importToolModule ()
                        hooks?tool <- toolHooks toolModule sessionPort journal
                    with ex ->
                        raise (InvalidOperationException(sprintf "Failed to load OpenCode tool module: %s" ex.Message))

                return box hooks
        }
