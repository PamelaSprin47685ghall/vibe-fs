namespace Wanxiangshu.Next.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools

module VerdictSurface =

    [<Emit("JSON.stringify($0)")>]
    let private stringify (value: obj) : string = jsNative

    let private contextString (ctx: obj) (name: string) =
        if isNull ctx || isNull ctx?(name) then
            None
        else
            let v = unbox<string> ctx?(name) in if String.IsNullOrWhiteSpace v then None else Some v

    let private mkSid (s: string) = SessionId.create s

    let create
        (sessionParents: Dictionary<string, string>)
        (sessionRoles: Dictionary<string, string>)
        (journal: AgentJournal option)
        (gitTreePortFor: string -> GitTreePort option)
        (reviewerHosts: Dictionary<string, ReviewerHost>)
        (verdictSessions: HashSet<string>)
        : (obj -> obj -> Task<obj>) =
        let gate = obj ()

        fun (args: obj) (ctx: obj) ->
            task {
                let sid = contextString ctx "sessionID"

                let role =
                    contextString ctx "agent"
                    |> Option.orElseWith (fun () ->
                        sid
                        |> Option.bind (fun id ->
                            match sessionRoles.TryGetValue id with
                            | true, v -> Some v
                            | false, _ -> None))

                let callId =
                    contextString ctx "toolCallId" |> Option.orElse (contextString ctx "callID")

                let vResult =
                    if
                        role
                        |> Option.exists (fun v -> not (v.Equals("reviewer", StringComparison.OrdinalIgnoreCase)))
                    then
                        Error "The verdict tool is available only to reviewer sessions"
                    elif sid.IsNone then
                        Error "Missing sessionID"
                    elif callId.IsNone then
                        Error "Missing tool call id"
                    elif isNull args || isNull args?verdict then
                        Error "Missing verdict"
                    else
                        try
                            StaticTools.reviewerVerdictOfString (unbox<string> args?verdict)
                        with _ ->
                            Error "verdict must be exactly PERFECT or REVISE"

                match vResult, sid, callId with
                | Error err, _, _ -> return box (stringify (createObj [ "error", box err ]))
                | Ok verdict, Some reviewerId, Some toolCallId ->
                    let mgrId =
                        match sessionParents.TryGetValue reviewerId with
                        | true, p -> Some p
                        | false, _ ->
                            // Worktree plugin instances do not share the in-memory
                            // sessionParents map with the orchestrator root. Recover
                            // the manager/orchestrator parent from durable linkage.
                            match journal with
                            | None -> None
                            | Some j ->
                                let child = ChildId.create reviewerId

                                (AgentJournal.snapshot j).AgentProjections.Sessions
                                |> Map.tryPick (fun parentId session ->
                                    match session.Linkage with
                                    | Some linkage when Map.containsKey child linkage.LinkedChildren ->
                                        Some(SessionId.value parentId)
                                    | _ -> None)

                    match journal, mgrId, gitTreePortFor reviewerId with
                    | None, _, _ ->
                        return box (stringify (createObj [ "error", box "Reviewer verdict requires a journal" ]))
                    | _, None, _ -> return box (stringify (createObj [ "error", box "Missing manager session" ]))
                    | _, _, None ->
                        return box (stringify (createObj [ "error", box "Reviewer verdict requires a GitTreePort" ]))
                    | Some j, Some mId, Some gtp ->
                        let host =
                            lock gate (fun () ->
                                match reviewerHosts.TryGetValue reviewerId with
                                | true, h -> h
                                | false, _ ->
                                    let h = ReviewerHost(j, mkSid mId, mkSid reviewerId, ?gitTreePort = Some gtp)
                                    reviewerHosts.[reviewerId] <- h
                                    h)

                        let providerRunId =
                            contextString ctx "messageID" |> Option.orElse (contextString ctx "messageId")

                        // Root user message for this run (confirmation identity for 2nd PERFECT).
                        // Host may expose it under several keys; also walk ctx.message if nested.
                        let rootUserMessageId =
                            contextString ctx "userMessageId"
                            |> Option.orElse (contextString ctx "userMessageID")
                            |> Option.orElse (contextString ctx "parentMessageID")
                            |> Option.orElse (contextString ctx "message.parentID")
                            |> Option.orElse (
                                if isNull ctx || isNull ctx?message then
                                    None
                                elif not (isNull ctx?message?parentID) then
                                    Some(unbox<string> ctx?message?parentID)
                                elif
                                    not (isNull ctx?message?id)
                                    && not (isNull ctx?message?role)
                                    && unbox<string> ctx?message?role = "user"
                                then
                                    Some(unbox<string> ctx?message?id)
                                else
                                    None
                            )

                        match providerRunId with
                        | None -> return box (stringify (createObj [ "error", box "Missing ProviderRunId" ]))
                        | Some providerRunId ->
                            match
                                host.SubmitVerdict(
                                    toolCallId,
                                    verdict,
                                    providerRunId,
                                    ?rootUserMessageId = rootUserMessageId
                                )
                            with
                            | Error err -> return box (stringify (createObj [ "error", box err ]))
                            | Ok result ->
                                lock gate (fun () -> verdictSessions.Add reviewerId |> ignore)

                                let status =
                                    match result with
                                    | ReviewFinishResult.Confirmed -> "CONFIRMED"
                                    | ReviewFinishResult.NeedsReview -> "NEEDS_REVIEW"

                                let vText =
                                    if verdict = ReviewGuardVerdict.Perfect then
                                        "PERFECT"
                                    else
                                        "REVISE"

                                return box (stringify (createObj [ "verdict", box vText; "status", box status ]))
                | Ok _, _, _ -> return box (stringify (createObj [ "error", box "Missing reviewer tool context" ]))
            }
