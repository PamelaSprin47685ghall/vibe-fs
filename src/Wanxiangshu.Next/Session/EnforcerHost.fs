namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Domain.EnforcerCatalogData
open Wanxiangshu.Next.Host
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

/// SSOT/15 — Blogger as Enforcer: the Blogger continuation-transform host.
///
/// ENFORCER-044: when the Host has collected a provider step's tool results and
/// enters the continuation transform, this module re-reads the full assistant
/// snapshot, re-canonicalises every `blog` call, merges them by PartOrdinal, and
/// commits ONE BlogEntryCommitted atomically (ENFORCER-045/154) — the single
/// fact that appends the frame, advances coverage, and records the enforcement
/// half.
///
/// ENFORCER-047/050/051: after the commit the continuation transform parks
/// (no provider request leaves) until the main session offers fresh material;
/// the offer stages the new delta and resumes the parked transform, which
/// injects the delta as a synthetic user message (ENFORCER-051) and returns, so
/// the Host's step loop resumes with a rebuilt provider view from durable frames
/// + typed context (not raw transcript append). Cycles after the first therefore
/// never create a PromptDispatcher side effect.
module EnforcerHost =

    /// ENFORCER-160: the parking lifetime for a continuation transform.
    /// Shared constant: SSOT/14 STRENGTH-079 `ParkedTransformLifetime` is the
    /// one code constant for how long a transform may stay suspended.
    let ParkedTransformLifetime = StrengthPolicy.Strength.ParkedTransformLifetime

    /// Raw part object → completed `blog` call arguments.
    ///
    /// ENFORCER-041: identity comes from the part itself here (the transform
    /// boundary has no ToolContext), and the fold's replay path reads the same
    /// shape — the assistant message id IS the ProviderRunIdentity, exactly as
    /// XWire derives it (`ProviderRunIdentity.create assistant.Id`).
    let private blogCallFromPart (part: obj) : (ToolCallId * obj) option =
        if isNull part then
            None
        else
            let kind =
                if isNull part?``type`` then
                    ""
                else
                    unbox<string> part?``type``

            let name =
                if isNull part?tool then
                    if isNull part?name then "" else unbox<string> part?name
                else
                    unbox<string> part?tool

            if kind <> "tool" || name <> "blog" then
                None
            else
                let callId =
                    if isNull part?callID then
                        if isNull part?callId then
                            None
                        else
                            Some(unbox<string> part?callId)
                    else
                        Some(unbox<string> part?callID)

                let status =
                    if isNull part?state then
                        None
                    else
                        match part?state?status with
                        | null -> None
                        | value -> Some(unbox<string> value)

                match callId, status with
                | Some id, Some "completed" ->
                    let input =
                        if isNull part?state || isNull part?state?input then
                            createEmpty
                        else
                            part?state?input

                    Some(ToolCallId.create id, input)
                | _ -> None

    /// The last assistant message of a transform snapshot and its parts.
    let private lastAssistantStep (rawMessages: obj list) : (string * obj list) option =
        rawMessages
        |> List.choose (fun message ->
            if isNull message then
                None
            else
                let info = if isNull message?info then message else message?info

                let role =
                    if isNull info then
                        None
                    else
                        (if isNull info?role then
                             None
                         else
                             Some(unbox<string> info?role))

                let id =
                    if isNull info || isNull info?id then
                        None
                    else
                        Some(unbox<string> info?id)

                match role, id with
                | Some "assistant", Some messageId ->
                    let parts =
                        if isNull message?parts then
                            []
                        else
                            unbox<obj array> message?parts |> Array.toList

                    Some(messageId, parts)
                | _ -> None)
        |> List.tryLast

    /// Decode a raw JS object into a string-keyed map (the codec's input shape).
    let private decodeObject (value: obj) : Map<string, obj> =
        if isNull value then
            Map.empty
        else
            let keys: string array = emitJsExpr value "Object.keys($0)"
            let mutable result = Map.empty

            for key in keys do
                result <- Map.add key (emitJsExpr (value, key) "$0[$1]") result

            result

    /// ENFORCER-042: (PartOrdinal, ToolCallId, CanonicalBlogCall) for one
    /// provider step, in provider-visible order. The ordinal is the part's
    /// index in the assistant message — the only ordering that survives
    /// parallel execution.
    let extractCalls
        (rawMessages: obj list)
        : (string * (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) list) option =
        match lastAssistantStep rawMessages with
        | None -> None
        | Some(messageId, parts) ->
            let catalog =
                rules |> List.map (fun rule -> rule.FieldName, rule.RuleId, rule.CatalogOrdinal)

            let calls =
                parts
                |> List.mapi (fun ordinal part -> ordinal, blogCallFromPart part)
                |> List.choose (fun (ordinal, parsed) ->
                    parsed
                    |> Option.map (fun (callId, input) ->
                        ordinal, callId, EnforcerCodec.decodeCall catalog (decodeObject input)))

            Some(messageId, calls)

    /// ENFORCER-045: the score vector as canonical JSON bytes (Map → object).
    let private scoresToObj (scores: Map<string, byte>) : obj =
        scores
        |> Map.toList
        |> List.map (fun (key, value) -> key, box (int value))
        |> createObj

    /// ENFORCER-043: a cycle is valid when the provider run is provable, at
    /// least one call exists, the merged text is non-empty, and every
    /// ToolCallId is unique.
    let private validateCycle
        (messageId: string)
        (calls: (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) list)
        : Result<EnforcerCycle.MergedCycle * ToolCallId list, string> =
        if String.IsNullOrWhiteSpace messageId then
            Error "blog cycle has no provable provider run (ENFORCER-043)"
        elif List.isEmpty calls then
            Error "blog cycle has no completed blog calls (ENFORCER-043)"
        else
            let callIds = calls |> List.map (fun (_, callId, _) -> callId)

            if List.length (List.distinct callIds) <> List.length calls then
                Error "blog cycle has duplicate ToolCallIds (ENFORCER-043)"
            else
                let merged =
                    EnforcerCycle.mergeCalls (calls |> List.map (fun (ordinal, _, call) -> ordinal, call))

                if not (EnforcerCycle.isValidCycle merged) then
                    Error "blog cycle merged text is empty after canonicalisation (ENFORCER-043)"
                else
                    Ok(merged, callIds)

    /// Commit one cycle: blobs first, then the single BlogEntryCommitted
    /// append (PERSIST-009 shape: durable effect → fact). The fold refuses a
    /// duplicate ProviderRun, so replay of an already-committed step is a no-op
    /// at the caller's idempotency check (ENFORCER-154).
    ///
    /// ENFORCER-045: coverage advance is ONLY the staged typed context. Re-deriving
    /// from XTrace head is forbidden — that path freezes PrefixCoverage at 0 and
    /// leaves CoveredPrefixDigest empty, so CTX-011 probes never arm.
    let private commitCycle
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (toolCallIds: ToolCallId list)
        (merged: EnforcerCycle.MergedCycle)
        (declared: BloggerMainRequestContext option)
        : Result<EnforcementCycleRecord, string> =
        let projections = AgentJournal.snapshot journal

        let already =
            projections.AgentProjections.Sessions
            |> Map.tryFind mainSessionId
            |> Option.bind (fun session -> session.Enforcement)
            |> Option.map (fun state -> EnforcementProjection.tryFindByProviderRun providerRun state)
            |> Option.flatten

        match already with
        | Some record -> Ok record
        | None ->
            match declared with
            | None -> Error "blog cycle has no staged coverage context (ENFORCER-045)"
            | Some coverage ->
                let session = projections.AgentProjections.Sessions |> Map.tryFind mainSessionId

                let epoch =
                    session
                    |> Option.bind (fun s -> s.PrefixEpoch)
                    |> Option.map (fun e -> e.EpochId)
                    |> Option.defaultValue PrefixEpochId.initial

                match journal.WriteBlob merged.MergedText with
                | Error error -> Error error
                | Ok textBlob ->
                    let writeScore () =
                        if Map.isEmpty merged.MergedScores then
                            Ok None
                        else
                            journal.WriteBlob(CanonicalJson.canonicalJson (scoresToObj merged.MergedScores))
                            |> Result.map Some

                    let writeEvidence () =
                        match merged.MergedEvidence with
                        | "" -> Ok None
                        | evidence -> journal.WriteBlob evidence |> Result.map Some

                    match writeScore (), writeEvidence () with
                    | Error error, _
                    | _, Error error -> Error error
                    | Ok scoreRef, Ok evidenceRef ->
                        let record =
                            { MainSessionId = mainSessionId
                              BloggerSessionId = bloggerSessionId
                              ProviderRun = providerRun
                              ToolCallIds = toolCallIds
                              CycleTextRef = textBlob.BlobRef
                              CycleTextDigest = textBlob.BlobDigest
                              CycleScoreRef = scoreRef |> Option.map (fun blob -> blob.BlobRef)
                              CycleEvidenceRef = evidenceRef |> Option.map (fun blob -> blob.BlobRef)
                              ObservedPrefixEpochId = epoch }

                        let fact =
                            AgentFact.BlogEntryCommitted
                                {| SessionId = mainSessionId
                                   BloggerSessionId = bloggerSessionId
                                   FrameEpochId = coverage.FrameEpochId
                                   PreviousIngestedThroughSequence = coverage.PreviousIngestedThroughSequence
                                   NextIngestedThroughSequence = coverage.NextIngestedThroughSequence
                                   PreviousCoverableTurnCutoffExclusive =
                                       coverage.PreviousCoverableTurnCutoffExclusive
                                   NextCoverableTurnCutoffExclusive = coverage.NextCoverableTurnCutoffExclusive
                                   NextCoveredPrefixDigest = coverage.NextCoveredPrefixDigest
                                   TextRef = textBlob.BlobRef
                                   TextDigest = textBlob.BlobDigest
                                   ProviderRun = providerRun
                                   ToolCallIds = toolCallIds
                                   ScoreVectorRef = record.CycleScoreRef
                                   EvidenceRef = record.CycleEvidenceRef
                                   ObservedPrefixEpochId = epoch |}

                        match
                            AgentJournal.appendAgent
                                (StreamId.Session mainSessionId)
                                (Some providerRun)
                                fact
                                journal
                        with
                        | Error failure -> Error(JournalAppendFailure.describe failure)
                        | Ok _ -> Ok record

    /// CTX-012: single production constructor path for BlogSquashCommitted from tool loop.
    let private commitSquash
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (squash: BloggerSquashRequestContext)
        (squashText: string)
        : Result<unit, string> =
        let projections = AgentJournal.snapshot journal

        match projections.AgentProjections.Sessions |> Map.tryFind mainSessionId with
        | None -> Error "BlogSquashCommitted requires an existing work session projection"
        | Some session ->
            match session.Companion |> Option.bind (fun c -> c.BloggerSessionId) with
            | Some linked when linked = bloggerSessionId ->
                let blog = session.Blog |> Option.defaultValue BlogProjection.empty
                let k = squash.CoveredFrameCount

                if k < 1 || k > List.length blog.Frames then
                    Error(sprintf "BlogSquashCommitted covers %d frames but %d exist" k (List.length blog.Frames))
                elif blog.FrameEpochId <> squash.FrameEpochId then
                    Error "BlogSquashCommitted frame epoch mismatch"
                else
                    let selected = List.truncate k blog.Frames
                    let digests = selected |> List.map (fun f -> f.Digest)

                    if digests <> squash.FrameDigests then
                        Error "BlogSquashCommitted frame digests mismatch"
                    else
                        match journal.WriteBlob squashText with
                        | Error error -> Error error
                        | Ok blob ->
                            let fact =
                                AgentFact.BlogSquashCommitted
                                    {| SessionId = mainSessionId
                                       BloggerSessionId = bloggerSessionId
                                       PreviousFrameEpochId = blog.FrameEpochId
                                       NextFrameEpochId = FrameEpochId.next blog.FrameEpochId
                                       CoveredFrameCount = k
                                       TextRef = blob.BlobRef
                                       TextDigest = blob.BlobDigest
                                       ProviderRun = providerRun |}

                            match
                                AgentJournal.appendAgent
                                    (StreamId.Session mainSessionId)
                                    (Some providerRun)
                                    fact
                                    journal
                            with
                            | Error failure -> Error(JournalAppendFailure.describe failure)
                            | Ok _ -> Ok()
            | Some _ -> Error "Squash completion belongs to a different Blogger session"
            | None -> Error "BlogSquashCommitted requires a durably linked Blogger session"

    /// ENFORCER-051: rebuild the full provider view from durable frames + context.
    ///
    /// Replaces `rawMessages @ [syntheticDelta]`. The raw transcript is NOT the
    /// history source — durable BlogFrames + the typed context are.
    let private rebuildFromContext
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (ctx: BloggerRequestContext)
        : obj list =
        let projections = AgentJournal.snapshot journal

        let mainSessionId =
            SessionAssociationProjection.tryMainSessionOf bloggerSessionId projections.AgentProjections.Associations

        let blog =
            mainSessionId
            |> Option.bind (fun sid -> projections.AgentProjections.Sessions |> Map.tryFind sid)
            |> Option.bind (fun session -> session.Blog)
            |> Option.defaultValue BlogProjection.empty

        let frameBodies =
            blog.Frames
            |> List.choose (fun frame ->
                match journal.Writer.BlobWriter.Read frame.TextRef with
                | Ok text -> Some(frame.Digest, text)
                | Error _ -> None)

        let kind =
            match ctx with
            | BloggerRequestContext.Main _ -> CompanionRequestKind.Normal
            | BloggerRequestContext.Squash squash -> CompanionRequestKind.Squash squash.CoveredFrameCount

        let delta =
            match BloggerRequestContext.toml ctx with
            | Some toml ->
                let messageId =
                    HostDigest.sha256Hex (String.concat "|" [ SessionId.value bloggerSessionId; "delta"; toml ])

                Some(messageId, toml)
            | None -> None

        let plan =
            CompanionProjectionBuilder.build
                HostDigest.sha256Hex
                bloggerSessionId
                blog.FrameEpochId
                kind
                frameBodies
                delta

        plan.Messages
        |> List.map (fun msg ->
            createObj
                [ "info", box (createObj [ "id", box msg.MessageId; "role", box msg.Role ])
                  "parts", box [| createObj [ "type", box "text"; "text", box msg.Text ] |] ])

    /// Map chunk NextCursor (first unconsumed semantic position) → XTrace sequence
    /// of the last COVERED part. Paired with `semanticCursorFor`'s `>`: the next
    /// delta starts strictly after this sequence (COMPANION-003 / CTX-011).
    let private lastCoveredSequence (xTrace: XTraceProjectionState) (nextCursor: SemanticCursor) =
        xTrace.Parts
        |> List.tryFindBack (fun part ->
            part.Turn < nextCursor.TurnIndex
            || (part.Turn = nextCursor.TurnIndex && part.PartIndex < nextCursor.PartIndex))
        |> Option.map (fun part -> part.Cursor.Sequence)
        |> Option.defaultValue 0L

    /// COMPANION-011: digest of X's provider-visible prefix at the coverable cutoff.
    /// When the cutoff does not move, the previous digest is kept so a mid-turn
    /// chunk cannot rewrite a proof that still describes the same turns.
    let private coveredPrefixDigest
        (previousCutoff: int)
        (previousDigest: string)
        (nextCutoff: int)
        (projection: ProviderProjection.ProviderSemanticProjection)
        : string =
        if nextCutoff = previousCutoff then
            previousDigest
        else
            let coveredMessages =
                projection.Messages
                |> List.truncate (min nextCutoff (List.length projection.Messages))

            HostDigest.sha256Hex (
                ProviderProjection.renderSemantic
                    { projection with
                        Messages = coveredMessages }
            )

    /// Public: build the staged offer context from the same delta the coordinator
    /// computed. The projection is required so COMPANION-011 can hash the covered
    /// prefix at the new cutoff.
    let internal mainContextFromChunk
        (blog: BlogProjectionState)
        (xTrace: XTraceProjectionState)
        (projection: ProviderProjection.ProviderSemanticProjection)
        (chunk: BloggerDeltaChunk)
        : BloggerRequestContext =
        let nextSeq = lastCoveredSequence xTrace chunk.NextCursor
        let nextDigest =
            coveredPrefixDigest
                blog.Coverage.CoverableTurnCutoffExclusive
                blog.Coverage.CoveredPrefixDigest
                chunk.NextCoverableTurnCutoffExclusive
                projection

        BloggerRequestContext.Main
            { Toml = chunk.Toml
              PreviousIngestedThroughSequence = blog.Coverage.IngestedThroughSequence
              NextIngestedThroughSequence = nextSeq
              PreviousCoverableTurnCutoffExclusive = blog.Coverage.CoverableTurnCutoffExclusive
              NextCoverableTurnCutoffExclusive = chunk.NextCoverableTurnCutoffExclusive
              NextCoveredPrefixDigest = nextDigest
              FrameEpochId = blog.FrameEpochId
              DeltaDigest = BlobDigest.create (HostDigest.sha256Hex chunk.Toml) }

    /// The Blogger continuation-transform handler.
    ///
    /// ENFORCER-044 steps 1-7: read the step, merge, commit atomically, then
    /// park or inject. Returns the (possibly modified) message list.
    ///
    /// The FIRST transform of a Blogger turn (the prompt_async origin,
    /// ENFORCER-051) has no assistant message yet — it must never park; the
    /// request has to go out. Only a continuation (assistant step present)
    /// parks.
    let handleContinuation
        (scope: IParkedTransformHost)
        (journal: AgentJournal option)
        (bloggerSessionId: SessionId)
        (rawMessages: obj list)
        : Task<obj list> =
        task {
            // ENFORCER-010: the execute gate proves the Blogger association;
            // the commit side re-proves it. An unprovable owner is fail-closed:
            // no cycle is committed under a guessed session (a fallback to the
            // blogger's own id would write to the wrong stream and escape the
            // per-session exactly-once index).
            let mainSessionId =
                journal
                |> Option.bind (fun j ->
                    SessionAssociationProjection.tryMainSessionOf
                        bloggerSessionId
                        (AgentJournal.snapshot j).AgentProjections.Associations)

            match journal, mainSessionId, extractCalls rawMessages with
            | Some durable, Some owner, Some(messageId, calls) when not (List.isEmpty calls) ->
                // ENFORCER-044 step 2: this is a tool-loop continuation whose
                // provider step produced completed blog calls — merge and
                // commit one cycle, then park (ENFORCER-047).
                let mainSessionId = owner
                let providerRun = ProviderRunIdentity.create messageId

                // ENFORCER-154: the commit is idempotent — a resumed transform
                // re-enters with the same raw messages (same provider run) and
                // must NOT consume the staged offer again. The offer belongs to
                // the NEXT cycle's coverage advance, not this one's.
                let key = SessionId.value bloggerSessionId
                let currentCtx = scope.TryPeekCurrentRequest key

                let alreadyEntry =
                    (AgentJournal.snapshot durable).AgentProjections.Sessions
                    |> Map.tryFind mainSessionId
                    |> Option.bind (fun session -> session.Enforcement)
                    |> Option.map (fun state -> EnforcementProjection.tryFindByProviderRun providerRun state)
                    |> Option.flatten
                    |> Option.isSome

                if alreadyEntry then
                    let resumeWithContext ctx =
                        rebuildFromContext durable bloggerSessionId ctx

                    match scope.TryTakePendingOffer key with
                    | Some ctx ->
                        match BloggerRuntime.adoptPendingAsCurrent (scope.GetBloggerRuntime key) ctx with
                        | Ok cell ->
                            scope.SetBloggerRuntime(key, cell)
                            scope.SetCurrentRequest(key, ctx)
                        | Error _ -> ()

                        return resumeWithContext ctx
                    | None -> return rawMessages
                else
                    let mutable committed = false
                    let mutable afterSquashMain: BloggerRequestContext option = None

                    match validateCycle messageId calls with
                    | Error reason ->
                        Diagnostic.emit
                            "enforcer-cycle-invalid"
                            [ "session_id", key; "result", reason ]
                    | Ok(merged, toolCallIds) ->
                        match currentCtx with
                        | Some(BloggerRequestContext.Squash squash) ->
                            // CTX-012 / C3: squash commits via the same blog tool loop.
                            // Coverage must not advance. Single writer = BlogSquashCommitted.
                            match commitSquash durable mainSessionId bloggerSessionId providerRun squash merged.MergedText with
                            | Ok _ ->
                                committed <- true
                                afterSquashMain <- None

                                match BloggerRuntime.onSquashCommitted (scope.GetBloggerRuntime key) None with
                                | Ok(cell, _) -> scope.SetBloggerRuntime(key, cell)
                                | Error _ -> ()

                                scope.ClearCurrentRequest key
                            | Error reason ->
                                Diagnostic.emit
                                    "enforcer-squash-commit-failed"
                                    [ "session_id", key; "result", reason ]
                        | Some(BloggerRequestContext.Main main) ->
                            match
                                commitCycle
                                    durable
                                    mainSessionId
                                    bloggerSessionId
                                    providerRun
                                    toolCallIds
                                    merged
                                    (Some main)
                            with
                            | Ok _ ->
                                committed <- true

                                match BloggerRuntime.onCycleCommitted (scope.GetBloggerRuntime key) with
                                | Ok cell -> scope.SetBloggerRuntime(key, cell)
                                | Error _ -> ()

                                scope.ClearCurrentRequest key
                            | Error reason ->
                                Diagnostic.emit
                                    "enforcer-cycle-commit-failed"
                                    [ "session_id", key; "result", reason ]
                        | None ->
                            Diagnostic.emit
                                "enforcer-cycle-commit-failed"
                                [ "session_id", key; "result", "missing CurrentRequest" ]

                    if not committed then
                        return rawMessages
                    else
                        let resumeWithContext ctx =
                            match journal with
                            | Some durable -> rebuildFromContext durable bloggerSessionId ctx
                            | None -> rawMessages

                        // Prefer PendingOffer (Main after park), else post-squash Main if any.
                        match scope.TryTakePendingOffer key, afterSquashMain with
                        | Some ctx, _
                        | None, Some ctx ->
                            match BloggerRuntime.adoptPendingAsCurrent (scope.GetBloggerRuntime key) ctx with
                            | Ok cell ->
                                scope.SetBloggerRuntime(key, cell)
                                scope.SetCurrentRequest(key, ctx)
                            | Error _ -> ()

                            return resumeWithContext ctx
                        | None, None ->
                            let! resumed = scope.ParkTransform(key, ParkedTransformLifetime)

                            if not resumed then
                                return rawMessages
                            else
                                match scope.TryTakePendingOffer key with
                                | Some ctx ->
                                    match BloggerRuntime.adoptPendingAsCurrent (scope.GetBloggerRuntime key) ctx with
                                    | Ok cell ->
                                        scope.SetBloggerRuntime(key, cell)
                                        scope.SetCurrentRequest(key, ctx)
                                    | Error _ -> ()

                                    return resumeWithContext ctx
                                | None -> return rawMessages
            | _ ->
                // COMPANION-005 first request / non-tool step: rebuild only from
                // durable frames + typed CurrentRequest. Never extract TOML from
                // raw user messages (C2).
                match journal, scope.TryPeekCurrentRequest(SessionId.value bloggerSessionId) with
                | Some durable, Some ctx -> return rebuildFromContext durable bloggerSessionId ctx
                | _ -> return rawMessages
        }


