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
        ?recordedErrors: HashSet<string>
    ) =

    /// Zero-width space: invisible nudge that prompts LLM to continue.
    let ZWSP = "​"

    let lastAssistantMsgs = Dictionary<string, obj>()
    let assistantParts = AssistantParts()
    let fallbackFailures = defaultArg recordedErrors (HashSet<string>())
    let retryAttempts = Dictionary<string, HashSet<string>>()
    let abortedSessions = AbortTracker()

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


    let recordRetryFailure sessionId raw =
        let properties = rawProperties raw
        let status = if isNull properties then null else properties?status

        if not (isNull status) && not (isNull status?``type``) && status?``type`` = "retry" then
            let attempt =
                if isNull status?attempt then
                    "unknown"
                else
                    string status?attempt

            let seenAttempts =
                match retryAttempts.TryGetValue sessionId with
                | true, values -> values
                | false, _ ->
                    let values = HashSet<string>()
                    retryAttempts.[sessionId] <- values
                    values

            if seenAttempts.Add attempt then
                match journal with
                | None -> ()
                | Some journal ->
                    let reason =
                        if isNull status?message then
                            "provider retry"
                        else
                            unbox<string> status?message

                    let fact =
                        AgentFact.FallbackFailureRecorded
                            {| SessionId = SessionId.create sessionId
                               Reason = reason |}

                    AgentJournal.appendAgent (StreamId.Session(SessionId.create sessionId)) None fact journal
                    |> ignore

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
                    { Model = None; Agent = None }
                    ignore

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
            // Event info.agent is the *resolved* OpenCode agent; a fallback
            // (build/plan/title) must never clobber a known DSL role.
            role
            |> Option.bind HostSessionContext.canonicalRole
            |> Option.iter (fun value -> sessionRoles.[sessionId] <- value)

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
                elif r = "user" then
                    retryAttempts.Remove sessionId |> ignore

            if isAbortError raw then
                abortedSessions.Mark sessionId
                abortChildren sessionId
            elif eventType raw = "session.error" then
                ()
            elif eventType raw = "session.status" then
                recordRetryFailure sessionId raw

            abortedSessions.Observe(raw, sessionId)

            if isTerminalEvent raw then
                if eventType raw = "session.aborted" then
                    abortedSessions.Mark sessionId
                    abortChildren sessionId

                let aborted = abortedSessions.Contains sessionId

                let terminalMessageId =
                    match lastAssistantMsgs.TryGetValue sessionId with
                    | true, lastMsg -> FallbackDetect.messageId lastMsg
                    | false, _ -> "terminal"

                let completedAssistant =
                    match lastAssistantMsgs.TryGetValue sessionId with
                    | true, lastMsg -> Some(assistantParts.Hydrate(terminalMessageId, lastMsg))
                    | false, _ -> None

                let hasTerminalAssistant =
                    completedAssistant |> Option.exists FallbackDetect.isTerminalAssistant

                if not aborted then
                    match terminalRole sessionId with
                    | Some agent when
                        agent.Equals("reviewer", StringComparison.OrdinalIgnoreCase)
                        && not (verdictSessions.Remove sessionId)
                        ->
                        HostReviewGuard.nudgeReviewer sessionPort journal nudgeSent sessionId terminalMessageId
                    | Some agent when agent.Equals("manager", StringComparison.OrdinalIgnoreCase) ->
                        match HostReviewGuard.missingTree journal gitTreePort sessionId with
                        | HostReviewGuard.ReviewGuardMissing treeHash ->
                            HostReviewGuard.nudgeManager
                                sessionPort
                                journal
                                managerGuardNudges
                                sessionId
                                terminalMessageId
                                treeHash
                        | HostReviewGuard.ReviewGuardConfirmed -> ()
                        | HostReviewGuard.ReviewGuardUnavailable reason ->
                            raise (InvalidOperationException(sprintf "Review guard unavailable: %s" reason))
                    | _ -> ()

                    match completedAssistant with
                    | Some completeMsg when hasTerminalAssistant ->
                        FallbackDetect.observeIdle journal fallbackFailures sessionId completeMsg

                        if FallbackDetect.isFailedAssistant completeMsg then
                            nudgeContinue sessionId terminalMessageId
                    | _ -> ()

                completedAssistant
                |> Option.iter (fun _ -> assistantParts.Remove terminalMessageId)

        forward raw
