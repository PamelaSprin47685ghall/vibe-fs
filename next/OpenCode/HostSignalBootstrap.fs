namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

module HostSignalBootstrap =

    type WiredSignals =
        { Reconciler: SessionReconciler
          SignalRouter: HostSignalRouter
          Subscription: IDisposable option
          RegisterOwned: string -> unit
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
        (input: obj)
        : WiredSignals =
        let snapshot =
            match snapshotOpt with
            | Some port -> port
            | None ->
                { new ISessionSnapshotPort with
                    member _.GetMessages _ =
                        Task.FromResult(Ok([]: SessionMessage list)) }

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
                turn

        let reconciler = SessionReconciler(snapshot, onTurn)

        let onSignal (signal: HostSignal) =
            match signal with
            | ProviderRetry retry ->
                RetrySignalHandler.handle journal fallbackFailures userMessageBindings retry
            | SessionIdle _
            | SessionDeleted _ -> reconciler.HandleSignal signal

        let signalRouter = HostSignalRouter(ownedSessions, onSignal)

        let subscription =
            match HostSignalSubscribe.trySubscribe input signalRouter.Observe with
            | Error err -> raise (InvalidOperationException err)
            | Ok sub -> sub

        let registerOwned (sessionId: string) =
            if not (String.IsNullOrWhiteSpace sessionId) then
                ownedSessions.Add sessionId |> ignore
                signalRouter.RegisterOwned(SessionId.create sessionId)

        let bindUserMessage (sessionId: string) (messageId: string) =
            if
                not (String.IsNullOrWhiteSpace sessionId)
                && not (String.IsNullOrWhiteSpace messageId)
            then
                let sid = SessionId.create sessionId
                let mid = MessageId.create messageId
                userMessageBindings.[sessionId] <- mid
                reconciler.BindUserMessage(sid, mid)
                registerOwned sessionId

        let bindActiveRun (sessionId: SessionId) (role: AgentRole) (directory: string option) =
            registerOwned (SessionId.value sessionId)

            reconciler.BindActiveRun
                { SessionId = sessionId
                  UserMessageId =
                    match userMessageBindings.TryGetValue(SessionId.value sessionId) with
                    | true, mid -> Some mid
                    | false, _ -> None
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

                    bindUserMessage sessionId messageId)

        { Reconciler = reconciler
          SignalRouter = signalRouter
          Subscription = subscription
          RegisterOwned = registerOwned
          BindUserMessage = bindUserMessage
          BindActiveRun = bindActiveRun
          ChatMessageHook = chatMessageHook
          ObserveEvent = signalRouter.Observe }
