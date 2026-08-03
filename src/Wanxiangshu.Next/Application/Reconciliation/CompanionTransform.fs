namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core.JsInterop
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools
open Wanxiangshu.Next.Domain
open CompanionProjection

module CompanionTransform =

    let handleCompanionTransform
        (companions: Dictionary<string, CompanionHost>)
        (gate: obj)
        (scope: PluginRuntimeScope)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (onBloggerCreated: (SessionId -> unit) option)
        (workspaceDirectory: string option)
        (inObj: obj)
        (rawOutObj: obj)
        =
        let rawMessages = unbox<obj array> rawOutObj?messages |> Array.toList

        // COMPANION-013 idempotency: never stack a second synthetic head on a
        // message array that already carries one.
        //
        // This used to be load-bearing for a real defect: the plugin registered the
        // transform under two hook names, so both fired over the same array. That is
        // fixed at the registration site (HOST-009), and the guard remains as the
        // invariant itself — one B head per request view, whoever calls.
        let alreadyHasBHead =
            rawMessages
            |> List.exists (fun message ->
                not (isNull message)
                && not (isNull message?info)
                && not (isNull message?info?id)
                && (unbox<string> message?info?id).StartsWith("companion-b-head"))

        let messageContext =
            rawMessages
            |> List.tryPick (fun message ->
                if isNull message || isNull message?info then
                    None
                else
                    let messageSessionId =
                        if isNull message?info?sessionID then
                            None
                        else
                            Some(unbox<string> message?info?sessionID)

                    let role =
                        if isNull message?info?agent then
                            None
                        else
                            Some(unbox<string> message?info?agent)

                    Some(messageSessionId, role))

        match messageContext with
        | Some(Some messageSessionId, _) when not (isNull inObj) && isNull inObj?sessionID ->
            inObj?sessionID <- messageSessionId
        | _ -> ()

        let sessionId =
            if isNull inObj || isNull inObj?sessionID then
                ""
            else
                unbox<string> inObj?sessionID

        if
            not alreadyHasBHead
            && not (String.IsNullOrWhiteSpace sessionId)
            && not (isNull rawOutObj?messages)
        then
            // COMPANION-001 / COMPANION-002: every managed work session has a Y, and
            // the only thing that must not have one is a Y itself. So the question
            // here is "is this session a Companion", answered from the durable
            // association (HOST-008) by one keyed lookup.
            //
            // What this replaced was `PromptAuthority.hasCompanion`, a whitelist over
            // ten CanonicalRoles. That shape could not be fixed by editing the list:
            // COMPANION-001 grants a Y regardless of role, so any role-keyed predicate
            // is answering a question the clause does not ask. It had also silently
            // excluded Inspector, Browser and Executor.
            //
            // No journal means no association and no durable Companion state. Failing
            // closed here rather than defaulting to "not a Companion" keeps a Y from
            // being handed a Y of its own during a journal-less run.
            let isCompanionSession =
                match journal with
                | None -> true
                | Some j ->
                    SessionAssociationProjection.isCompanion
                        (SessionId.create sessionId)
                        (AgentJournal.snapshot j).AgentProjections.Associations

            if not isCompanionSession then
                let companion =
                    lock gate (fun () ->
                        match companions.TryGetValue sessionId with
                        | true, value -> value
                        | false, _ ->
                            let durable =
                                journal
                                |> Option.map (fun j -> AgentJournalCompanionPort j :> ICompanionDurablePort)

                            let restoredBloggerId =
                                match journal with
                                | Some j ->
                                    // COMPANION-003: Y is recorded as its own identity.
                                    // The previous version searched the parent's handle
                                    // links for the literal target `"blogger"`, which is
                                    // agent-string matching standing in for an identity —
                                    // and it also put an internal agent into the EXEC-005
                                    // resource view that AGENT-008 keeps it out of.
                                    (AgentJournal.snapshot j).AgentProjections.Sessions
                                    |> Map.tryFind (SessionId.create sessionId)
                                    |> Option.bind (fun s -> s.Companion)
                                    |> Option.bind (fun companion -> companion.BloggerSessionId)
                                    |> Option.map SessionId.value
                                | None -> None

                            let value =
                                new CompanionHost(
                                    SessionId.create sessionId,
                                    sessionPort,
                                    ?durable = durable,
                                    onBloggerCreated =
                                        (fun bloggerId ->
                                            // Own + bind the blogger run so idle
                                            // reconcile can NotifyTerminal and
                                            // complete the pending blog Submit.
                                            onBloggerCreated |> Option.iter (fun callback -> callback bloggerId)),
                                    ?restoredBloggerId = restoredBloggerId,
                                    ?journal = journal,
                                    // The blogger is a companion, not a worktree
                                    // worker: pin it to the stable workspace so its
                                    // system prompt (Host instruction loading from
                                    // the session directory) survives the
                                    // manager worktree release at publish
                                    // (measured: the post-release delta lost the
                                    // AGENTS.md block and broke the ARCH-004
                                    // prefix seal).
                                    ?bloggerDirectory = workspaceDirectory
                                )

                            companions.[sessionId] <- value

                            // CTX-006 step 5: the squash attempt's plan goes through
                            // the same scope dictionary an X attempt uses — no second
                            // attempt registry on the Y chain.
                            value.RecordSquashPlan <-
                                fun bloggerId providerRun ->
                                    match journal with
                                    | None -> ()
                                    | Some j ->
                                        let projections = (AgentJournal.snapshot j).AgentProjections

                                        match PromptAuthorityLedger.activeProfile bloggerId projections with
                                        | None -> ()
                                        | Some authority ->
                                            let plan =
                                                AttemptPlanner.plan
                                                    authority
                                                    AgentPairCursor.initial
                                                    (PhysicalUserMessageId.create (SessionId.value bloggerId))
                                                    providerRun
                                                    (PromptAuthority.PromptOrigin.AuthorityRoot
                                                        PromptAuthority.RootAuthorityKind.AgentOwnerRoot)
                                                    ProviderRequestKind.BloggerSquash
                                                    false
                                                    (fun () -> Error NoCandidateReason.NoCoverage)

                                            scope.RecordAttemptPlan bloggerId providerRun plan

                            value)

                // ENFORCER-050: single coordinator. The main session transform
                // decides start (idle), skip (in-flight), or offer (parked) —
                // never two paths to the same Blogger.
                let bloggerIdOpt = companion.BloggerSession
                let parkedHost = scope :> IParkedTransformHost

                match bloggerIdOpt with
                | Some bloggerId when parkedHost.HasParked(SessionId.value bloggerId) ->
                    // Parked: stage the typed delta and resume the parked transform.
                    // The continuation rebuilds the full provider view from durable
                    // frames + this context — no PromptDispatcher, no raw transcript.
                    let blog = companion.Memory.Blog
                    let xTrace = companion.Memory.XTrace

                    let current =
                        Projection.decodeMessageView rawMessages |> ProviderProjection.toSemantic

                    let ingestCursor =
                        XTraceProjection.semanticCursorFor blog.Coverage.IngestedThroughSequence xTrace

                    match
                        BloggerDelta.nextChunk
                            BloggerDelta.DeltaLimitBytes
                            ingestCursor
                            blog.Coverage.CoverableTurnCutoffExclusive
                            current.Messages
                    with
                    | Some chunk ->
                        let ctx = EnforcerHost.mainContextFromChunk blog xTrace chunk
                        parkedHost.OfferParked(SessionId.value bloggerId, ctx) |> ignore
                    | None -> ()
                | _ -> companion.TransformRaw rawMessages |> replaceMessagesInPlace rawOutObj
