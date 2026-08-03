namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools
open Wanxiangshu.Next.Domain
open CompanionProjection

module CompanionTransform =

    let private ensureCompanion
        (companions: Dictionary<string, CompanionHost>)
        (gate: obj)
        (scope: PluginRuntimeScope)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (onBloggerCreated: (SessionId -> unit) option)
        (workspaceDirectory: string option)
        (sessionId: string)
        : CompanionHost =
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
                            (fun bloggerId -> onBloggerCreated |> Option.iter (fun callback -> callback bloggerId)),
                        ?restoredBloggerId = restoredBloggerId,
                        ?journal = journal,
                        ?bloggerDirectory = workspaceDirectory
                    )

                companions.[sessionId] <- value

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

    /// Main-session transform: Host view unchanged; material decision is sole coordinator.
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
        : Task<unit> =
        task {
            let rawMessages = unbox<obj array> rawOutObj?messages |> Array.toList

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
                let isCompanionSession =
                    match journal with
                    | None -> true
                    | Some j ->
                        SessionAssociationProjection.isCompanion
                            (SessionId.create sessionId)
                            (AgentJournal.snapshot j).AgentProjections.Associations

                if not isCompanionSession then
                    let companion =
                        ensureCompanion
                            companions
                            gate
                            scope
                            sessionPort
                            journal
                            onBloggerCreated
                            workspaceDirectory
                            sessionId

                    // Host view unchanged (CTX-002). Coordinator owns all Blogger effects.
                    companion.TransformRaw rawMessages |> replaceMessagesInPlace rawOutObj

                    let projection =
                        Projection.decodeMessageView rawMessages |> ProviderProjection.toSemantic

                    // No child until there is a real X gap. Empty fixture transforms
                    // (HOST-009 positional hooks) must not require Host transport.
                    let blog, xTrace =
                        match journal with
                        | Some j ->
                            let sessions = (AgentJournal.snapshot j).AgentProjections.Sessions
                            let session = sessions |> Map.tryFind (SessionId.create sessionId)

                            (session
                             |> Option.bind (fun s -> s.Blog)
                             |> Option.defaultValue BlogProjection.empty),
                            (session
                             |> Option.bind (fun s -> s.XTrace)
                             |> Option.defaultValue XTraceProjection.empty)
                        | None -> companion.Memory.Blog, companion.Memory.XTrace

                    let ingestCursor =
                        XTraceProjection.semanticCursorFor blog.Coverage.IngestedThroughSequence xTrace

                    let hasMaterial =
                        BloggerDelta.nextChunk
                            BloggerDelta.DeltaLimitBytes
                            ingestCursor
                            blog.Coverage.CoverableTurnCutoffExclusive
                            projection.Messages
                        |> Option.isSome

                    if hasMaterial then
                        let! bloggerId = companion.EnsureBloggerAsync()

                        let! _ =
                            BloggerCoordinator.onMainMaterial
                                (scope :> IParkedTransformHost)
                                companion
                                journal
                                (SessionId.create sessionId)
                                bloggerId
                                projection

                        ()
        }
