namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// docs/what/enforcer.md — Blogger as Enforcer: the Blogger continuation-transform host.
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
    /// Owned by the Enforcer domain (GOV-003: no proposal-constant dependency
    /// in the production graph; the proposal may reference this, never vice
    /// versa).
    let ParkedTransformLifetime = TimeSpan.FromMinutes 10.0

    /// C4: commit-path UTF-8 safety bounds.
    let MaxBlogTextBytes = 512 * 1024
    let MaxEvidenceBytes = 128 * 1024
    /// ENFORCER-042: defensive multi-call cap (protocol violation still merged).
    let MaxMergedToolCalls = 32

    /// Item 14: three commit outcomes. Park only on KnownCommitted.
    [<RequireQualifiedAccess>]
    type CycleCommitOutcome =
        | KnownCommitted
        | KnownNotCommitted of reason: string
        | CommitUnknown of reason: string

    /// Local outcome of one continuation cycle body (no program-counter bools).
    [<RequireQualifiedAccess>]
    type CycleDisposition =
        | Working
        | Committed of afterSquashMain: BloggerRequestContext option
        | InjectRepair of BloggerRequestContext
        | CommitUnknown
        | AbandonThenCatchUp

    /// Continuation transform result. Empty message lists are forbidden: Host
    /// forwards them as provider `messages` and rejects with 400.
    /// StopPhysicalRun asks the plugin to AbortSession after projecting messages.
    [<RequireQualifiedAccess>]
    type ContinuationOutcome =
        | ProjectMessages of obj list
        | StopPhysicalRun of messages: obj list * reason: string

    /// Prefer non-empty preferred; else fallback. Never invent a blank list when
    /// either side has content. Both empty is an invariant break: blanking Host
    /// transcript yields provider 400 (messages cannot be empty).
    let private ensureNonEmpty (preferred: obj list) (fallback: obj list) : obj list =
        if not (List.isEmpty preferred) then
            preferred
        elif not (List.isEmpty fallback) then
            fallback
        else
            Diagnostic.fatal
                "enforcer-empty-projection"
                [ "result", "ensureNonEmpty: both preferred and fallback are empty" ]

            preferred

    let private projectMessages (messages: obj list) (fallback: obj list) : ContinuationOutcome =
        ContinuationOutcome.ProjectMessages(ensureNonEmpty messages fallback)

    let private stopPhysicalRun (messages: obj list) (fallback: obj list) (reason: string) : ContinuationOutcome =
        ContinuationOutcome.StopPhysicalRun(ensureNonEmpty messages fallback, reason)

    /// Item 15: stable minimal repair instruction (no dynamic context resend).
    let RepairInstruction =
        "# Protocol repair\n\nCall the blog tool exactly once with non-empty text. Do not answer in prose."

    let private classifyAppendFailure (failure: JournalAppendFailure) : CycleCommitOutcome =
        match failure with
        | WriteUnknown(_, _) -> CycleCommitOutcome.CommitUnknown(JournalAppendFailure.describe failure)
        | FactRejected(_, _) -> CycleCommitOutcome.KnownNotCommitted(JournalAppendFailure.describe failure)

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
    /// Host sets `time.completed` only when the run ends or is interrupted
    /// (SessionSnapshotPort). Outbound `messages.transform` creates the assistant
    /// shell first — completed is unset. ENFORCER-060 must not fire on that shell.
    let private assistantIsCompleted (message: obj) : bool =
        if isNull message then
            false
        else
            let info = if isNull message?info then message else message?info

            let timeCompleted (source: obj) =
                if isNull source || isNull source?time then
                    null
                else
                    source?time?completed

            not (isNull (timeCompleted info)) || not (isNull (timeCompleted message))

    /// Last assistant terminal as (messageId, calls, completed); public so the
    /// Application-layer recovery probe can bind a claim to the same terminal.
    let lastAssistantStep (rawMessages: obj list) : (string * obj list * bool) option =
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

                    Some(messageId, parts, assistantIsCompleted message)
                | _ -> None)
        |> List.tryLast

    /// Decode a raw JS object into a string-keyed map (the codec's input shape).
    let private decodeObject (value: obj) : Map<string, obj> =
        if isNull value then
            Map.empty
        else
            let keys: string array = emitJsExpr value "Object.keys($0)"

            keys
            |> Array.fold (fun acc key -> Map.add key (emitJsExpr (value, key) "$0[$1]") acc) Map.empty

    /// ENFORCER-042: (PartOrdinal, ToolCallId, CanonicalBlogCall) for one
    /// provider step, in provider-visible order. The ordinal is the part's
    /// index in the assistant message — the only ordering that survives
    /// parallel execution.
    ///
    /// ENFORCER-023: only calls that pass tip re-validation enter the list.
    /// Failed tip decode is a protocol skip (execute should already have
    /// rejected; defense in depth at transform).
    let extractCalls
        (rawMessages: obj list)
        : (string * (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) list * bool) option =
        match lastAssistantStep rawMessages with
        | None -> None
        | Some(messageId, parts, completed) ->
            let rules =
                Wanxiangshu.Infrastructure.Resources.RuntimeResources.current().EnforcerRules

            let calls =
                parts
                |> List.mapi (fun ordinal part -> ordinal, blogCallFromPart part)
                |> List.choose (fun (ordinal, parsed) ->
                    parsed
                    |> Option.bind (fun (callId, input) ->
                        match EnforcerCodec.decodeCall rules (decodeObject input) with
                        | Ok call -> Some(ordinal, callId, call)
                        | Error reason ->
                            // CTX-014: fold identity into result — no whitelist growth for
                            // protocol-skip diagnostics that are never recovery inputs.
                            Diagnostic.emit
                                "enforcer-blog-call-invalid"
                                [ "result",
                                  sprintf "ordinal=%d call_id=%s %s" ordinal (ToolCallId.value callId) reason ]

                            None))

            Some(messageId, calls, completed)

    /// ENFORCER-043: a cycle is valid when the provider run is provable, at
    /// least one call exists, the merged text is non-empty, and every
    /// ToolCallId is unique. Tip is required on each call (decode already).
    let private validateCycle
        (messageId: string)
        (calls: (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) list)
        : Result<EnforcerCycle.MergedCycle * ToolCallId list, string> =
        if String.IsNullOrWhiteSpace messageId then
            Error "blog cycle has no provable provider run (ENFORCER-043)"
        elif List.isEmpty calls then
            Error "blog cycle has no completed blog calls (ENFORCER-043)"
        elif List.length calls > MaxMergedToolCalls then
            Error(sprintf "blog cycle exceeds MaxMergedToolCalls=%d" MaxMergedToolCalls)
        else
            let callIds = calls |> List.map (fun (_, callId, _) -> callId)

            if List.length (List.distinct callIds) <> List.length calls then
                Error "blog cycle has duplicate ToolCallIds (ENFORCER-043)"
            else
                let merged =
                    EnforcerCycle.mergeCalls (calls |> List.map (fun (ordinal, _, call) -> ordinal, call))

                if merged.MultiCall then
                    // ENFORCER-042: multi-call is a protocol violation; still merge defensively.
                    Diagnostic.emit
                        "enforcer-protocol-violation"
                        [ "result",
                          "multiple blog calls in one provider step; tip = first by PartOrdinal (ENFORCER-025)"
                          "call_count", string (List.length calls) ]

                if not (EnforcerCycle.isValidCycle merged) then
                    Error "blog cycle merged text is empty after canonicalisation (ENFORCER-043)"
                elif SyntheticToml.byteCount merged.MergedText > MaxBlogTextBytes then
                    Error(sprintf "blog cycle text exceeds MaxBlogTextBytes=%d" MaxBlogTextBytes)
                elif SyntheticToml.byteCount merged.MergedEvidence > MaxEvidenceBytes then
                    Error(sprintf "blog cycle evidence exceeds MaxEvidenceBytes=%d" MaxEvidenceBytes)
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
        : CycleCommitOutcome =
        let projections = AgentJournal.snapshot journal

        let already =
            projections.AgentProjections.Sessions
            |> Map.tryFind mainSessionId
            |> Option.bind (fun session -> session.Enforcement)
            |> Option.map (fun state -> EnforcementProjection.tryFindByProviderRun providerRun state)
            |> Option.flatten

        // CommitUnknown reconcile: receipt already present → treat as KnownCommitted.
        match already with
        | Some _ -> CycleCommitOutcome.KnownCommitted
        | None ->
            match declared with
            | None -> CycleCommitOutcome.KnownNotCommitted "blog cycle has no staged coverage context (ENFORCER-045)"
            | Some coverage ->
                // PERSIST-010 precheck (writer-side CAS): fold rejects IngestCursorMismatch
                // only AFTER the line is durable, which poisons the journal. Staged
                // PreviousIngestedThroughSequence is frozen at materialization; concurrent
                // commit / crash-resume may advance coverage first. Refuse before append so
                // failure is KnownNotCommitted (recoverable abandon), never FactRejected.
                let liveBlog =
                    projections.AgentProjections.Sessions
                    |> Map.tryFind mainSessionId
                    |> Option.bind (fun session -> session.Blog)
                    |> Option.defaultValue BlogProjection.empty

                let liveIngest = liveBlog.Coverage.IngestedThroughSequence
                let liveCutoff = liveBlog.Coverage.CoverableTurnCutoffExclusive
                let liveFrameEpoch = liveBlog.FrameEpochId

                if coverage.PreviousIngestedThroughSequence <> liveIngest then
                    CycleCommitOutcome.KnownNotCommitted(
                        sprintf
                            "staged previous ingest cursor %d disagrees with projection %d (PERSIST-010 precheck)"
                            coverage.PreviousIngestedThroughSequence
                            liveIngest
                    )
                elif coverage.PreviousCoverableTurnCutoffExclusive <> liveCutoff then
                    CycleCommitOutcome.KnownNotCommitted(
                        sprintf
                            "staged previous coverable cutoff %d disagrees with projection %d (PERSIST-010 precheck)"
                            coverage.PreviousCoverableTurnCutoffExclusive
                            liveCutoff
                    )
                elif coverage.FrameEpochId <> liveFrameEpoch then
                    CycleCommitOutcome.KnownNotCommitted(
                        sprintf
                            "staged frame epoch %d disagrees with projection %d (PERSIST-010 precheck)"
                            (FrameEpochId.value coverage.FrameEpochId)
                            (FrameEpochId.value liveFrameEpoch)
                    )
                elif coverage.NextIngestedThroughSequence <= coverage.PreviousIngestedThroughSequence then
                    CycleCommitOutcome.KnownNotCommitted "coverage did not advance"
                else
                    // C5: use epoch frozen at request materialization, never live PrefixEpoch.
                    let epoch = coverage.ObservedPrefixEpochId

                    match journal.WriteBlob merged.MergedText with
                    | Error error -> CycleCommitOutcome.KnownNotCommitted error
                    | Ok textBlob ->
                        // ENFORCER-045 tip v2: TipRuleId + FieldNameAtCommit on the fact;
                        // no score-vector blob (ENFORCER-072).
                        let writeEvidence () =
                            match merged.MergedEvidence with
                            | "" -> Ok None
                            | evidence -> journal.WriteBlob evidence |> Result.map Some

                        match writeEvidence () with
                        | Error error -> CycleCommitOutcome.KnownNotCommitted error
                        | Ok evidenceRef ->
                            // Re-read after blobs: only coverage-advancing facts race us;
                            // refuse still-stale staged cursor without writing the fact.
                            let latestBlog =
                                AgentJournal.snapshot journal
                                |> fun snap -> snap.AgentProjections.Sessions
                                |> Map.tryFind mainSessionId
                                |> Option.bind (fun session -> session.Blog)
                                |> Option.defaultValue BlogProjection.empty

                            if
                                coverage.PreviousIngestedThroughSequence
                                <> latestBlog.Coverage.IngestedThroughSequence
                                || coverage.PreviousCoverableTurnCutoffExclusive
                                   <> latestBlog.Coverage.CoverableTurnCutoffExclusive
                                || coverage.FrameEpochId <> latestBlog.FrameEpochId
                            then
                                CycleCommitOutcome.KnownNotCommitted(
                                    sprintf
                                        "staged previous ingest cursor %d disagrees with projection %d after blob write (PERSIST-010 precheck)"
                                        coverage.PreviousIngestedThroughSequence
                                        latestBlog.Coverage.IngestedThroughSequence
                                )
                            else
                                let tip = merged.CanonicalTip

                                let fact =
                                    ContextFact.BlogEntryCommitted
                                        {| SessionId = mainSessionId
                                           BloggerSessionId = bloggerSessionId
                                           RequestId = coverage.RequestId
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
                                           TipRuleId = tip.RuleId
                                           FieldNameAtCommit = Some tip.FieldName
                                           EvidenceRef = evidenceRef |> Option.map (fun blob -> blob.BlobRef)
                                           ObservedPrefixEpochId = epoch |}

                                match
                                    AgentJournal.appendAgent
                                        (StreamId.Session mainSessionId)
                                        (Some providerRun)
                                        fact
                                        journal
                                with
                                | Error failure -> classifyAppendFailure failure
                                | Ok _ -> CycleCommitOutcome.KnownCommitted

    /// CTX-012: single production constructor path for BlogSquashCommitted from tool loop.
    let private commitSquash
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (squash: BloggerSquashRequestContext)
        (squashText: string)
        : CycleCommitOutcome =
        let projections = AgentJournal.snapshot journal

        // CommitUnknown reconcile via unified receipt.
        let alreadyReceipt =
            projections.AgentProjections.Sessions
            |> Map.tryFind mainSessionId
            |> Option.bind (fun s -> s.BloggerCycles)
            |> Option.bind (fun cycles -> BloggerCycleProjection.tryReceipt providerRun cycles)

        match alreadyReceipt with
        | Some _ -> CycleCommitOutcome.KnownCommitted
        | None ->
            match projections.AgentProjections.Sessions |> Map.tryFind mainSessionId with
            | None ->
                CycleCommitOutcome.KnownNotCommitted "BlogSquashCommitted requires an existing work session projection"
            | Some session ->
                match session.Companion |> Option.bind (fun c -> c.BloggerSessionId) with
                | Some linked when linked = bloggerSessionId ->
                    let blog = session.Blog |> Option.defaultValue BlogProjection.empty
                    let k = squash.CoveredFrameCount

                    if k < 1 || k > List.length blog.Frames then
                        CycleCommitOutcome.KnownNotCommitted(
                            sprintf "BlogSquashCommitted covers %d frames but %d exist" k (List.length blog.Frames)
                        )
                    elif blog.FrameEpochId <> squash.FrameEpochId then
                        CycleCommitOutcome.KnownNotCommitted "BlogSquashCommitted frame epoch mismatch"
                    else
                        let selected = List.truncate k blog.Frames
                        let digests = selected |> List.map (fun f -> f.Digest)

                        if digests <> squash.FrameDigests then
                            CycleCommitOutcome.KnownNotCommitted "BlogSquashCommitted frame digests mismatch"
                        else
                            match journal.WriteBlob squashText with
                            | Error error -> CycleCommitOutcome.KnownNotCommitted error
                            | Ok blob ->
                                let fact =
                                    ContextFact.BlogSquashCommitted
                                        {| SessionId = mainSessionId
                                           BloggerSessionId = bloggerSessionId
                                           RequestId = squash.RequestId
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
                                | Error failure -> classifyAppendFailure failure
                                | Ok _ -> CycleCommitOutcome.KnownCommitted
                | Some _ ->
                    CycleCommitOutcome.KnownNotCommitted "Squash completion belongs to a different Blogger session"
                | None ->
                    CycleCommitOutcome.KnownNotCommitted "BlogSquashCommitted requires a durably linked Blogger session"

    type FrameLoadError =
        | MissingAssociation
        | MissingBlogSession
        | MissingFrameBlob of digest: string
        | DigestMismatch of digest: string
        | EpochMismatch

    /// C6: unique fail-closed loader for effective BlogFrames.
    /// Silent List.choose drop of bad frames is forbidden.
    let loadEffectiveFrames
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        : Result<(BlobDigest * string) list * FrameEpochId, FrameLoadError> =
        let projections = AgentJournal.snapshot journal

        match SessionAssociationProjection.tryBloggerOf mainSessionId projections.AgentProjections.Associations with
        | None -> Error FrameLoadError.MissingAssociation
        | Some _ ->
            match projections.AgentProjections.Sessions |> Map.tryFind mainSessionId with
            | None -> Error FrameLoadError.MissingBlogSession
            | Some session ->
                let blog = session.Blog |> Option.defaultValue BlogProjection.empty

                if List.isEmpty blog.Frames then
                    Ok([], blog.FrameEpochId)
                else
                    let rec load remaining acc =
                        match remaining with
                        | [] -> Ok(List.rev acc, blog.FrameEpochId)
                        | frame :: rest ->
                            match journal.Writer.BlobWriter.Read frame.TextRef with
                            | Error _ -> Error(FrameLoadError.MissingFrameBlob(BlobDigest.value frame.Digest))
                            | Ok text ->
                                if HostDigest.sha256Hex text <> BlobDigest.value frame.Digest then
                                    Error(FrameLoadError.DigestMismatch(BlobDigest.value frame.Digest))
                                else
                                    load rest ((frame.Digest, text) :: acc)

                    load blog.Frames []

    /// ENFORCER-051: rebuild the full provider view from durable frames + context.
    /// Missing association / frame load → None so the caller keeps rawMessages.
    /// Never return an empty list: that blanks the Host transcript (mock lastUser=null).
    let private tryRebuildFromContext
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (ctx: BloggerRequestContext)
        : obj list option =
        let projections = AgentJournal.snapshot journal

        let mainSessionId =
            SessionAssociationProjection.tryMainSessionOf bloggerSessionId projections.AgentProjections.Associations

        match mainSessionId with
        | None -> None
        | Some owner ->
            // Zero frames is legitimate (first Main before any Entry). Missing
            // association was already filtered. Blob load still fail-closed.
            match loadEffectiveFrames journal owner with
            | Error FrameLoadError.MissingAssociation
            | Error FrameLoadError.MissingBlogSession -> None
            | Error(FrameLoadError.MissingFrameBlob _)
            | Error(FrameLoadError.DigestMismatch _)
            | Error FrameLoadError.EpochMismatch -> None
            | Ok(frameBodies, frameEpoch) ->
                let kind =
                    match ctx with
                    | BloggerRequestContext.Main _ -> CompanionRequestKind.Normal
                    | BloggerRequestContext.Squash squash -> CompanionRequestKind.Squash squash.CoveredFrameCount

                let delta =
                    match ctx with
                    | BloggerRequestContext.Main main ->
                        let messageId =
                            CompanionIdentity.newWorkMessageId HostDigest.sha256Hex bloggerSessionId main.DeltaDigest

                        Some(messageId, main.Toml)
                    | BloggerRequestContext.Squash _ -> None

                // ENFORCER-070/071: RecentTips from main session (oldest → newest).
                // Same source for normal / squash / restart / recovery / compaction rebuilds.
                let previousTips =
                    match projections.AgentProjections.Sessions |> Map.tryFind owner with
                    | Some session ->
                        session.Enforcement
                        |> Option.map EnforcementProjection.recentTips
                        |> Option.defaultValue []
                        |> List.map (fun tip -> tip.FieldName, tip.CycleId)
                    | None -> []

                let plan =
                    CompanionProjectionBuilder.build
                        HostDigest.sha256Hex
                        bloggerSessionId
                        frameEpoch
                        kind
                        frameBodies
                        delta
                        previousTips

                // C6: rebuild frames/instruction are synthetic projections, not new
                // user authority. New Work delta is marked physical for diagnostics;
                // HOST-010 still binds authority pre-transform.
                plan.Messages
                |> List.map (fun msg ->
                    createObj
                        [ "info",
                          box (
                              createObj
                                  [ "id", box msg.MessageId
                                    "role", box msg.Role
                                    "synthetic", box (not msg.IsPhysical)
                                    "source",
                                    box (
                                        if msg.IsPhysical then
                                            "physical-delta"
                                        else
                                            "synthetic-projection"
                                    ) ]
                          )
                          "parts", box [| createObj [ "type", box "text"; "text", box msg.Text ] |] ])
                |> Some

    /// Dead-code hygiene: never default a rebuild miss to []. Callers that still
    /// need a list must pass the Host rawMessages as fallback.
    let private rebuildFromContext journal bloggerSessionId ctx (fallback: obj list) =
        tryRebuildFromContext journal bloggerSessionId ctx
        |> Option.defaultValue fallback

    /// Map chunk NextCursor (first unconsumed semantic position) → XTrace sequence
    /// of the last COVERED part. Paired with `semanticCursorFor`'s `>`: the next
    /// delta starts strictly after this sequence (COMPANION-003 / CTX-011).
    ///
    /// Scoped to the current reanchor generation's Turn/Part labels (HOST-006).
    /// `None` = mapping failed (empty trace, or Host cursor not present on XTrace).
    /// NEVER default to 0: silent 0 with Prev>0 stages Next≤Prev and dies at commit.
    let private lastCoveredSequence (xTrace: XTraceProjectionState) (nextCursor: SemanticCursor) : int64 option =
        XTraceProjection.currentGenerationParts xTrace.Parts
        |> List.tryFindBack (fun part ->
            part.Turn < nextCursor.TurnIndex
            || (part.Turn = nextCursor.TurnIndex && part.PartIndex < nextCursor.PartIndex))
        |> Option.map (fun part -> part.Cursor.Sequence)

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

    /// C5: inverse of BloggerCoordinator.materializeRequest blob.
    /// Full typed context — never leave cutoff/digest at zero defaults.
    let tryReloadRequestContext (journal: AgentJournal) (openReq: OpenBloggerRequest) : BloggerRequestContext option =
        match journal.Writer.BlobWriter.Read openReq.ContextRef with
        | Error _ -> None
        | Ok json ->
            try
                let raw = Fable.Core.JS.JSON.parse json

                let hasKey (key: string) : bool =
                    emitJsExpr (raw, key) "$0 != null && Object.prototype.hasOwnProperty.call($0, $1)"

                let asString (key: string) : string =
                    if not (hasKey key) then
                        ""
                    else
                        let value = raw?(key)

                        if isNull value then
                            ""
                        elif emitJsExpr value "typeof $0 === 'string'" then
                            unbox<string> value
                        elif emitJsExpr value "typeof $0 === 'number'" then
                            string (unbox<float> value)
                        else
                            ""

                let asInt64 (key: string) : int64 option =
                    if not (hasKey key) then
                        None
                    else
                        let value = raw?(key)

                        if isNull value then
                            None
                        elif emitJsExpr value "typeof $0 === 'number'" then
                            Some(int64 (unbox<float> value))
                        elif emitJsExpr value "typeof $0 === 'bigint'" then
                            Some(int64 (unbox<float> (emitJsExpr value "Number($0)")))
                        elif emitJsExpr value "typeof $0 === 'string'" then
                            let text = unbox<string> value

                            if String.IsNullOrWhiteSpace text then
                                None
                            else
                                Some(int64 (float text))
                        else
                            None

                let asInt (key: string) : int option =
                    if not (hasKey key) then
                        None
                    else
                        let value = raw?(key)

                        if isNull value then
                            None
                        elif emitJsExpr value "typeof $0 === 'number'" then
                            Some(int (unbox<float> value))
                        elif emitJsExpr value "typeof $0 === 'string'" then
                            let text = unbox<string> value

                            if String.IsNullOrWhiteSpace text then
                                None
                            else
                                Some(int (float text))
                        else
                            None

                if openReq.RequestKind = "squash" then
                    let covered =
                        asInt "covered_frame_count"
                        |> Option.defaultValue (List.length openReq.SelectedFrameDigests)

                    Some(
                        BloggerRequestContext.Squash
                            { RequestId = openReq.RequestId
                              MainSessionId = openReq.MainSessionId
                              BloggerSessionId = openReq.BloggerSessionId
                              FrameEpochId = openReq.FrameEpochId
                              CoveredFrameCount = covered
                              FrameDigests = openReq.SelectedFrameDigests
                              ObservedPrefixEpochId = openReq.ObservedPrefixEpochId }
                    )
                else
                    let toml = asString "toml"
                    let deltaDigestRaw = asString "delta_digest"

                    let deltaDigest =
                        if String.IsNullOrWhiteSpace deltaDigestRaw then
                            if String.IsNullOrWhiteSpace toml then
                                openReq.ContextDigest
                            else
                                BlobDigest.create (HostDigest.sha256Hex toml)
                        else
                            BlobDigest.create deltaDigestRaw

                    let prevIngest =
                        asInt64 "prev_ingest"
                        |> Option.defaultValue openReq.PreviousIngestedThroughSequence

                    let nextIngest =
                        asInt64 "next_ingest" |> Option.defaultValue openReq.NextIngestedThroughSequence

                    Some(
                        BloggerRequestContext.Main
                            { RequestId = openReq.RequestId
                              MainSessionId = openReq.MainSessionId
                              BloggerSessionId = openReq.BloggerSessionId
                              Toml = toml
                              PreviousIngestedThroughSequence = prevIngest
                              NextIngestedThroughSequence = nextIngest
                              PreviousCoverableTurnCutoffExclusive = asInt "prev_cutoff" |> Option.defaultValue 0
                              NextCoverableTurnCutoffExclusive = asInt "next_cutoff" |> Option.defaultValue 0
                              NextCoveredPrefixDigest = asString "next_prefix_digest"
                              FrameEpochId = openReq.FrameEpochId
                              DeltaDigest = deltaDigest
                              ObservedPrefixEpochId = openReq.ObservedPrefixEpochId }
                    )
            with _ ->
                None

    /// Live commit authority: InFlight payload only.
    /// Completed-blog transform must NEVER heal InFlight from durable open —
    /// Host msgs end on the historical last assistant (new outbound shell is
    /// not in the list). Healing open here re-binds a new RequestId to an old
    /// provider run (stale-cycle race). Crash recovery re-arms InFlight before
    /// handleContinuation when the open request is still live.
    let tryLiveCycleContext (scope: IParkedTransformHost) (bloggerSessionId: SessionId) : BloggerRequestContext option =
        scope.TryPeekCurrentRequest(SessionId.value bloggerSessionId)

    /// Rebuild / empty-calls only: live InFlight, else reload open without
    /// committing. Does not SetCurrentRequest (no side effect on authority).
    let resolveCycleContext
        (scope: IParkedTransformHost)
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        : BloggerRequestContext option =
        let key = SessionId.value bloggerSessionId

        match scope.TryPeekCurrentRequest key with
        | Some ctx -> Some ctx
        | None ->
            let openReq =
                (AgentJournal.snapshot journal).AgentProjections.Sessions
                |> Map.tryFind mainSessionId
                |> Option.bind (fun session -> session.BloggerCycles)
                |> Option.bind (fun cycles -> BloggerCycleProjection.tryOpenByBlogger bloggerSessionId cycles)

            match openReq with
            | None -> None
            | Some req ->
                match tryReloadRequestContext journal req with
                | None -> None
                | Some ctx ->
                    match scope.GetBloggerRuntime(key).State with
                    | _ -> Some ctx

    let private tryOpenByBlogger
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        : OpenBloggerRequest option =
        (AgentJournal.snapshot journal).AgentProjections.Sessions
        |> Map.tryFind mainSessionId
        |> Option.bind (fun session -> session.BloggerCycles)
        |> Option.bind (fun cycles -> BloggerCycleProjection.tryOpenByBlogger bloggerSessionId cycles)

    let private isBlogToolPart (part: obj) : bool =
        if isNull part then
            false
        else
            let kind =
                if isNull part?``type`` then
                    ""
                else
                    unbox<string> part?``type``

            let name =
                if not (isNull part?tool) then unbox<string> part?tool
                elif not (isNull part?name) then unbox<string> part?name
                else ""

            kind = "tool" && name = "blog"

    let private blogPartStatus (part: obj) : string option =
        if isNull part || isNull part?state then
            None
        else
            match part?state?status with
            | null -> None
            | value -> Some(unbox<string> value)

    /// pending/running blog: Host will re-enter after tool completion — not pure prose.
    let private hasIncompleteBlogTool (rawMessages: obj list) : bool =
        match lastAssistantStep rawMessages with
        | None -> false
        | Some(_, parts, _) ->
            parts
            |> List.exists (fun part ->
                isBlogToolPart part
                && match blogPartStatus part with
                   | Some "pending"
                   | Some "running" -> true
                   | _ -> false)

    /// Any blog tool part on the last assistant (completed/error/pending/running).
    /// Host cleanup after abort marks hanging tools status=error + interrupted=true
    /// and sets assistant time.completed — that is NOT ENFORCER-060 pure prose.
    let private hasAnyBlogToolPart (rawMessages: obj list) : bool =
        match lastAssistantStep rawMessages with
        | None -> false
        | Some(_, parts, _) -> parts |> List.exists isBlogToolPart

    let private blogPartInterrupted (part: obj) : bool =
        if isNull part || isNull part?state then
            false
        else
            let meta = part?state?metadata

            if isNull meta then
                false
            else
                match meta?interrupted with
                | null -> false
                | value -> unbox<bool> value = true

    /// Abort/cleanup terminal: blog attempted but never completed successfully.
    let private hasFailedBlogAttempt (rawMessages: obj list) : bool =
        match lastAssistantStep rawMessages with
        | None -> false
        | Some(_, parts, _) ->
            parts
            |> List.exists (fun part ->
                isBlogToolPart part
                && match blogPartStatus part with
                   | Some "completed" -> false
                   | Some "error" -> true
                   | Some "pending"
                   | Some "running" -> false
                   | _ -> blogPartInterrupted part)

    /// Extract the requestKey from an interaction-repair synthetic user message.
    let private repairRequestKey (message: obj) : string option =
        if isNull message then
            None
        else
            let info = if isNull message?info then message else message?info

            if
                not (isNull info)
                && not (isNull info?source)
                && unbox<string> info?source = "interaction-repair"
                && not (isNull info?synthetic)
                && unbox<bool> info?synthetic
                && not (isNull info?requestKey)
            then
                Some(unbox<string> info?requestKey)
            else
                None

    /// ENFORCER-060/061: stable InteractionRepair user message (item 15 — fixed text only).
    let private withRepairInstruction (rawMessages: obj list) (requestKey: string) : obj list =
        let msgId =
            "enforcer-repair-"
            + (HostDigest.sha256Hex (requestKey + "|" + RepairInstruction)).Substring(0, 24)

        let repairMsg =
            createObj
                [ "info",
                  box (
                      createObj
                          [ "id", box msgId
                            "role", box "user"
                            "synthetic", box true
                            "source", box "interaction-repair"
                            "requestKey", box requestKey ]
                  )
                  "parts", box [| createObj [ "type", box "text"; "text", box RepairInstruction ] |] ]

        rawMessages @ [ repairMsg ]

    let private isEmptyTextCycleFailure (reason: string) : bool =
        reason.IndexOf("merged text is empty", StringComparison.Ordinal) >= 0

    /// Rebuild provider-semantic turns from durable XTrace (AABB refresh source).
    /// Current reanchor generation only: Host turn indices restart after HOST-006,
    /// so mixing generations under groupBy Turn glues voided labels to live ones.
    let private projectionFromXTrace
        (journal: AgentJournal)
        (xTrace: XTraceProjectionState)
        : ProviderProjection.ProviderSemanticProjection =
        let byTurn =
            XTraceProjection.currentGenerationParts xTrace.Parts
            |> List.groupBy (fun part -> part.Turn)
            |> List.sortBy fst

        let messages =
            byTurn
            |> List.choose (fun (_turn, parts) ->
                let ordered = parts |> List.sortBy (fun p -> p.PartIndex)

                let role =
                    ordered
                    |> List.tryHead
                    |> Option.map (fun p -> p.Role)
                    |> Option.defaultValue "user"

                let semanticParts =
                    ordered
                    |> List.choose (fun part ->
                        match journal.Writer.BlobWriter.Read part.TextRef with
                        | Error _ -> None
                        | Ok body ->
                            match part.Kind with
                            | "text" -> Some(ProviderProjection.SemanticText body)
                            | "reasoning" -> Some(ProviderProjection.SemanticReasoning body)
                            | "tool_call" ->
                                part.ToolName
                                |> Option.map (fun name -> ProviderProjection.SemanticToolCall(name, body))
                            | "tool_result" -> Some(ProviderProjection.SemanticToolResult body)
                            | "media_omitted" ->
                                let mediaType = if String.IsNullOrWhiteSpace body then None else Some body

                                Some(ProviderProjection.SemanticMedia(mediaType, ""))
                            | _ -> None)

                if List.isEmpty semanticParts then
                    None
                else
                    Some
                        { ProviderProjection.SemanticMessage.Role = role
                          ProviderProjection.SemanticMessage.Parts = semanticParts })

        { ProviderId = None
          ModelId = None
          Variant = None
          Tools = []
          System = []
          Messages = messages }

    /// Public: build the staged offer context from the same delta the coordinator
    /// computed. Freezes RequestId + ObservedPrefixEpochId at materialization (C5).
    ///
    /// ENFORCER-045 / PERSIST-010: refuse at birth when coverage cannot strictly
    /// advance. A zero-advance window is a known, handleable mapping failure —
    /// return None so no BloggerMain is started. Unknown invariant breaks that
    /// still reach commit keep Diagnostic.fatal (君子不立危墙: 已知拒生, 未知仍杀).
    let internal mainContextFromChunk
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (observedEpoch: PrefixEpochId)
        (blog: BlogProjectionState)
        (xTrace: XTraceProjectionState)
        (projection: ProviderProjection.ProviderSemanticProjection)
        (chunk: BloggerDeltaChunk)
        : BloggerRequestContext option =
        match lastCoveredSequence xTrace chunk.NextCursor with
        | None -> None
        | Some nextSeq when nextSeq <= blog.Coverage.IngestedThroughSequence -> None
        | Some nextSeq ->
            let nextDigest =
                coveredPrefixDigest
                    blog.Coverage.CoverableTurnCutoffExclusive
                    blog.Coverage.CoveredPrefixDigest
                    chunk.NextCoverableTurnCutoffExclusive
                    projection

            let deltaDigest = BlobDigest.create (HostDigest.sha256Hex chunk.Toml)

            let requestId =
                BloggerRequestId.create (
                    HostDigest.sha256Hex (
                        String.concat
                            "|"
                            [ SessionId.value mainSessionId
                              SessionId.value bloggerSessionId
                              "main"
                              BlobDigest.value deltaDigest
                              string blog.Coverage.IngestedThroughSequence
                              string nextSeq ]
                    )
                )

            Some(
                BloggerRequestContext.Main
                    { RequestId = requestId
                      MainSessionId = mainSessionId
                      BloggerSessionId = bloggerSessionId
                      Toml = chunk.Toml
                      PreviousIngestedThroughSequence = blog.Coverage.IngestedThroughSequence
                      NextIngestedThroughSequence = nextSeq
                      PreviousCoverableTurnCutoffExclusive = blog.Coverage.CoverableTurnCutoffExclusive
                      NextCoverableTurnCutoffExclusive = chunk.NextCoverableTurnCutoffExclusive
                      NextCoveredPrefixDigest = nextDigest
                      FrameEpochId = blog.FrameEpochId
                      DeltaDigest = deltaDigest
                      ObservedPrefixEpochId = observedEpoch }
            )

    /// AABB: re-chunk from current IngestedThrough against latest XTrace.
    /// Returns None when sealed or no material.
    let tryRefreshMainContextFromJournal
        (scope: IParkedTransformHost)
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        : BloggerRequestContext option =
        let key = SessionId.value bloggerSessionId

        if BloggerRuntimeHost.blocksNew (Some journal) mainSessionId scope key then
            None
        else
            let session =
                AgentProjection.tryFind mainSessionId (AgentJournal.snapshot journal).AgentProjections
                |> Option.defaultValue AgentProjection.emptySession

            let blog = session.Blog |> Option.defaultValue BlogProjection.empty
            let xTrace = session.XTrace |> Option.defaultValue XTraceProjection.empty

            let epoch =
                session.PrefixEpoch
                |> Option.map (fun e -> e.EpochId)
                |> Option.defaultValue PrefixEpochId.initial

            let projection = projectionFromXTrace journal xTrace

            let ingestCursor =
                XTraceProjection.semanticCursorFor blog.Coverage.IngestedThroughSequence xTrace

            match
                BloggerDelta.nextChunk
                    BloggerDelta.DeltaLimitBytes
                    ingestCursor
                    blog.Coverage.CoverableTurnCutoffExclusive
                    projection.Messages
            with
            | None -> None
            | Some chunk -> mainContextFromChunk mainSessionId bloggerSessionId epoch blog xTrace projection chunk

    /// The Blogger continuation-transform handler.
    ///
    /// ENFORCER-044 steps 1-7: read the step, merge, commit atomically, then
    /// park or inject. Returns the (possibly modified) message list.
    ///
    /// The FIRST transform of a Blogger turn (the prompt_async origin,
    /// ENFORCER-051) has no assistant message yet — it must never park; the
    /// request has to go out. Only a continuation (assistant step present)
    /// parks.
    /// ENFORCER-153 / DSL-003: the recovery stage probe, injected by the caller
    /// (Application layer owns the derivation; Session cannot reference it by
    /// compile order). Derived from the durable repair claim + provider-visible
    /// transcript on every read — `BloggerRuntimeCell` carries no Recovery
    /// mirror, and this module must never grow one.
    type RecoveryStageProbe = BloggerRequestContext -> BloggerToolRecovery

    let handleContinuation
        (scope: IParkedTransformHost)
        (journal: AgentJournal option)
        (repairNudge: InteractionRepairNudge option)
        (recoveryProbe: AgentJournal -> SessionId -> obj list -> RecoveryStageProbe)
        (bloggerSessionId: SessionId)
        (rawMessages: obj list)
        : Task<ContinuationOutcome> =
        task {
            // Never blank the Host transcript. Project = continue provider view;
            // Stop = project non-empty messages then plugin AbortSession.
            let project (msgs: obj list) = projectMessages msgs rawMessages

            let stop (reason: string) =
                stopPhysicalRun rawMessages rawMessages reason

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
            | Some durable, Some owner, Some(_messageId, calls, assistantCompleted) when List.isEmpty calls ->
                // Host transform msgs do NOT include the newly created outbound assistant
                // (prompt.ts: updateMessage then trigger transform on prior msgs).
                // lastAssistant = historical tail. Empty completed-blog list means:
                // 1) pending/running blog — Host re-enters after tool completion
                // 2) abort cleanup: blog status=error+interrupted, assistant completed
                //    → NOT pure prose; fail closed if still InFlight
                // 3) outbound after prior success is non-empty extractCalls (other arm)
                // 4) pure prose terminal (no blog parts at all) — ENFORCER-060 once when live
                // 5) no live request + interrupted/prose terminal → stop, never invent repair
                let key = SessionId.value bloggerSessionId

                let currentCtx =
                    match scope.TryPeekCurrentRequest key with
                    | Some c -> Some c
                    | None -> resolveCycleContext scope durable owner bloggerSessionId

                // Repair injection requires LIVE InFlight authority only.
                // Durable-re-derived currentCtx is for rebuild/fatal/abandon — never for aabbRepair.
                // Abort residue (stop → Host interrupted blog) has no live cycle to repair.
                let liveCtx = tryLiveCycleContext scope bloggerSessionId

                let rebuild () =
                    match currentCtx with
                    | Some c ->
                        tryRebuildFromContext durable bloggerSessionId c
                        |> Option.defaultValue rawMessages
                    | None -> rawMessages

                let fatalEnd (reason: string) =
                    Diagnostic.fatal "enforcer-cycle-failed" [ "session_id", key; "result", reason ]

                    BloggerAbandon.openRequest durable owner bloggerSessionId currentCtx reason

                    match BloggerRuntime.onFail (scope.GetBloggerRuntime key) with
                    | Ok next -> scope.SetBloggerRuntime(key, next)
                    | Error _ -> ()

                    scope.ClearCurrentRequest key
                    project rawMessages

                /// ENFORCER-068 AABB: refresh transcript + inject repair. The injected
                /// synthetic message IS the consumed marker (ENFORCER-153 derivation).
                /// Not used on first pure-prose (that is InteractionNudge). Used for: nudge
                /// hard-fail, second pure prose, interrupted tool, ENFORCER-061 empty text.
                let aabbRepair (ctx: BloggerRequestContext) (reason: string) =
                    let cell = scope.GetBloggerRuntime key

                    if
                        AgentProjection.mainSealedForBlogger owner (AgentJournal.snapshot durable).AgentProjections
                        && not (BloggerRuntime.isDrainOpen cell)
                    then
                        BloggerRuntimeHost.forceSealRuntime scope key
                        project rawMessages
                    else
                        // ENFORCER-062/067/068 bridge: the confirmed failure advances the
                        // primary A/A/B/B cursor through the ONE writer. Exhaustion forbids
                        // the next automatic attempt — the repair projection is then NOT
                        // injected (it would re-arm the same run). The same terminal run
                        // observed twice stays AlreadyRecorded: it advances once.
                        let providerRun =
                            match lastAssistantStep rawMessages with
                            | Some(messageId, _, _) when not (String.IsNullOrWhiteSpace messageId) ->
                                ProviderRunIdentity.create messageId
                            | _ -> ProviderRunIdentity.create "unknown-prose-run"

                        let advanceOutcome =
                            match
                                FallbackController.recordConfirmedFailure
                                    durable
                                    AgentPairCursor.DefaultAutoRecoveryBudget
                                    owner
                                    providerRun
                                    reason
                            with
                            | Ok outcome -> Some outcome
                            | Error err ->
                                Diagnostic.emit
                                    "enforcer-aabb-bridge"
                                    [ "session_id", key; "result", "recordConfirmedFailure rejected: " + err ]

                                None

                        match advanceOutcome with
                        | Some(FallbackController.AdvanceOutcome.Exhausted _) ->
                            Diagnostic.emit "enforcer-aabb-exhausted" [ "session_id", key; "result", reason ]
                            fatalEnd "blog aabb exhausted; auto-recovery budget spent"
                        | _ ->
                            // Advanced / AlreadyRecorded / NoActiveRun: repair continues
                            // (NoActiveRun = no accepted primary root, FALLBACK-001).
                            let fresh =
                                tryRefreshMainContextFromJournal scope durable owner bloggerSessionId
                                |> Option.defaultValue ctx

                            scope.SetCurrentRequest(key, fresh)

                            match scope.GetBloggerRuntime key with
                            | c ->
                                match c.State with
                                | BloggerRuntimeState.InFlight _ ->
                                    scope.SetBloggerRuntime(
                                        key,
                                        { c with
                                            State = BloggerRuntimeState.InFlight fresh }
                                    )
                                | _ -> ()

                            Diagnostic.emit "enforcer-cycle-repair" [ "session_id", key; "result", reason ]
                            let requestKey = BloggerRequestId.value (BloggerRequestContext.requestId fresh)

                            let rebuilt =
                                tryRebuildFromContext durable bloggerSessionId fresh
                                |> Option.defaultValue rawMessages

                            project (withRepairInstruction rebuilt requestKey)

                /// ENFORCER-066: durable InteractionRepair. No AABB transcript refresh.
                /// repairNudge is injected from HostSessionNudge (compile-order port).
                let interactionNudge
                    (ctx: BloggerRequestContext)
                    (terminalRun: ProviderRunIdentity)
                    (reason: string)
                    : Task<ContinuationOutcome> =
                    task {
                        match repairNudge with
                        | None ->
                            Diagnostic.emit
                                "enforcer-cycle-nudge-fail"
                                [ "session_id", key; "result", "no repair nudge port; " + reason ]

                            return aabbRepair ctx ("nudge-no-port: " + reason)
                        | Some send ->
                            let! sent =
                                send bloggerSessionId RepairInstruction None journal terminalRun "blogger-missing-tool"

                            match sent with
                            | Ok _ ->
                                // The durable claim written by the send is the nudge
                                // marker (ENFORCER-153); nothing mirrors it in memory.
                                Diagnostic.emit "enforcer-cycle-nudge" [ "session_id", key; "result", reason ]

                                // Nudge is a durable prompt_async; transform projects current view only.
                                return project rawMessages
                            | Error err when err.IndexOf("already claimed", StringComparison.OrdinalIgnoreCase) >= 0 ->
                                // ENFORCER-067: claim exists / pending — not failure; no AABB.
                                // The existing durable claim already identifies this nudge.
                                Diagnostic.emit "enforcer-cycle-nudge-pending" [ "session_id", key; "result", err ]

                                return project rawMessages
                            | Error err ->
                                // ENFORCER-067 immediate failure → AABB.
                                Diagnostic.emit "enforcer-cycle-nudge-fail" [ "session_id", key; "result", err ]

                                return aabbRepair ctx ("nudge-failed: " + err)
                    }

                if hasIncompleteBlogTool rawMessages then
                    return project rawMessages
                elif hasFailedBlogAttempt rawMessages then
                    // Interrupted tool call is NOT pure-prose nudge (ENFORCER-060/065).
                    // Original recovery: one AABB, then exhaust.
                    match liveCtx with
                    | Some ctx ->
                        match recoveryProbe durable bloggerSessionId rawMessages ctx with
                        | BloggerToolRecovery.AabbRepairConsumed ->
                            return fatalEnd "blog tool interrupted; aabb exhausted"
                        | _ -> return aabbRepair ctx "blog tool interrupted without completed call"
                    | None ->
                        // No live cycle: interrupted blog without authority is stop/abort residue,
                        // not a repair opportunity. Stop, never inject # Protocol repair.
                        return stop "unowned-interrupted-blog-without-CurrentRequest"
                elif hasAnyBlogToolPart rawMessages then
                    return project (rebuild ())
                elif not assistantCompleted then
                    return project (rebuild ())
                else
                    // ENFORCER-060/064..068: completed assistant, zero blog parts → pure prose.
                    let terminalRun =
                        match lastAssistantStep rawMessages with
                        | Some(messageId, _, _) when not (String.IsNullOrWhiteSpace messageId) ->
                            ProviderRunIdentity.create messageId
                        | _ -> ProviderRunIdentity.create "unknown-prose-run"

                    match liveCtx with
                    | None -> return stop "unowned-completed-prose-without-CurrentRequest"
                    | Some ctx ->
                        match recoveryProbe durable bloggerSessionId rawMessages ctx with
                        | BloggerToolRecovery.NoRecovery ->
                            return! interactionNudge ctx terminalRun "no completed blog calls (ENFORCER-060)"
                        | BloggerToolRecovery.InteractionNudgeIssued issuedRun when issuedRun = terminalRun ->
                            // ENFORCER-067: same terminal re-entry / transform re-fire — not failure.
                            // Do not AABB until a *new* pure-prose terminal arrives.
                            Diagnostic.emit
                                "enforcer-cycle-nudge-pending"
                                [ "session_id", key; "result", "same terminal re-entry while nudge in flight" ]

                            return project rawMessages
                        | BloggerToolRecovery.InteractionNudgeIssued _ ->
                            // Semantic failure: nudge accepted, new terminal still pure prose → AABB.
                            return aabbRepair ctx "nudge semantic failure; pure prose again (ENFORCER-067)"
                        | BloggerToolRecovery.AabbRepairConsumed ->
                            return fatalEnd "protocol-repair-exhausted (ENFORCER-060)"
            | Some durable, Some owner, Some(messageId, calls, assistantCompleted) when not (List.isEmpty calls) ->
                // ENFORCER-044: merge/commit on completed blog tool parts when this plugin
                // owns the cycle (live CurrentRequest).
                //
                // Host prompt.ts: transform msgs do NOT include the newly created
                // outbound assistant — lastAssistant is always the previous one.
                // processor.cleanup sets time.completed AFTER tools finish and BEFORE
                // the next loop iteration reloads msgs and re-triggers transform.
                // So the only Host trajectory that shows blog tool status=completed
                // also has assistant.time.completed. Skipping commit on that flag
                // freezes RecordCoverage: every later delta restarts at the origin
                // 200 KiB window with no fatal (silent stall).
                //
                // ENFORCER-154 alreadyEntry/alreadyReceipt still refuse re-commit.
                // liveCtx=None means we do not own this step — never invent authority.
                let mainSessionId = owner
                let providerRun = ProviderRunIdentity.create messageId
                let key = SessionId.value bloggerSessionId
                // Peek only — never heal InFlight from open on this arm.
                let liveCtx = tryLiveCycleContext scope bloggerSessionId

                let snapshot = AgentJournal.snapshot durable

                let alreadyEntry =
                    snapshot.AgentProjections.Sessions
                    |> Map.tryFind mainSessionId
                    |> Option.bind (fun session -> session.Enforcement)
                    |> Option.map (fun state -> EnforcementProjection.tryFindByProviderRun providerRun state)
                    |> Option.flatten
                    |> Option.isSome

                let alreadyReceipt =
                    snapshot.AgentProjections.Sessions
                    |> Map.tryFind mainSessionId
                    |> Option.bind (fun session -> session.BloggerCycles)
                    |> Option.bind (fun cycles -> BloggerCycleProjection.tryReceipt providerRun cycles)
                    |> Option.isSome

                let resumeWithContext ctx =
                    tryRebuildFromContext durable bloggerSessionId ctx
                    |> Option.defaultValue rawMessages

                let mainBlocks () =
                    BloggerRuntimeHost.blocksNew (Some durable) mainSessionId scope key

                /// Catch-up drain: one ≤200 KiB window from durable coverage; None = caught up.
                /// Stale PendingOffer is discarded — context must recompute from coverage (COMPANION-008).
                /// Caught-up / sealed → StopPhysicalRun so Host does not loop on tool calls.
                let resumeCatchUp (fallback: obj list) (caughtUpReason: string) : ContinuationOutcome =
                    if mainBlocks () then
                        BloggerRuntimeHost.forceSealRuntime scope key
                        stopPhysicalRun rawMessages fallback "main-sealed-blocks-request"
                    else
                        scope.TryTakePendingOffer key |> ignore

                        match tryRefreshMainContextFromJournal scope durable mainSessionId bloggerSessionId with
                        | Some ctx ->
                            match BloggerRuntime.adoptPendingAsCurrent (scope.GetBloggerRuntime key) ctx with
                            | Ok cell ->
                                scope.SetBloggerRuntime(key, cell)
                                scope.SetCurrentRequest(key, ctx)
                            | Error _ -> ()

                            project (resumeWithContext ctx)
                        | None ->
                            // Caught up. Durable seal ends reactivation permanently.
                            let cell = scope.GetBloggerRuntime key

                            if
                                AgentProjection.mainSealedForBlogger
                                    mainSessionId
                                    (AgentJournal.snapshot durable).AgentProjections
                            then
                                BloggerRuntimeHost.forceSealCellDropOffer scope key
                            else
                                match cell.State with
                                | BloggerRuntimeState.InFlight _ ->
                                    match BloggerRuntime.onCycleCommitted cell with
                                    | Ok parked -> scope.SetBloggerRuntime(key, parked)
                                    | Error _ -> ()
                                | _ -> ()

                            stopPhysicalRun rawMessages fallback caughtUpReason

                if alreadyEntry || alreadyReceipt then
                    // ENFORCER-154: same provider run already committed — drain remaining gap.
                    return resumeCatchUp rawMessages "idempotent-receipt-catch-up-complete"
                elif liveCtx.IsNone then
                    // No owned cycle. Unowned completed blog is protocol stop (not silent
                    // project): returning rawMessages alone lets Host tool-loop forever.
                    // Live unowned (assistant not completed) remains Diagnostic.fatal.
                    if assistantCompleted then
                        return stop "unowned-completed-blog-without-CurrentRequest"
                    else
                        match tryOpenByBlogger durable mainSessionId bloggerSessionId with
                        | Some _ ->
                            Diagnostic.fatal
                                "enforcer-cycle-failed"
                                [ "session_id", key; "result", "missing CurrentRequest" ]

                            return project rawMessages
                        | None ->
                            Diagnostic.fatal
                                "enforcer-cycle-failed"
                                [ "session_id", key; "result", "live blog without cycle authority" ]

                            return project rawMessages
                else
                    // PERSIST-010 precheck / concurrent coverage advance: abandon stale
                    // staged cycle then rebuild from live journal coverage. Must NOT
                    // resumeWithContext(liveCtx) — that freezes PreviousIngestedThrough
                    // at the pre-crash cursor and loops KnownNotCommitted forever.
                    let mutable disposition = CycleDisposition.Working

                    let fatalEnd (reason: string) =
                        Diagnostic.fatal "enforcer-cycle-failed" [ "session_id", key; "result", reason ]

                        BloggerAbandon.openRequest durable mainSessionId bloggerSessionId liveCtx reason

                        match BloggerRuntime.onFail (scope.GetBloggerRuntime key) with
                        | Ok cell -> scope.SetBloggerRuntime(key, cell)
                        | Error _ -> ()

                        scope.ClearCurrentRequest key

                    let unexpectedEnd (reason: string) = fatalEnd reason

                    /// KnownNotCommitted is recoverable: abandon open + Idle, then
                    /// resumeCatchUp re-chunks from projection.IngestedThroughSequence.
                    /// Must NOT Diagnostic.fatal — that SIGKILLs before catch-up runs.
                    let abandonStaleCycle (reason: string) =
                        Diagnostic.emit "enforcer-cycle-stale" [ "session_id", key; "result", reason ]

                        BloggerAbandon.openRequest durable mainSessionId bloggerSessionId liveCtx reason

                        match BloggerRuntime.onFail (scope.GetBloggerRuntime key) with
                        | Ok cell -> scope.SetBloggerRuntime(key, cell)
                        | Error _ -> ()

                        scope.ClearCurrentRequest key
                        disposition <- CycleDisposition.AbandonThenCatchUp

                    // ENFORCER-153: the AABB budget is derived from the transcript
                    // (the injected repair message for the live request IS the spent
                    // marker), never from a runtime mirror.
                    let aabbConsumed () =
                        match liveCtx with
                        | Some ctx ->
                            (recoveryProbe durable bloggerSessionId rawMessages ctx) = BloggerToolRecovery.AabbRepairConsumed
                        | None -> false

                    match validateCycle messageId calls with
                    | Error reason when
                        isEmptyTextCycleFailure reason
                        && not (aabbConsumed ())
                        && not (hasIncompleteBlogTool rawMessages)
                        ->
                        // ENFORCER-061: empty text keeps one AABB repair budget (not pure-prose nudge).
                        let fresh =
                            tryRefreshMainContextFromJournal scope durable mainSessionId bloggerSessionId
                            |> Option.orElse liveCtx

                        match fresh with
                        | None -> unexpectedEnd (reason + "; aabb-refresh-empty")
                        | Some freshCtx ->
                            disposition <- CycleDisposition.InjectRepair freshCtx
                            scope.SetCurrentRequest(key, freshCtx)

                            match scope.GetBloggerRuntime key with
                            | c ->
                                match c.State with
                                | BloggerRuntimeState.InFlight _ ->
                                    scope.SetBloggerRuntime(
                                        key,
                                        { c with
                                            State = BloggerRuntimeState.InFlight freshCtx }
                                    )
                                | _ -> ()

                            Diagnostic.emit "enforcer-cycle-repair" [ "session_id", key; "result", reason ]
                    | Error reason ->
                        if isEmptyTextCycleFailure reason && aabbConsumed () then
                            unexpectedEnd "protocol-repair-exhausted"
                        else
                            unexpectedEnd reason
                    | Ok(merged, toolCallIds) ->
                        match liveCtx with
                        | Some(BloggerRequestContext.Squash squash) ->
                            match
                                commitSquash durable mainSessionId bloggerSessionId providerRun squash merged.MergedText
                            with
                            | CycleCommitOutcome.KnownCommitted ->
                                disposition <- CycleDisposition.Committed None

                                match BloggerRuntime.onSquashCommitted (scope.GetBloggerRuntime key) None with
                                | Ok(cell, _) -> scope.SetBloggerRuntime(key, cell)
                                | Error _ -> ()

                                scope.ClearCurrentRequest key
                            | CycleCommitOutcome.KnownNotCommitted reason -> abandonStaleCycle reason
                            | CycleCommitOutcome.CommitUnknown reason ->
                                disposition <- CycleDisposition.CommitUnknown

                                Diagnostic.fatal "enforcer-cycle-commit-unknown" [ "session_id", key; "result", reason ]
                        | Some(BloggerRequestContext.Main main) ->
                            let tomlDigest = BlobDigest.create (HostDigest.sha256Hex main.Toml)

                            // First physical send materializes open with PromptKey after
                            // StartFromContext. Catch-up drain reuses live CurrentRequest
                            // without a new open slot — only the open that matches this
                            // RequestId must carry a PromptKey.
                            let openUnbound =
                                tryOpenByBlogger durable mainSessionId bloggerSessionId
                                |> Option.exists (fun openReq ->
                                    openReq.RequestId = main.RequestId && openReq.PromptKey.IsNone)

                            if tomlDigest <> main.DeltaDigest then
                                unexpectedEnd "delta digest mismatch"
                            elif main.NextIngestedThroughSequence <= main.PreviousIngestedThroughSequence then
                                unexpectedEnd "coverage did not advance"
                            elif openUnbound then
                                unexpectedEnd "open request has no PromptKey binding"
                            else
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
                                | CycleCommitOutcome.KnownCommitted ->
                                    disposition <- CycleDisposition.Committed None

                                    match BloggerRuntime.onCycleCommitted (scope.GetBloggerRuntime key) with
                                    | Ok cell ->
                                        // Handle may have sealed during the cycle.
                                        if
                                            AgentProjection.mainSealedForBlogger
                                                mainSessionId
                                                (AgentJournal.snapshot durable).AgentProjections
                                            && not (BloggerRuntime.isDrainOpen cell)
                                        then
                                            BloggerRuntimeHost.forceSealCellDropOffer scope key
                                        else
                                            scope.SetBloggerRuntime(key, cell)
                                    | Error _ -> ()

                                    scope.ClearCurrentRequest key
                                | CycleCommitOutcome.KnownNotCommitted reason -> abandonStaleCycle reason
                                | CycleCommitOutcome.CommitUnknown reason ->
                                    disposition <- CycleDisposition.CommitUnknown

                                    Diagnostic.fatal
                                        "enforcer-cycle-commit-unknown"
                                        [ "session_id", key; "result", reason ]
                        | None -> unexpectedEnd "missing CurrentRequest"

                    match disposition with
                    | CycleDisposition.InjectRepair ctx ->
                        // ENFORCER-062/067/068 bridge (empty text): the confirmed
                        // failure advances the primary A/A/B/B cursor through the ONE
                        // writer. Exhaustion forbids the automatic next attempt — no
                        // repair projection is injected. Replay of the same terminal
                        // run stays AlreadyRecorded and advances once.
                        let advanceOutcome =
                            match
                                FallbackController.recordConfirmedFailure
                                    durable
                                    AgentPairCursor.DefaultAutoRecoveryBudget
                                    mainSessionId
                                    (ProviderRunIdentity.create messageId)
                                    "blog empty text (ENFORCER-061)"
                            with
                            | Ok outcome -> Some outcome
                            | Error err ->
                                Diagnostic.emit
                                    "enforcer-aabb-bridge"
                                    [ "session_id", key; "result", "recordConfirmedFailure rejected: " + err ]

                                None

                        match advanceOutcome with
                        | Some(FallbackController.AdvanceOutcome.Exhausted _) ->
                            Diagnostic.emit
                                "enforcer-aabb-exhausted"
                                [ "session_id", key; "result", "blog empty text (ENFORCER-061)" ]

                            fatalEnd "blog aabb exhausted; auto-recovery budget spent"
                            return failwith "unreachable: fatalEnd ends the cycle"
                        | _ ->
                            let requestKey = BloggerRequestId.value (BloggerRequestContext.requestId ctx)
                            return project (withRepairInstruction (resumeWithContext ctx) requestKey)
                    | CycleDisposition.CommitUnknown -> return project rawMessages
                    | CycleDisposition.AbandonThenCatchUp ->
                        // Stale staged coverage abandoned: rebuild next window from live
                        // IngestedThroughSequence. resumeCatchUp sets CurrentRequest +
                        // InFlight when material remains; None = true catch-up stop.
                        return resumeCatchUp rawMessages "stale-cycle-catch-up-complete"
                    | CycleDisposition.Working ->
                        match liveCtx with
                        | Some ctx -> return project (resumeWithContext ctx)
                        | None -> return project rawMessages
                    | CycleDisposition.Committed afterSquashMain ->
                        if mainBlocks () then
                            BloggerRuntimeHost.forceSealRuntime scope key
                            return stop "main-sealed-after-commit"
                        else
                            // Drain contract: after commit, immediately take next ≤200 KiB window
                            // from durable coverage until catch-up. PendingOffer is a wake signal
                            // only — never prefer stale frozen context over re-chunk.
                            scope.TryTakePendingOffer key |> ignore

                            match
                                tryRefreshMainContextFromJournal scope durable mainSessionId bloggerSessionId,
                                afterSquashMain
                            with
                            | Some ctx, _
                            | None, Some ctx ->
                                let ctx =
                                    tryRefreshMainContextFromJournal scope durable mainSessionId bloggerSessionId
                                    |> Option.defaultValue ctx

                                match BloggerRuntime.adoptPendingAsCurrent (scope.GetBloggerRuntime key) ctx with
                                | Ok cell ->
                                    scope.SetBloggerRuntime(key, cell)
                                    scope.SetCurrentRequest(key, ctx)
                                | Error _ -> ()

                                return project (resumeWithContext ctx)
                            | None, None ->
                                // Caught up now. Durable seal closes DrainWindow permanently.
                                let cell = scope.GetBloggerRuntime key

                                if
                                    AgentProjection.mainSealedForBlogger
                                        mainSessionId
                                        (AgentJournal.snapshot durable).AgentProjections
                                then
                                    BloggerRuntimeHost.forceSealCellDropOffer scope key
                                    scope.ClearCurrentRequest key
                                    return stop "main-sealed-caught-up"
                                else
                                    match cell.State with
                                    | BloggerRuntimeState.InFlight _ ->
                                        match BloggerRuntime.onCycleCommitted cell with
                                        | Ok parked -> scope.SetBloggerRuntime(key, parked)
                                        | Error _ -> ()
                                    | _ -> ()

                                    let! resumed = scope.ParkTransform(key, ParkedTransformLifetime)

                                    if not resumed then
                                        if mainBlocks () then
                                            BloggerRuntimeHost.forceSealRuntime scope key
                                            return stop "park-ended-main-sealed"
                                        else
                                            // Re-check gap: InFlight wake may have arrived after last refresh.
                                            match
                                                tryRefreshMainContextFromJournal
                                                    scope
                                                    durable
                                                    mainSessionId
                                                    bloggerSessionId
                                            with
                                            | Some ctx ->
                                                match
                                                    BloggerRuntime.adoptPendingAsCurrent
                                                        (scope.GetBloggerRuntime key)
                                                        ctx
                                                with
                                                | Ok next ->
                                                    scope.SetBloggerRuntime(key, next)
                                                    scope.SetCurrentRequest(key, ctx)
                                                | Error _ -> ()

                                                return project (resumeWithContext ctx)
                                            | None ->
                                                // True catch-up after park lifetime: quiet stop (not fatal).
                                                // Never return [] — Host would blank messages → provider 400.
                                                return stop "park-ended-catch-up-complete"
                                    else
                                        scope.TryTakePendingOffer key |> ignore

                                        match
                                            tryRefreshMainContextFromJournal
                                                scope
                                                durable
                                                mainSessionId
                                                bloggerSessionId
                                        with
                                        | Some ctx ->
                                            if mainBlocks () then
                                                BloggerRuntimeHost.forceSealRuntime scope key
                                                return stop "park-resumed-main-sealed"
                                            else
                                                match
                                                    BloggerRuntime.adoptPendingAsCurrent
                                                        (scope.GetBloggerRuntime key)
                                                        ctx
                                                with
                                                | Ok next ->
                                                    scope.SetBloggerRuntime(key, next)
                                                    scope.SetCurrentRequest(key, ctx)
                                                | Error _ -> ()

                                                return project (resumeWithContext ctx)
                                        | None -> return project rawMessages
            | _ ->
                // COMPANION-005 first request / non-tool step: rebuild only from
                // durable frames + typed CurrentRequest. Never extract TOML from
                // raw user messages (C2).
                match journal, scope.TryPeekCurrentRequest(SessionId.value bloggerSessionId) with
                | Some durable, Some ctx ->
                    return
                        project (
                            tryRebuildFromContext durable bloggerSessionId ctx
                            |> Option.defaultValue rawMessages
                        )
                | _ -> return project rawMessages
        }
