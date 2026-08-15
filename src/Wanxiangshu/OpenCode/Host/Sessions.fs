namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Foundation

type SessionPromptOptions = OpenCodePromptOptions

type ISessionHostPort =
    abstract SubscribeTerminal: sessionId: SessionId * listener: TerminalCompletionListener -> IDisposable

    /// PROMPT-005/PROMPT-011: the outcome, not a `Result`.
    ///
    /// `Result<messageId, string>` had to invent a message id on every success and
    /// had one failure shape for every failure. Both erasures matter: a transport
    /// receipt is not a physical message, and `AcceptanceUnknown` is not
    /// `Retryable` — resending the former can produce two logical effects.
    abstract SendPrompt: sessionId: SessionId * text: string * opts: SessionPromptOptions -> Task<SendOutcome>

    abstract AbortSession: sessionId: SessionId -> Task<Result<unit, string>>
    /// Fission replacement interrupt: abort only this physical Host session.
    /// Unlike AbortSession, this does not detach/cancel the logical owner's
    /// managed children. The later TurnAborted is classified by FissionRuntime.
    abstract InterruptSessionOnly: sessionId: SessionId -> Task<Result<unit, string>>
    abstract AbortChildren: parentId: SessionId -> Task

    /// Fission-only physical sibling creation. `physicalParentId` is the old
    /// caller's Host parent; no managed-child registry/linkage is created.
    abstract CreateSiblingSession:
        ownerSessionId: SessionId * physicalParentId: SessionId option * options: OpenCodeChildOptions ->
            Task<Result<SessionId, string>>

    abstract TryGetParentSession: sessionId: SessionId -> Task<Result<SessionId option, string>>
    abstract CreateChildSession: parentId: SessionId * options: OpenCodeChildOptions -> Task<Result<SessionId, string>>
    abstract ListChildren: parentId: SessionId -> Task<Result<OpenCodeChildInfo list, string>>

    /// HOST-015: the family root every managed child is physically parented to.
    /// Ownership is proven by durable journal links, never by Host parentID.
    abstract FamilyRootOf: sessionId: SessionId -> SessionId

type InjectedSessionPort
    (
        underlyingPort: IOpenCodePort option,
        eventPort: IEventObservationPort,
        ?familyParent: SessionId -> SessionId option
    ) =
    let activeListeners = HashSet<SessionId>()
    let parentChildMap = Dictionary<SessionId, HashSet<SessionId>>()
    let childParents = Dictionary<SessionId, SessionId>()
    let lockObj = obj ()
    let restoredParent = defaultArg familyParent (fun _ -> None)

    let rec findRestoredRoot current =
        match restoredParent current with
        | Some parentId when parentId <> current -> findRestoredRoot parentId
        | _ -> current

    let familyRoot (sessionId: SessionId) =
        match lock lockObj (fun () -> childParents.TryGetValue sessionId) with
        | true, rootId -> rootId
        | false, _ -> findRestoredRoot sessionId

    let managedChild (sessionId: SessionId) =
        lock lockObj (fun () -> childParents.ContainsKey sessionId)
        || restoredParent sessionId |> Option.isSome
        || SessionExecutionBinding.tryParent sessionId |> Option.isSome

    let registerChild (parentId: SessionId) (childId: SessionId) =
        lock lockObj (fun () ->
            let rootId =
                match childParents.TryGetValue parentId with
                | true, value -> value
                | false, _ -> parentId

            match childParents.TryGetValue childId with
            | true, previousRoot when previousRoot <> rootId && parentChildMap.ContainsKey previousRoot ->
                parentChildMap.[previousRoot].Remove childId |> ignore
            | _ -> ()

            if not (parentChildMap.ContainsKey rootId) then
                parentChildMap.[rootId] <- HashSet<SessionId>()

            parentChildMap.[rootId].Add childId |> ignore
            childParents.[childId] <- rootId)

    let forgetChildParents (children: SessionId list) =
        for childId in children do
            childParents.Remove childId |> ignore

    let getAndRemoveChildren (parentId: SessionId) =
        lock lockObj (fun () ->
            if parentChildMap.ContainsKey parentId then
                let children = parentChildMap.[parentId] |> Seq.toList
                parentChildMap.Remove parentId |> ignore
                forgetChildParents children
                children
            else
                [])

    let detachFromRoot (rootId: SessionId) (childId: SessionId) =
        if parentChildMap.ContainsKey rootId then
            parentChildMap.[rootId].Remove childId |> ignore

    let pruneEmptyRoot (rootId: SessionId) =
        if parentChildMap.ContainsKey rootId && parentChildMap.[rootId].Count = 0 then
            parentChildMap.Remove rootId |> ignore

    let detachChild (childId: SessionId) =
        lock lockObj (fun () ->
            match childParents.TryGetValue childId with
            | true, rootId ->
                childParents.Remove childId |> ignore
                detachFromRoot rootId childId
                pruneEmptyRoot rootId
            | false, _ -> ())

    let abortOneChild (childId: SessionId) =
        task {
            match underlyingPort with
            | Some port ->
                let! _ = port.AbortSession childId
                ()
            | None -> ()
        }

    let abortChildren (parentId: SessionId) =
        task {
            let children = getAndRemoveChildren parentId

            for childId in children do
                do! abortOneChild childId
        }

    let routeSendOptions (sessionId: SessionId) (opts: SessionPromptOptions) =
        task {
            match SessionExecutionBinding.effectiveAgent sessionId opts with
            | Error error -> return Error error
            | Ok effectiveAgent ->
                // EMR-004/006: required execution waits on the scheduler rather than
                // turning capacity backpressure into a provider/business failure.
                let! target = ModelRouting.acquireManaged sessionId effectiveAgent

                let routed =
                    { opts with
                        Agent = Some effectiveAgent
                        Model = Some(ModelRouting.toOpenCodeModel target) }

                return
                    if managedChild sessionId then
                        SessionExecutionBinding.normalizeManagedPrompt sessionId routed
                    else
                        SessionExecutionBinding.normalizeUserFacingPrompt sessionId routed
        }

    let sendThroughPort
        (port: IOpenCodePort)
        (sessionId: SessionId)
        (text: string)
        (sendOptions: OpenCodePromptOptions)
        =
        task {
            // chat.params runs inside the Host send. Mark this interval so
            // its root-session observer cannot mistake our own continuation
            // or typed override for a new external user choice.
            SessionExecutionBinding.beginInternalSend sessionId sendOptions

            try
                // Pass the outcome through unchanged. This layer knows less
                // about acceptance than the port does; narrowing here is how
                // AcceptanceUnknown used to become a plain error.
                return! port.SendPrompt sessionId text sendOptions
            finally
                SessionExecutionBinding.endInternalSend sessionId
        }

    let bindSiblingLane (port: IOpenCodePort) (ownerSessionId: SessionId) (laneId: SessionId) (agent: string option) =
        taskResult {
            try
                SessionExecutionBinding.bindInternalRoot laneId agent
                ProviderLanguageBinding.ensureInherited ownerSessionId laneId |> ignore
                return laneId
            with ex ->
                do!
                    task {
                        let! _ = port.AbortSession laneId
                        return ()
                    }
                    |> TaskResultCE.ofTask

                return! Error ex.Message
        }

    let createSiblingSession
        (ownerSessionId: SessionId)
        (physicalParentId: SessionId option)
        (options: OpenCodeChildOptions)
        =
        taskResult {
            let! port =
                underlyingPort
                |> Result.requireSome "No Host transport: cannot create a sibling session"

            let! laneId = port.CreateSession physicalParentId options
            return! bindSiblingLane port ownerSessionId laneId options.Agent
        }

    let createChildSession (parentId: SessionId) (options: OpenCodeChildOptions) =
        taskResult {
            let rootId = familyRoot parentId
            // HOST-015: every managed child is physically parented to the
            // family root — a son's son is a son. Recovery proves ownership
            // by the journal-linked SessionId + agent/title, never by the
            // Host parentID.
            let hostParentId = rootId

            let! port =
                underlyingPort
                |> Result.requireSome "No Host transport: cannot create a child session"

            let! childId = port.CreateChildSession hostParentId options
            registerChild rootId childId
            SessionExecutionBinding.bind parentId childId options.Agent
            // HOST-026: inherit owner/commissioner language (parentId), not family root.
            ProviderLanguageBinding.ensureInherited parentId childId |> ignore
            return childId
        }

    interface ISessionHostPort with
        member _.AbortChildren(parentId) = abortChildren parentId

        member me.SubscribeTerminal(sessionId, listener) =
            lock lockObj (fun () -> activeListeners.Add(sessionId) |> ignore)

            let sub =
                eventPort.SubscribeTerminalListener(fun sId outcome ->
                    if sId = sessionId then
                        listener sId outcome)

            { new IDisposable with
                member _.Dispose() =
                    sub.Dispose()
                    lock lockObj (fun () -> activeListeners.Remove(sessionId) |> ignore) }

        member me.SendPrompt(sessionId, text, opts) =
            task {
                let hasListener = lock lockObj (fun () -> activeListeners.Contains(sessionId))

                if not hasListener then
                    // Fatal, not Retryable: the listener is registered by the caller
                    // before it sends, so this is a call-order defect. Retrying the
                    // same wrong order cannot fix it.
                    return Fatal "AG-LISTENER-BEFORE-SEND: Listener must be registered before sending prompt"
                else
                    match! routeSendOptions sessionId opts with
                    | Error error -> return Fatal error
                    | Ok sendOptions ->
                        match underlyingPort with
                        | Some port -> return! sendThroughPort port sessionId text sendOptions
                        | None ->
                            return Fatal "No Host transport: plugin input carried no client, serverUrl, baseUrl or port"
            }

        member _.InterruptSessionOnly(sessionId) =
            task {
                Diagnostic.emit "session-fission-interrupt" [ "session_id", SessionId.value sessionId ]
                SessionExecutionBinding.cancelUnacquired sessionId

                match underlyingPort with
                | Some port -> return! port.AbortSession sessionId
                | None -> return Error "No Host transport: cannot interrupt session"
            }

        member me.AbortSession(sessionId) =
            task {
                // Who killed a turn is the first question a stalled run raises, and the answer used
                // to be nowhere: every abort path was silent, so an interrupted tool call looked
                // identical to a model that simply stopped. One record per abort, visible under
                // WANXIANGSHU_DIAG=1.
                Diagnostic.emit "session-abort" [ "session_id", SessionId.value sessionId ]
                SessionExecutionBinding.cancelUnacquired sessionId
                detachChild sessionId
                do! abortChildren sessionId

                match underlyingPort with
                | Some port ->
                    let! _ = port.AbortSession(sessionId)
                    ()
                | None -> ()

                return Ok()
            }

        member _.CreateSiblingSession(ownerSessionId, physicalParentId, options) =
            createSiblingSession ownerSessionId physicalParentId options

        member _.TryGetParentSession(sessionId) =
            match underlyingPort with
            | None -> Task.FromResult(Error "No Host transport: cannot read session parent")
            | Some port -> port.GetSessionParent sessionId

        member me.CreateChildSession(parentId, options) = createChildSession parentId options

        member _.ListChildren(parentId) =
            match underlyingPort with
            | Some port -> port.ListChildren parentId
            | None -> Task.FromResult(Error "No Host transport: cannot list child sessions")

        member _.FamilyRootOf(sessionId) = familyRoot sessionId
