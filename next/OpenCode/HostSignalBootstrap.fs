namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

module HostSignalBootstrap =

    type WiredSignals =
        { Reconciler: SessionReconciler
          SignalRouter: HostSignalRouter
          Subscription: IDisposable option
          RegisterOwned: string -> unit
          RegisterSource: string -> SessionSignalSource -> unit
          BindUserMessage: string -> string -> unit
          BindActiveRun: SessionId -> AgentRole -> string option -> unit
          ChatMessageHook: obj
          ObserveEvent: obj -> unit }

    let wire
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (snapshotOpt: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (gitTreePort: GitTreePort option)
        (sessionRoles: Dictionary<string, string>)
        (sessionParents: Dictionary<string, string>)
        (verdictSessions: HashSet<string>)
        (nudgeSent: HashSet<string>)
        (managerGuardNudges: HashSet<string>)
        (ownedSessions: HashSet<string>)
        (userMessageBindings: Dictionary<string, MessageId>)
        (fallbackFailures: HashSet<string>)
        (disposeExecutorRuntime: string -> unit)
        (modelConfig: ModelResolver.ModelConfig option)
        (input: obj)
        : WiredSignals =
        let snapshot =
            match snapshotOpt with
            | Some port -> port
            | None ->
                { new ISessionSnapshotPort with
                    member _.GetMessages _ =
                        Task.FromResult(Ok([]: SessionMessage list)) }

        let abortedSessions = HashSet<string>()

        let onTurn (turn: ReconciledTurn) =
            TerminalPolicies.apply
                sessionPort
                eventPort
                journal
                gitTreePort
                verdictSessions
                nudgeSent
                managerGuardNudges
                sessionParents
                disposeExecutorRuntime
                abortedSessions
                turn

        let reconciler = SessionReconciler(snapshot, onTurn)

        let onSignal (signal: HostSignal) =
            match signal with
            | ProviderRetry retry -> RetrySignalHandler.handle journal fallbackFailures userMessageBindings retry
            | SessionIdle _
            | SessionDeleted _ -> reconciler.HandleSignal signal

        let signalRouter = HostSignalRouter(ownedSessions, onSignal)

        let subscription =
            match HostSignalSubscribe.trySubscribe input signalRouter.ObserveGlobal with
            | Error err -> raise (InvalidOperationException err)
            | Ok(sub, _source) -> sub

        let registerOwned (sessionId: string) =
            if not (String.IsNullOrWhiteSpace sessionId) then
                ownedSessions.Add sessionId |> ignore
                signalRouter.RegisterOwned(SessionId.create sessionId)

        let registerSource (sessionId: string) (source: SessionSignalSource) =
            if not (String.IsNullOrWhiteSpace sessionId) then
                signalRouter.RegisterSource(SessionId.create sessionId, source)

        let bindUserMessage (sessionId: string) (messageId: string) =
            if
                not (String.IsNullOrWhiteSpace sessionId)
                && not (String.IsNullOrWhiteSpace messageId)
            then
                let sid = SessionId.create sessionId
                let mid = MessageId.create messageId
                userMessageBindings.[sessionId] <- mid
                reconciler.BindUserMessage(sid, mid)
                abortedSessions.Remove sessionId |> ignore
                registerOwned sessionId
                registerSource sessionId LocalPluginEvent

        let workspaceDir =
            if isNull input || isNull input?directory then
                None
            else
                let d = unbox<string> input?directory
                if String.IsNullOrWhiteSpace d then None else Some d

        let bindActiveRun (sessionId: SessionId) (role: AgentRole) (directory: string option) =
            let key = SessionId.value sessionId
            registerOwned key
            // Child sessions in a different worktree directory are observed via
            // global SSE only; local plugin events belong to that worktree's
            // own plugin instance.
            match directory, workspaceDir with
            | Some childDir, Some root when childDir <> root -> registerSource key GlobalForeignDirectoryEvent
            | Some _, None -> registerSource key GlobalForeignDirectoryEvent
            | _ -> registerSource key LocalPluginEvent

            reconciler.BindActiveRun
                { SessionId = sessionId
                  RunId = None
                  RootUserMessageId =
                    match userMessageBindings.TryGetValue(SessionId.value sessionId) with
                    | true, mid -> Some mid
                    | false, _ -> None
                  ContinuationMessageIds = Set.empty
                  AgentRole = Some role
                  Directory = defaultArg directory "" }

        let chatMessageHook =
            box (fun (inputObj: obj) (outputObj: obj) ->
                if not (isNull inputObj) then
                    let sessionId =
                        if isNull inputObj?sessionID then
                            ""
                        else
                            unbox<string> inputObj?sessionID

                    let messageId =
                        if not (isNull inputObj?messageID) then
                            unbox<string> inputObj?messageID
                        elif not (isNull inputObj?messageId) then
                            unbox<string> inputObj?messageId
                        elif
                            not (isNull outputObj)
                            && not (isNull outputObj?message)
                            && not (isNull outputObj?message?id)
                        then
                            unbox<string> outputObj?message?id
                        elif
                            not (isNull outputObj)
                            && not (isNull outputObj?info)
                            && not (isNull outputObj?info?id)
                        then
                            unbox<string> outputObj?info?id
                        elif not (isNull outputObj) && not (isNull outputObj?id) then
                            unbox<string> outputObj?id
                        else
                            ""

                    let agent =
                        if not (isNull inputObj?agent) then
                            Some(unbox<string> inputObj?agent)
                        else
                            None

                    registerOwned sessionId

                    agent
                    |> Option.bind HostSessionContext.canonicalRole
                    |> Option.iter (fun role -> sessionRoles.[sessionId] <- role)

                    bindUserMessage sessionId messageId

                    // A/A/B/B: rewrite outbound model from durable Fallback projection.
                    // HostPendingRun already does this for forked children; root sessions
                    // need the same path so Side B is not stuck on client-supplied model.
                    match modelConfig, journal, agent with
                    | Some cfg, Some j, Some _ when not (String.IsNullOrWhiteSpace sessionId) ->
                        match
                            ModelResolver.resolveForSession cfg (SessionId.create sessionId) (AgentJournal.snapshot j)
                        with
                        | Some selected ->
                            let modelObj =
                                createObj
                                    [ "providerID", box selected.providerID
                                      "modelID", box selected.modelID
                                      "id", box selected.modelID ]

                            if not (isNull outputObj) then
                                if not (isNull outputObj?message) then
                                    outputObj?message?model <- modelObj

                                if not (isNull outputObj?info) then
                                    outputObj?info?model <- modelObj

                                outputObj?model <- modelObj

                            inputObj?model <- modelObj
                        | None -> ()
                    | _ -> ())

        { Reconciler = reconciler
          SignalRouter = signalRouter
          Subscription = subscription
          RegisterOwned = registerOwned
          BindUserMessage = bindUserMessage
          BindActiveRun = bindActiveRun
          ChatMessageHook = chatMessageHook
          RegisterSource = registerSource
          ObserveEvent = signalRouter.ObserveLocal }
