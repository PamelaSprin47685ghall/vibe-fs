namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core.JsInterop
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

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
        ?recordedErrors: HashSet<string>
    ) =

    /// Zero-width space: invisible nudge that prompts LLM to continue.
    let ZWSP = "​"

    let lastAssistantMsgs = Dictionary<string, obj>()

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
        sessionPort.AbortChildren(SessionId.create parentId) |> ignore

    let errorReason (raw: obj) =
        let properties = rawProperties raw

        if not (isNull properties) && not (isNull properties?error) then
            let err = properties?error
            let name = if isNull err?name then "" else unbox<string> err?name
            let message = if isNull err?message then "" else unbox<string> err?message

            if String.IsNullOrWhiteSpace name then
                message
            else
                sprintf "%s: %s" name message
        else
            "unknown provider error"

    let recordProviderError sessionId raw =
        match journal with
        | None -> ()
        | Some journal ->
            let fact =
                AgentFact.FallbackFailureRecorded
                    {| SessionId = SessionId.create sessionId
                       Reason = errorReason raw |}

            AgentJournal.appendAgent (StreamId.Session(SessionId.create sessionId)) None fact journal
            |> ignore

    let nudgeReviewer sessionId =
        if nudgeSent.Add sessionId then
            sessionPort.SendPrompt(
                SessionId.create sessionId,
                "Submit a structured verdict with the verdict tool: PERFECT or REVISE. Do not put a verdict in prose.",
                { Model = None
                  Agent = Some "reviewer" }
            )
            |> ignore

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

                sessionPort.SendPrompt(SessionId.create sessionId, ZWSP, { Model = None; Agent = None })
                |> ignore

    let getPartsArray (msg: obj) : obj array option =
        if isNull msg then
            None
        elif not (isNull msg?parts) then
            Some(unbox<obj array> msg?parts)
        elif not (isNull msg?properties) && not (isNull msg?properties?parts) then
            Some(unbox<obj array> msg?properties?parts)
        else
            None

    member _.Observe(raw: obj, forward: obj -> unit) =
        let sessionId, role = HostSessionContext.read raw

        if not (String.IsNullOrWhiteSpace sessionId) then
            // Event info.agent is the *resolved* OpenCode agent; a fallback
            // (build/plan/title) must never clobber a known DSL role.
            role
            |> Option.filter (fun value -> HostSessionContext.roleOf value |> Option.isSome)
            |> Option.iter (fun value -> sessionRoles.[sessionId] <- value)

            rawParentSessionId raw
            |> Option.iter (fun parentId ->
                if not (String.IsNullOrWhiteSpace parentId) then
                    sessionParents.[sessionId] <- parentId)

            let ev = if isNull raw?event then raw else raw?event
            let props = if isNull ev then null else ev?properties
            let msg = if isNull props then null else props?message
            let target = if isNull msg then props else msg

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
                    match getPartsArray target with
                    | Some _ -> lastAssistantMsgs.[sessionId] <- target
                    | None ->
                        if not (lastAssistantMsgs.ContainsKey sessionId) then
                            lastAssistantMsgs.[sessionId] <- target

            if isAbortError raw then
                abortChildren sessionId
            elif eventType raw = "session.error" then
                recordProviderError sessionId raw

            if isTerminalEvent raw then
                if eventType raw = "session.aborted" then
                    abortChildren sessionId

                match sessionRoles.TryGetValue sessionId with
                | true, agent when
                    agent.Equals("reviewer", StringComparison.OrdinalIgnoreCase)
                    && not (verdictSessions.Contains sessionId)
                    ->
                    nudgeReviewer sessionId
                | _ -> ()

                match lastAssistantMsgs.TryGetValue sessionId with
                | true, lastMsg ->
                    FallbackDetect.observeIdle journal (defaultArg recordedErrors (HashSet<string>())) sessionId lastMsg

                    if FallbackDetect.isFailedAssistant lastMsg then
                        let msgId = FallbackDetect.messageId lastMsg
                        nudgeContinue sessionId msgId
                | false, _ -> ()

        forward raw
