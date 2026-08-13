namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Outcome
open Wanxiangshu.Kernel

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
    abstract AbortChildren: parentId: SessionId -> Task
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

    let familyRoot (sessionId: SessionId) =
        match lock lockObj (fun () -> childParents.TryGetValue sessionId) with
        | true, rootId -> rootId
        | false, _ ->
            let rec findRoot current =
                match restoredParent current with
                | Some parentId when parentId <> current -> findRoot parentId
                | _ -> current

            findRoot sessionId

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

    let getAndRemoveChildren (parentId: SessionId) =
        lock lockObj (fun () ->
            if parentChildMap.ContainsKey parentId then
                let children = parentChildMap.[parentId] |> Seq.toList
                parentChildMap.Remove parentId |> ignore

                for childId in children do
                    childParents.Remove childId |> ignore

                children
            else
                [])

    let detachChild (childId: SessionId) =
        lock lockObj (fun () ->
            match childParents.TryGetValue childId with
            | true, rootId ->
                childParents.Remove childId |> ignore

                if parentChildMap.ContainsKey rootId then
                    parentChildMap.[rootId].Remove childId |> ignore

                    if parentChildMap.[rootId].Count = 0 then
                        parentChildMap.Remove rootId |> ignore
            | false, _ -> ())

    let abortChildren (parentId: SessionId) =
        task {
            let children = getAndRemoveChildren parentId

            for childId in children do
                match underlyingPort with
                | Some port ->
                    let! _ = port.AbortSession childId
                    ()
                | None -> ()

                ()
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
                    let normalized =
                        if managedChild sessionId then
                            SessionExecutionBinding.normalizeManagedPrompt sessionId opts
                        else
                            SessionExecutionBinding.normalizeUserFacingPrompt sessionId opts

                    match normalized with
                    | Error error -> return Fatal error
                    | Ok sendOptions ->
                        match underlyingPort with
                        | Some port ->
                            // chat.params runs inside the Host send. Mark this interval so
                            // its root-session observer cannot mistake our own continuation
                            // or typed override for a new external user choice.
                            SessionExecutionBinding.beginInternalSend sessionId

                            try
                                // Pass the outcome through unchanged. This layer knows less
                                // about acceptance than the port does; narrowing here is how
                                // AcceptanceUnknown used to become a plain error.
                                return! port.SendPrompt sessionId text sendOptions
                            finally
                                SessionExecutionBinding.endInternalSend sessionId
                        | None ->
                            // No Host transport was resolved from the plugin input. The
                            // previous code fabricated a completed AgentRunResult with
                            // "test output" here, which made a misconfigured runtime
                            // indistinguishable from a finished agent.
                            return Fatal "No Host transport: plugin input carried no client, serverUrl, baseUrl or port"
            }

        member me.AbortSession(sessionId) =
            task {
                // Who killed a turn is the first question a stalled run raises, and the answer used
                // to be nowhere: every abort path was silent, so an interrupted tool call looked
                // identical to a model that simply stopped. One record per abort, visible under
                // WANXIANGSHU_DIAG=1.
                Diagnostic.emit "session-abort" [ "session_id", SessionId.value sessionId ]
                detachChild sessionId
                do! abortChildren sessionId

                match underlyingPort with
                | Some port ->
                    let! _ = port.AbortSession(sessionId)
                    ()
                | None -> ()

                return Ok()
            }

        member me.CreateChildSession(parentId, options) =
            task {
                let rootId = familyRoot parentId
                // HOST-015: every managed child is physically parented to the
                // family root — a son's son is a son. Recovery proves ownership
                // by the journal-linked SessionId + agent/title, never by the
                // Host parentID.
                let hostParentId = rootId

                match underlyingPort with
                | Some port ->
                    let! res = port.CreateChildSession hostParentId options

                    match res with
                    | Ok childId ->
                        registerChild rootId childId
                        SessionExecutionBinding.bind parentId childId options.Agent

                        // HOST-026: inherit owner/commissioner language (parentId), not family root.
                        ProviderLanguageBinding.ensureInherited parentId childId |> ignore
                        return Ok childId
                    | Error err -> return Error err
                | None ->
                    // Same defect class as the SendPrompt None branch: minting a
                    // SessionId the Host never issued hands the caller an identity
                    // that every later operation silently no-ops against.
                    return Error "No Host transport: cannot create a child session"
            }

        member _.ListChildren(parentId) =
            match underlyingPort with
            | Some port -> port.ListChildren parentId
            | None -> Task.FromResult(Error "No Host transport: cannot list child sessions")

        member _.FamilyRootOf(sessionId) = familyRoot sessionId
