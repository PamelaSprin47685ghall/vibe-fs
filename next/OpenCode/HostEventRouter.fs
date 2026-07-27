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

    let assistantTracker = AssistantTurnTracker()
    let assistantParts = AssistantParts()
    let fallbackFailures = defaultArg recordedErrors (HashSet<string>())
    let abortedSessions = AbortTracker()
    let disposeExecOpt = defaultArg disposeExecutorRuntime (fun _ -> ())
    let onSessionDirectory = defaultArg onSessionDirectory (fun _ _ -> ())

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

    /// Detects abort across the surfaces OpenCode uses:
    ///   1. `session.error` with `error.name = MessageAbortedError | AbortError`
    ///   2. `message.updated` where the assistant message carries
    ///      `info.error.name` or `message.error.name` = `MessageAbortedError | AbortError`.
    /// A pure, standalone function — no side effects, no string-heuristic guesses.
    let isAbortedAssistant (raw: obj) =
        let properties = rawProperties raw

        if isNull properties then
            false
        else
            let errorNameMatches name =
                name = "MessageAbortedError" || name = "AbortError"

            // 1. session.error with error.name
            if eventType raw = "session.error" then
                not (isNull properties?error)
                && not (isNull properties?error?name)
                && errorNameMatches (unbox<string> properties?error?name)
            else
                // 2. message.updated with info.error.name
                (not (isNull properties?info)
                 && not (isNull properties?info?error)
                 && not (isNull properties?info?error?name)
                 && errorNameMatches (unbox<string> properties?info?error?name))
                // 3. message.updated where error surfaces at message level
                || (not (isNull properties?message)
                    && not (isNull properties?message?error)
                    && not (isNull properties?message?error?name)
                    && errorNameMatches (unbox<string> properties?message?error?name))

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

    let terminalRoleOf (takenAssistant: obj option) (sessionId: string) =
        let fromMessage =
            takenAssistant
            |> Option.bind (fun message ->
                if not (isNull message?info) && not (isNull message?info?agent) then
                    HostSessionContext.canonicalRole (unbox<string> message?info?agent)
                else
                    None)

        match fromMessage with
        | Some role -> Some role
        | None ->
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
                    assistantTracker.Record(sessionId, target)
                elif r = "user" then
                    // New user turn id clears a stale assistant; same-id replay
                    // during an in-flight provider call must keep the assistant
                    // id so session.status=retry can attribute FallbackFailure.
                    assistantTracker.NoteUser(sessionId, target)

            if isAbortedAssistant raw then
                abortedSessions.Mark sessionId
                abortChildren sessionId
                // Clear the aborted assistant message so it cannot be
                // consumed by a terminal event — aborted state alone is
                // sufficient to block nudges, but clearing prevents any
                // other path from accessing it.
                assistantTracker.ClearCurrent sessionId
            elif eventType raw = "session.error" then
                ()
            elif eventType raw = "session.status" then
                // Provider retry is the ONLY durable fallback writer (SSOT §6).
                // Aborted sessions are torn-down transports, not model failures.
                if not (abortedSessions.Contains sessionId) then
                    let lastAssistantMsgId = assistantTracker.LastMessageId sessionId

                    HostEventRetry.record journal fallbackFailures lastAssistantMsgId sessionId raw

            abortedSessions.Observe(raw, sessionId)

            if isTerminalEvent raw then
                disposeExecOpt sessionId

                if eventType raw = "session.aborted" then
                    abortedSessions.Mark sessionId
                    abortChildren sessionId

                let aborted = abortedSessions.Contains sessionId

                let takenAssistant = assistantTracker.TakeTerminal sessionId

                let terminalMessageId =
                    AssistantTurnTracker.terminalMessageId sessionId takenAssistant

                let terminalModel = AssistantTurnTracker.terminalModel takenAssistant

                let completedAssistant =
                    takenAssistant
                    |> Option.bind (fun lastMsg -> assistantParts.TryHydrate(terminalMessageId, lastMsg))

                HostTerminalHandler.handle
                    sessionPort
                    journal
                    gitTreePort
                    verdictSessions
                    nudgeSent
                    managerGuardNudges
                    nudgeContinue
                    sessionParents
                    aborted
                    sessionId
                    takenAssistant
                    completedAssistant
                    terminalMessageId
                    terminalModel
                    (terminalRoleOf takenAssistant sessionId)

                // Retire the terminal message ID regardless of whether parts
                // were found — late-arriving parts must be rejected even when
                // the message had no observable parts (aborted, unknown, empty).
                assistantParts.Retire terminalMessageId

                completedAssistant
                |> Option.iter (fun _ -> assistantParts.Remove terminalMessageId)

        forward raw
