namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core.JsInterop
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Process

/// Keeps host event facts at the adapter boundary: session identity, parentage,
/// role, terminal nudge, and parent-abort propagation.
type HostEventRouter
    (
        sessionPort: ISessionHostPort,
        sessionParents: Dictionary<string, string>,
        sessionRoles: Dictionary<string, string>,
        verdictSessions: HashSet<string>,
        nudgeSent: HashSet<string>,
        ?journal: AgentJournal,
        ?gitTreePort: GitTreePort,
        ?recordedErrors: HashSet<string>,
        ?disposeExecutorRuntime: (string -> unit),
        ?onSessionDirectory: string -> string -> unit
    ) =

    /// Zero-width space: invisible nudge that prompts LLM to continue.
    let ZWSP = "​"

    let lastAssistantMsgs = Dictionary<string, obj>()
    let assistantParts = AssistantParts()
    let fallbackFailures = defaultArg recordedErrors (HashSet<string>())
    let retryAttemptBySession = Dictionary<string, string>()
    /// Sessions whose in-flight provider request failed due to host shutdown
    /// (mocking stopped, connection reset). Their empty/xml terminal is a
    /// transport artifact, NOT a model failure — observeIdle must skip them.
    let hostShutdownSessions = HashSet<string>()
    let abortedSessions = AbortTracker()
    let disposeExecOpt = defaultArg disposeExecutorRuntime (fun _ -> ())
    let onSessionDirectory = defaultArg onSessionDirectory (fun _ _ -> ())

    /// A session is dead after 4 consecutive fallback failures (SSOT §6:
    /// DurableFallback.nextDecision = FallbackDecision.Dead). Dead sessions must
    /// not receive internal nudges — the router has no diagnostics channel, so a
    /// dead session is skipped silently at the prompt-send sites below.
    let sessionDead (sessionId: string) : bool =
        match journal with
        | Some j ->
            j.IsPoisoned
            || DurableFallback.isDead (SessionId.create sessionId) (AgentJournal.snapshot j)
        | None -> false

    let rawEvent (raw: obj) =
        if isNull raw || isNull raw?event then raw else raw?event

    let rawProperties (raw: obj) =
        let event = rawEvent raw
        if isNull event then null else event?properties

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

    let eventType (raw: obj) =
        let event = rawEvent raw

        if isNull event || isNull event?``type`` then
            ""
        else
            unbox<string> event?``type``

    let isTerminalEvent (raw: obj) =
        match eventType raw with
        | "session.idle"
        | "session.aborted" -> true
        | _ -> false

    /// Real OpenCode has no session.aborted event: an abort surfaces as
    /// session.error with name MessageAbortedError, followed by idle.
    let isAbortError (raw: obj) =
        if eventType raw <> "session.error" then
            false
        else
            let properties = rawProperties raw

            not (isNull properties)
            && not (isNull properties?error)
            && not (isNull properties?error?name)
            && unbox<string> properties?error?name = "MessageAbortedError"

    let abortChildren parentId =
        Pty.abortParent parentId
        sessionPort.AbortChildren(SessionId.create parentId) |> ignore
        disposeExecOpt parentId


    let managerGuardNudges = HashSet<string>()

    let nudgedMessages = HashSet<string>()
    let nudgeCounts = Dictionary<string, int>()
    let maxNudgesPerSession = 3

    let nudgeContinue (sessionId: string) (msgId: string) =
        if nudgedMessages.Add msgId then
            let count =
                match nudgeCounts.TryGetValue sessionId with
                | true, c -> c
                | false, _ -> 0

            if count < maxNudgesPerSession then
                nudgeCounts.[sessionId] <- count + 1

                HostSessionNudge.send
                    sessionPort
                    (SessionId.create sessionId)
                    ZWSP
                    { Model = None
                      Agent = None
                      Directory = None }
                    ignore
                    journal

    let terminalRole sessionId =
        match lastAssistantMsgs.TryGetValue sessionId with
        | true, message when not (isNull message?info) && not (isNull message?info?agent) ->
            HostSessionContext.canonicalRole (unbox<string> message?info?agent)
        | _ ->
            match sessionRoles.TryGetValue sessionId with
            | true, role -> Some role
            | false, _ -> None

    member _.Observe(raw: obj, forward: obj -> unit) =
        let sessionId, role = HostSessionContext.read raw

        if not (String.IsNullOrWhiteSpace sessionId) then
            HostEventDirectory.rawDirectory raw
            |> Option.iter (fun directory ->
                if not (String.IsNullOrWhiteSpace directory) then
                    onSessionDirectory sessionId directory)

            // Event info.agent is the *resolved* OpenCode agent; a fallback
            // (build/plan/title) must never clobber a known DSL role.
            role
            |> Option.bind HostSessionContext.canonicalRole
            |> Option.iter (fun value -> sessionRoles.[sessionId] <- value)

            // Only session.created carries a session parent. Message payloads also
            // expose parentID (message parent), which must never mark a top-level
            // Manager as a child — that skips ReviewGuard entirely.
            if eventType raw = "session.created" then
                rawParentSessionId raw
                |> Option.iter (fun parentId ->
                    if not (String.IsNullOrWhiteSpace parentId) then
                        sessionParents.[sessionId] <- parentId)

            let ev = if isNull raw?event then raw else raw?event
            let props = if isNull ev then null else ev?properties
            let msg = if isNull props then null else props?message
            let target = if isNull msg then props else msg

            assistantParts.Record props

            if not (isNull target) then
                let info = target?info

                let r =
                    if not (isNull info) && not (isNull info?role) then
                        unbox<string> info?role
                    elif not (isNull target?role) then
                        unbox<string> target?role
                    else
                        ""

                if r = "assistant" then
                    lastAssistantMsgs.[sessionId] <- target

                    // MessageAbortedError on a message.updated event is a host-
                    // shutdown artifact (the session's transport was torn down),
                    // not a model failure. Mark it so observeIdle skips the
                    // empty/xml terminal.
                    if
                        not (isNull info)
                        && not (isNull info?error)
                        && not (isNull info?error?name)
                        && unbox<string> info?error?name = "MessageAbortedError"
                    then
                        hostShutdownSessions.Add sessionId |> ignore
                elif r = "user" then
                    retryAttemptBySession.Remove sessionId |> ignore

            if isAbortError raw then
                abortedSessions.Mark sessionId
                abortChildren sessionId
            elif eventType raw = "session.error" then
                ()
            elif eventType raw = "session.status" then
                // Fail-closed on shutdown: a provider retry observed while the
                // session is being torn down is a host-shutdown artifact, not a
                // model failure, and must not poison the durable fallback budget
                // (which would otherwise mark the session Dead and break restart
                // recovery). HostEventRetry.record also skips the harness stop
                // sentinel as a second line of defense.
                if not (abortedSessions.Contains sessionId) then
                    let lastAssistantMsgId =
                        match lastAssistantMsgs.TryGetValue sessionId with
                        | true, lastMsg -> FallbackDetect.messageId sessionId lastMsg
                        | false, _ -> ""

                    HostEventRetry.record
                        journal
                        fallbackFailures
                        retryAttemptBySession
                        hostShutdownSessions
                        lastAssistantMsgId
                        sessionId
                        raw

            abortedSessions.Observe(raw, sessionId)

            if isTerminalEvent raw then
                disposeExecOpt sessionId

                if eventType raw = "session.aborted" then
                    abortedSessions.Mark sessionId
                    abortChildren sessionId

                let aborted = abortedSessions.Contains sessionId

                let terminalMessageId =
                    match lastAssistantMsgs.TryGetValue sessionId with
                    | true, lastMsg -> FallbackDetect.messageId sessionId lastMsg
                    | false, _ -> "terminal"

                let terminalModel =
                    match lastAssistantMsgs.TryGetValue sessionId with
                    | true, lastMsg when not (isNull lastMsg?info) ->
                        let info = lastMsg?info

                        if not (isNull info?providerID) && not (isNull info?modelID) then
                            Some
                                { providerID = unbox<string> info?providerID
                                  modelID = unbox<string> info?modelID
                                  variant = None }
                        elif not (isNull info?model) && not (isNull info?model?providerID) then
                            Some
                                { providerID = unbox<string> info?model?providerID
                                  modelID = unbox<string> info?model?modelID
                                  variant = None }
                        else
                            None
                    | _ -> None

                let completedAssistant =
                    match lastAssistantMsgs.TryGetValue sessionId with
                    | true, lastMsg -> Some(assistantParts.Hydrate(terminalMessageId, lastMsg))
                    | false, _ -> None

                let hasTerminalAssistant =
                    completedAssistant |> Option.exists FallbackDetect.isTerminalAssistant

                if not aborted && not (hostShutdownSessions.Contains sessionId) then
                    // Record the terminal failure before any internal nudge. The
                    // fourth failed terminal therefore becomes Dead before the
                    // guard/continuation sites consult the durable projection.
                    match completedAssistant with
                    | Some completeMsg when hasTerminalAssistant ->
                        FallbackDetect.observeIdle journal fallbackFailures retryAttemptBySession sessionId completeMsg
                    | _ -> ()

                    match terminalRole sessionId with
                    | Some agent when
                        agent.Equals("reviewer", StringComparison.OrdinalIgnoreCase)
                        && not (verdictSessions.Remove sessionId)
                        ->
                        if not (sessionDead sessionId) then
                            HostReviewGuard.nudgeReviewer
                                sessionPort
                                journal
                                nudgeSent
                                sessionId
                                terminalMessageId
                                terminalModel
                    | Some agent when
                        agent.Equals("manager", StringComparison.OrdinalIgnoreCase)
                        && not (sessionParents.ContainsKey sessionId)
                        ->
                        // Every unconfirmed manager terminal re-evaluates the guard.
                        // Send is deferred one microtask so Host idle is fully released;
                        // failed sends do not lock the guard key, so the next terminal retries.
                        if not (sessionDead sessionId) then
                            match HostReviewGuard.missingTree journal gitTreePort sessionId with
                            | HostReviewGuard.ReviewGuardMissing treeHash ->
                                HostReviewGuard.nudgeManager
                                    sessionPort
                                    journal
                                    managerGuardNudges
                                    sessionId
                                    terminalMessageId
                                    treeHash
                                    terminalModel
                            | HostReviewGuard.ReviewGuardConfirmed -> ()
                            | HostReviewGuard.ReviewGuardUnavailable reason ->
                                raise (InvalidOperationException(sprintf "Review guard unavailable: %s" reason))
                    | _ -> ()

                    match completedAssistant with
                    | Some completeMsg when hasTerminalAssistant && FallbackDetect.isFailedAssistant completeMsg ->
                        if not (sessionDead sessionId) then
                            nudgeContinue sessionId terminalMessageId
                    | _ -> ()

                completedAssistant
                |> Option.iter (fun _ -> assistantParts.Remove terminalMessageId)

        forward raw
