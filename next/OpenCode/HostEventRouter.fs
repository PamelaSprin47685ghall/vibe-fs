namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core.JsInterop
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Kernel.Identity

/// Keeps host event facts at the adapter boundary: session identity, parentage,
/// role, terminal nudge, and parent-abort propagation.
type HostEventRouter
    (
        sessionPort: ISessionHostPort,
        sessionParents: Dictionary<string, string>,
        sessionRoles: Dictionary<string, string>,
        verdictSessions: HashSet<string>,
        nudgeSent: HashSet<string>
    ) =

    let mutable latestSessionId = ""

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

    let nudgeReviewer sessionId =
        if nudgeSent.Add sessionId then
            sessionPort.SendPrompt(
                SessionId.create sessionId,
                "Submit a structured verdict with the verdict tool: PERFECT or REVISE. Do not put a verdict in prose.",
                { Model = None
                  Agent = Some "reviewer" }
            )
            |> ignore

    member _.LatestSessionId = latestSessionId

    member _.Observe(raw: obj, forward: obj -> unit) =
        let sessionId, role = HostSessionContext.read raw

        if not (String.IsNullOrWhiteSpace sessionId) then
            latestSessionId <- sessionId
            // Event info.agent is the *resolved* OpenCode agent; a fallback
            // (build/plan/title) must never clobber a known DSL role.
            role
            |> Option.filter (fun value -> HostSessionContext.roleOf value |> Option.isSome)
            |> Option.iter (fun value -> sessionRoles.[sessionId] <- value)

            rawParentSessionId raw
            |> Option.iter (fun parentId ->
                if not (String.IsNullOrWhiteSpace parentId) then
                    sessionParents.[sessionId] <- parentId)

            if isAbortError raw then
                abortChildren sessionId

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

        forward raw
