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

    let private textFromParts (parts: obj array) = CompletedTurnClassifier.partsText parts

    let private latestUserPromptText
        (snapshot: ISessionSnapshotPort option)
        (sessionId: string)
        (preferredMessageId: string option)
        =
        task {
            match snapshot with
            | None -> return None
            | Some port ->
                let! messagesResult = port.GetMessages(SessionId.create sessionId)

                match messagesResult with
                | Error _ -> return None
                | Ok messages ->
                    let users = messages |> List.filter (fun message -> message.Role = "user")

                    let preferred =
                        match preferredMessageId with
                        | Some mid when not (String.IsNullOrWhiteSpace mid) ->
                            users
                            |> List.tryFind (fun message -> MessageId.value message.Id = mid)
                            |> Option.bind (fun message ->
                                let text = textFromParts message.Parts
                                if String.IsNullOrWhiteSpace text then None else Some text)
                        | _ -> None

                    match preferred with
                    | Some text -> return Some text
                    | None ->
                        return
                            users
                            |> List.rev
                            |> List.tryPick (fun message ->
                                let text = textFromParts message.Parts
                                if String.IsNullOrWhiteSpace text then None else Some text)
        }

    let private authorityAgent (journal: AgentJournal option) (sessionId: string) : string option =
        match journal with
        | None -> None
        | Some j ->
            let sid = SessionId.create sessionId
            let snapshot = AgentJournal.snapshot j

            PromptAuthorityLedger.activeProfile sid snapshot.AgentProjections
            |> Option.map (fun profile -> PromptAuthority.roleLabel profile.CanonicalRole)

    let create
        (sessionParents: Dictionary<string, string>)
        (sessionRoles: Dictionary<string, string>)
        (currentPhysicalUserMessage: string -> string option)
        (journal: AgentJournal option)
        (gitTreePortFor: string -> GitTreePort option)
        (reviewerHosts: Dictionary<string, ReviewerHost>)
        (verdictSessions: HashSet<string>)
        (snapshot: ISessionSnapshotPort option)
        : (obj -> obj -> Task<obj>) =
        let gate = obj ()

        fun (args: obj) (ctx: obj) ->
            task {
                let sid = contextString ctx "sessionID"

                // Authority is SSOT for role gating. Host tool context agent is the only
                // non-authority input; sessionRoles is never consulted.
                let role =
                    match sid with
                    | Some id ->
                        authorityAgent journal id
                        |> Option.orElse (contextString ctx "agent" |> Option.bind HostSessionContext.canonicalRole)
                    | None -> contextString ctx "agent" |> Option.bind HostSessionContext.canonicalRole

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

                        // Physical confirmation: pass both prompt text (diagnostics)
                        // and the current physical user message id (authorization).
                        let physicalUserMessageId =
                            currentPhysicalUserMessage reviewerId
                            |> Option.orElse (contextString ctx "userMessageID")
                            |> Option.orElse (contextString ctx "userMessageId")

                        let! userPromptText =
                            task {
                                let fromParts =
                                    if isNull ctx || isNull ctx?message || isNull ctx?message?parts then
                                        None
                                    else
                                        try
                                            let text = textFromParts (unbox<obj array> ctx?message?parts)
                                            if String.IsNullOrWhiteSpace text then None else Some text
                                        with _ ->
                                            None

                                match
                                    fromParts
                                    |> Option.orElse (contextString ctx "prompt")
                                    |> Option.orElse (contextString ctx "input")
                                with
                                | Some text -> return Some text
                                | None -> return! latestUserPromptText snapshot reviewerId physicalUserMessageId
                            }

                        match providerRunId with
                        | None -> return box (stringify (createObj [ "error", box "Missing ProviderRunId" ]))
                        | Some providerRunId ->
                            match
                                host.SubmitVerdict(
                                    toolCallId,
                                    verdict,
                                    providerRunId,
                                    ?userPromptText = userPromptText,
                                    ?userMessageId = physicalUserMessageId
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
