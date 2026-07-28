namespace Wanxiangshu.Next.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
open SpikePluginHelpers

module SpikePlugin =

    let initSpikePlugin (input: obj) : Task<obj> =
        task {
            let portOpt = OpenCodePort.create input
            let journal = PluginHost.createJournal input
            let sessionDirectories = Dictionary<string, string>()
            let ownedSessions = HashSet<string>()
            let userMessageBindings = Dictionary<string, MessageId>()
            let fallbackFailures = HashSet<string>()

            match PluginHost.createHost input portOpt with
            | Error err -> return raise (InvalidOperationException err)
            | Ok(eventPort, sessionPort, snapshotOpt) ->
                let companions = Dictionary<string, CompanionHost>()
                let companionGate = obj ()
                let sessionRoles = Dictionary<string, string>()
                let sessionParents = Dictionary<string, string>()
                let verdictSessions = HashSet<string>()
                let nudgeSent = HashSet<string>()
                let managerGuardNudges = HashSet<string>()

                PluginHost.restoreSessionRoles journal sessionRoles
                PluginHost.restoreSessionParents journal sessionParents

                for KeyValue(childId, parentId) in sessionParents do
                    ownedSessions.Add childId |> ignore
                    ownedSessions.Add parentId |> ignore

                let gitTreePort =
                    match PluginHost.gitTreePortFromInput input with
                    | Some port -> Some port
                    | None -> PluginHost.workspaceDirectory input |> Option.map GitTree.create

                let sessionBudgets = Dictionary<string, int>()
                let sessionOutputLimits = Dictionary<string, int>()
                let bloggerModel = ModelResolver.bloggerModelFromEnv ()
                let mutable toolSurfaceRef: obj option = None

                let modelConfig = ModelResolver.fromEnv ()

                let disposeExecutorRuntimeCb (sid: string) =
                    match toolSurfaceRef with
                    | Some ts -> ts?disposeExecutorRuntime (sid) |> ignore
                    | None -> ()

                let wired =
                    HostSignalBootstrap.wire
                        sessionPort
                        eventPort
                        snapshotOpt
                        journal
                        gitTreePort
                        sessionRoles
                        sessionParents
                        verdictSessions
                        nudgeSent
                        managerGuardNudges
                        ownedSessions
                        userMessageBindings
                        fallbackFailures
                        disposeExecutorRuntimeCb
                        modelConfig
                        input

                let bindRunStarted =
                    box (fun (sessionId: string) (role: string) (directory: string) ->
                        match AgentRoleHelpers.roleOfString role with
                        | None -> ()
                        | Some agentRole ->
                            wired.BindActiveRun
                                (SessionId.create sessionId)
                                agentRole
                                (if String.IsNullOrWhiteSpace directory then
                                     None
                                 else
                                     Some directory))

                let transform inObj outObj =
                    let projectionSessionIdOpt =
                        if
                            not (isNull inObj)
                            && not (isNull inObj?sessionID)
                            && not (String.IsNullOrWhiteSpace(unbox<string> inObj?sessionID))
                        then
                            Some(unbox<string> inObj?sessionID)
                        else
                            projectionSessionIdFromMessages outObj

                    match projectionSessionIdOpt with
                    | Some projectionSessionId ->
                        wired.RegisterOwned projectionSessionId

                        // sessionRoles is display/tool-surface cache only.
                        // Companion eligibility must not be inferred here.
                        if not (isNull inObj) && isNull inObj?sessionID then
                            inObj?sessionID <- projectionSessionId
                    | None -> ()

                    CompanionTransform.handleCompanionTransform
                        companions
                        companionGate
                        sessionPort
                        (Some(eventPort :> IEventOutputBoundaryPort))
                        journal
                        sessionBudgets
                        sessionOutputLimits
                        sessionRoles
                        bloggerModel
                        (Some(fun bloggerId ->
                            // Register ownership + ActiveRun so idle→reconcile
                            // emits TerminalOutcome.Completed for this child.
                            wired.RegisterOwned(SessionId.value bloggerId)
                            wired.BindActiveRun bloggerId AgentRole.Blogger None))
                        inObj
                        outObj

                let chatParams = ChatParamsHook.create journal modelConfig

                let hooks =
                    createObj
                        [ "projection", box Projection.projectMessages
                          "events", box eventPort
                          "sessions", box sessionPort
                          "journal", box journal
                          "hostEventsSubscription", box wired.Subscription
                          "bindRunStarted", bindRunStarted
                          "chat.message", box (uncurriedExecute wired.ChatMessageHook)
                          // Host built-in retry reuses the same user message; inject
                          // DurableFallback EffectiveModel on every provider attempt.
                          "chat.params", box (uncurriedExecute chatParams)
                          "chat.transform", box (uncurriedExecute (box transform))
                          // Both hooks are registered for compatibility: some
                          // OpenCode host versions call chat.transform while
                          // others call experimental.chat.messages.transform.
                          // The idempotency guard in handleCompanionTransform
                          // detects companion-b-head already present and skips
                          // the second invocation, preventing duplicate B heads.
                          "experimental.chat.messages.transform", box (uncurriedExecute (box transform))
                          "experimental.chat.system.transform",
                          box (systemTransformHook sessionBudgets sessionOutputLimits)
                          "config", box (fun (config: obj) -> ManagerConfig.configureManager config) ]

                hooks?event <- box wired.ObserveEvent

                let client = if isNull input then null else input?client

                if not (isNull client) then
                    try
                        let! toolModule = importToolModule ()

                        let onRunStarted =
                            Some(fun sessionId role directory -> wired.BindActiveRun sessionId role directory)

                        // Child background SSOT: parent B first; if no B, whole session A.
                        let backgroundBFor =
                            Some(fun sessionId ->
                                let fromB =
                                    match companions.TryGetValue sessionId with
                                    | true, host ->
                                        match host.Memory.ActivePrefixEpoch with
                                        | Some epoch when not (String.IsNullOrWhiteSpace epoch.FrozenB) ->
                                            Some epoch.FrozenB
                                        | _ ->
                                            host.Memory.LatestB
                                            |> Option.filter (fun text -> not (String.IsNullOrWhiteSpace text))
                                    | false, _ -> None

                                match fromB with
                                | Some text -> Some text
                                | None -> TerminalSessionA.fullText eventPort (SessionId.create sessionId))

                        let toolSurface =
                            toolHooks
                                toolModule
                                sessionPort
                                journal
                                gitTreePort
                                (PluginHost.workspaceDirectory input)
                                sessionParents
                                sessionRoles
                                wired.CurrentPhysicalUserMessage
                                verdictSessions
                                sessionDirectories
                                modelConfig
                                onRunStarted
                                backgroundBFor
                                snapshotOpt

                        toolSurfaceRef <- Some toolSurface
                        hooks?tool <- toolSurface
                    with ex ->
                        raise (InvalidOperationException(sprintf "Failed to load OpenCode tool module: %s" ex.Message))

                return box hooks
        }

    let createSpikePlugin (config: SpikePluginConfig) : obj =
        let input: obj =
            createObj
                [ "directory", box config.Directory
                  "port", box (config.Port |> Option.map box |> Option.defaultValue null) ]

        createObj
            [ "hooks",
              box (fun (inputObj: obj) ->
                  let mergedInput =
                      if isNull inputObj then
                          input
                      else
                          createObj
                              [ "directory",
                                box (
                                    if isNull inputObj?directory then
                                        config.Directory
                                    else
                                        inputObj?directory
                                )
                                "port",
                                box (
                                    if isNull inputObj?port then
                                        box config.Port
                                    else
                                        inputObj?port
                                )
                                "client", box inputObj?client
                                "events", box inputObj?events
                                "gitTreePort", box inputObj?gitTreePort ]

                  initSpikePlugin mergedInput) ]
