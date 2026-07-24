namespace Wanxiangshu.Next.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
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

    [<Emit("(args, context) => $0(args)(context)")>]
    let private uncurriedExecute (fn: obj) : obj = jsNative

    let private gitTreePortFromInput (input: obj) : GitTreePort option =
        if isNull input || isNull input?gitTreePort || isNull input?gitTreePort?getTreeHash then
            None
        else
            let rawPort = input?gitTreePort

            Some
                { GetTreeHash =
                    fun () ->
                        let getTreeHash = rawPort?getTreeHash
                        unbox<string> (getTreeHash ()) }

    let private nudgeReviewer (sessionPort: ISessionHostPort) (nudgeSent: HashSet<string>) (sessionId: string) : unit =
        if nudgeSent.Add sessionId then
            let prompt =
                "Submit a structured verdict with the verdict tool: PERFECT or REVISE. "
                + "Do not put a verdict in prose."

            let nudgeTask: Task<Result<MessageId, string>> =
                sessionPort.SendPrompt(
                    SessionId.create sessionId,
                    prompt,
                    { Model = None
                      Agent = Some "reviewer" }
                )

            nudgeTask |> ignore

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
            agents?reviewer <- StaticTools.reviewerAgentConfig ()

    let private toolHooks
        (toolModule: obj)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (gitTreePort: GitTreePort option)
        (sessionParents: Dictionary<string, string>)
        (sessionRoles: Dictionary<string, string>)
        (verdictSessions: HashSet<string>)
        : obj =
        ToolSurface.create toolModule sessionPort journal gitTreePort sessionParents sessionRoles verdictSessions

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
                let sessionParents = Dictionary<string, string>()
                let verdictSessions = HashSet<string>()
                let nudgeSent = HashSet<string>()
                let gitTreePort = gitTreePortFromInput input
                let mutable latestSessionId = ""

                let rawEvent (raw: obj) =
                    if isNull raw || isNull raw?event then raw else raw?event

                let rawProperties (raw: obj) =
                    let event = rawEvent raw
                    if isNull event then null else event?properties

                let rawSessionId (raw: obj) =
                    let event = rawEvent raw
                    let properties = rawProperties raw

                    if not (isNull properties) && not (isNull properties?sessionID) then
                        Some(unbox<string> properties?sessionID)
                    elif not (isNull event) && not (isNull event?sessionID) then
                        Some(unbox<string> event?sessionID)
                    else
                        None

                let rawParentSessionId (raw: obj) =
                    let event = rawEvent raw
                    let properties = rawProperties raw

                    if not (isNull properties) && not (isNull properties?parentID) then
                        Some(unbox<string> properties?parentID)
                    elif
                        not (isNull properties)
                        && not (isNull properties?info)
                        && not (isNull properties?info?parentID)
                    then
                        Some(unbox<string> properties?info?parentID)
                    elif not (isNull event) && not (isNull event?parentID) then
                        Some(unbox<string> event?parentID)
                    else
                        None

                let isTerminalEvent (raw: obj) =
                    let event = rawEvent raw

                    if isNull event || isNull event?``type`` then
                        false
                    else
                        let eventType = unbox<string> event?``type``
                        eventType = "session.idle" || eventType = "session.aborted"

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

                                rawParentSessionId raw
                                |> Option.iter (fun parentId ->
                                    if not (String.IsNullOrWhiteSpace parentId) then
                                        sessionParents.[sessionId] <- parentId)

                                if isTerminalEvent raw then
                                    match sessionRoles.TryGetValue sessionId with
                                    | true, agent when agent.Equals("reviewer", StringComparison.OrdinalIgnoreCase) ->
                                        if not (verdictSessions.Contains sessionId) then
                                            nudgeReviewer sessionPort nudgeSent sessionId
                                    | _ -> ()

                            observe raw))

                let client = if isNull input then null else input?client

                if not (isNull client) then
                    try
                        let! toolModule = importToolModule ()

                        hooks?tool <-
                            toolHooks
                                toolModule
                                sessionPort
                                journal
                                gitTreePort
                                sessionParents
                                sessionRoles
                                verdictSessions
                    with ex ->
                        raise (InvalidOperationException(sprintf "Failed to load OpenCode tool module: %s" ex.Message))

                return box hooks
        }
