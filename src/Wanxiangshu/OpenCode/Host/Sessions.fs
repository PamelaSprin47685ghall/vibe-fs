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
    /// Internal attempt stop: abort only this physical Host attempt.
    /// Unlike AbortSession, this does not detach/cancel logical children and is
    /// unavailable to user-facing/root sessions.
    abstract InterruptAttempt: sessionId: SessionId -> Task<Result<unit, string>>
    abstract TerminateAttempt: sessionId: SessionId * reason: string -> Task<Result<unit, string>>
    abstract TryTakeAttemptTermination: sessionId: SessionId -> string option
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

[<RequireQualifiedAccess>]
module ManagedSessionTermination =

    let private captureResult (operation: unit -> Task<Result<unit, string>>) =
        task {
            try
                return! operation ()
            with ex ->
                return Error ex.Message
        }

    let private captureUnit (operation: unit -> Task) =
        task {
            try
                do! operation ()
                return Ok()
            with ex ->
                return Error ex.Message
        }

    let private combineEffects cancelOutcome abortOutcome =
        match cancelOutcome, abortOutcome with
        | Error error, _ -> Error("descendant cancellation failed: " + error)
        | Ok(), Error error -> Error("Host abort failed: " + error)
        | Ok(), Ok() -> Ok()

    /// MANAGED-SESSION-017: fail-closed termination is one causal CE, not an
    /// attempt-only interrupt followed by a future callback guessing the cause.
    /// Descendant durable cancellation precedes physical teardown; Failed is
    /// published after teardown so the existing fork terminal listener commits
    /// HandleCompleted and pulses the parent join mailbox.
    let terminate
        (cancelSessionChildren: SessionId -> Task)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (sessionId: SessionId)
        (reason: string)
        : Task<Result<unit, string>> =
        if sessionPort.FamilyRootOf sessionId = sessionId then
            Task.FromResult(
                Error "MANAGED-SESSION-016: user-facing/root session may only be interrupted by the external user"
            )
        else
            task {
                let! cancelOutcome = captureUnit (fun () -> cancelSessionChildren sessionId)
                let! abortOutcome = captureResult (fun () -> sessionPort.AbortSession sessionId)

                eventPort.NotifyTerminal sessionId (TerminalOutcome.Failed reason)
                |> ignore

                return combineEffects cancelOutcome abortOutcome
            }

type InjectedSessionPort
    (
        underlyingPort: IOpenCodePort option,
        eventPort: IEventObservationPort,
        ?familyParent: SessionId -> SessionId option
    ) =
    // DSL-MUTABLE: resource — active terminal listener registry per session
    let activeListeners = Dictionary<SessionId, HashSet<Guid>>()

    let removeActiveListenerToken sessionId token =
        match activeListeners.TryGetValue sessionId with
        | true, listeners ->
            listeners.Remove token |> ignore
            listeners.Count = 0
        | false, _ -> false

    // DSL-MUTABLE: resource — parent-to-child session set map
    let parentChildMap = Dictionary<SessionId, HashSet<SessionId>>()
    // DSL-MUTABLE: resource — child-to-parent session map
    let childParents = Dictionary<SessionId, SessionId>()
    /// DSL-cross-callback-proof: physical single-flight
    // DSL-MUTABLE: single-flight — attempt-scoped termination reason consumed on abort
    let attemptTerminations = Dictionary<SessionId, string>()
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
        // Dispatch admission is intentionally capacity-free. A session is a
        // reusable container, and the async prompt enqueue must never wait for a
        // provider slot. The sole model/capacity owner is chat.message, where the
        // Host is actually preparing this physical user message for execution.
        if managedChild sessionId then
            SessionExecutionBinding.prepareManagedPrompt sessionId opts
        else
            SessionExecutionBinding.prepareUserFacingPrompt sessionId opts

    let sendThroughPort
        (port: IOpenCodePort)
        (sessionId: SessionId)
        (text: string)
        (sendOptions: OpenCodePromptOptions)
        =
        // Pass the outcome through unchanged. This layer knows less about
        // acceptance than the port does; execution identity is handed off by
        // PromptKey at chat.message -> messages.transform, never by this call stack.
        port.SendPrompt sessionId text sendOptions

    let sendAvailablePort sessionId text sendOptions =
        match underlyingPort with
        | Some port -> sendThroughPort port sessionId text sendOptions
        | None -> Task.FromResult(Fatal "No Host transport: plugin input carried no client, serverUrl, baseUrl or port")

    let sendRoutedPrompt sessionId text opts =
        match routeSendOptions sessionId opts with
        | Error error -> Task.FromResult(Fatal error)
        | Ok sendOptions -> sendAvailablePort sessionId text sendOptions

    let bindSiblingLane (port: IOpenCodePort) (ownerSessionId: SessionId) (laneId: SessionId) (agent: string option) =
        taskResult {
            try
                SessionExecutionBinding.bindInternalRoot laneId agent
                ModelRouting.bindCapacityChild ownerSessionId laneId
                PersonaBinding.ensureInherited ownerSessionId laneId |> ignore
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
            PersonaBinding.ensureInherited parentId childId |> ignore
            // HOST-026: inherit owner/commissioner language (parentId), not family root.
            ProviderLanguageBinding.ensureInherited parentId childId |> ignore
            return childId
        }

    let interruptManagedAttempt (sessionId: SessionId) =
        task {
            Diagnostic.emit "session-attempt-interrupt" [ "session_id", SessionId.value sessionId ]
            SessionExecutionBinding.cancelUnacquired sessionId

            match underlyingPort with
            | Some port -> return! port.AbortSession sessionId
            | None -> return Error "No Host transport: cannot interrupt attempt"
        }

    let abortManagedSession (sessionId: SessionId) =
        task {
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

    interface ISessionHostPort with
        member _.AbortChildren(parentId) = abortChildren parentId

        member me.SubscribeTerminal(sessionId, listener) =
            let token = Guid.NewGuid()

            lock lockObj (fun () ->
                let listeners =
                    match activeListeners.TryGetValue sessionId with
                    | true, current -> current
                    | false, _ ->
                        // DSL-MUTABLE: algorithm-scratch — local created set for batch replay
                        let created = HashSet<Guid>()
                        activeListeners.[sessionId] <- created
                        created

                listeners.Add token |> ignore)

            let sub =
                eventPort.SubscribeTerminalListener(fun sId outcome ->
                    if sId = sessionId then
                        listener sId outcome)

            { new IDisposable with
                member _.Dispose() =
                    sub.Dispose()

                    lock lockObj (fun () ->
                        if removeActiveListenerToken sessionId token then
                            activeListeners.Remove(sessionId) |> ignore) }

        member me.SendPrompt(sessionId, text, opts) =
            let hasListener =
                lock lockObj (fun () ->
                    match activeListeners.TryGetValue sessionId with
                    | true, listeners -> listeners.Count > 0
                    | false, _ -> false)

            if not hasListener then
                // Fatal, not Retryable: the listener is registered by the caller
                // before it sends, so this is a call-order defect. Retrying the
                // same wrong order cannot fix it.
                Task.FromResult(Fatal "AG-LISTENER-BEFORE-SEND: Listener must be registered before sending prompt")
            else
                sendRoutedPrompt sessionId text opts

        member _.InterruptAttempt(sessionId) =
            if not (managedChild sessionId) then
                Task.FromResult(
                    Error "MANAGED-SESSION-016: user-facing/root session may only be interrupted by the external user"
                )
            else
                interruptManagedAttempt sessionId

        member _.TerminateAttempt(sessionId, reason) =
            if not (managedChild sessionId) then
                Task.FromResult(
                    Error "MANAGED-SESSION-016: user-facing/root session may only be interrupted by the external user"
                )
            else
                lock lockObj (fun () -> attemptTerminations.[sessionId] <- reason)
                interruptManagedAttempt sessionId

        member _.TryTakeAttemptTermination(sessionId) =
            lock lockObj (fun () ->
                match attemptTerminations.TryGetValue sessionId with
                | true, reason ->
                    attemptTerminations.Remove sessionId |> ignore
                    Some reason
                | false, _ -> None)

        member me.AbortSession(sessionId) =
            if not (managedChild sessionId) then
                Task.FromResult(
                    Error "MANAGED-SESSION-016: user-facing/root session may only be interrupted by the external user"
                )
            else
                abortManagedSession sessionId

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
