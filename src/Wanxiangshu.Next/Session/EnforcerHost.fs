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

    /// C4: commit-path UTF-8 safety bounds (not nudge/throttle).
    let MaxBlogTextBytes = 512 * 1024
    let MaxEvidenceBytes = 128 * 1024
    let MaxSerializedScoresBytes = 64 * 1024
    let MaxMergedToolCalls = 32

    /// Item 14: three commit outcomes. Park only on KnownCommitted.
    [<RequireQualifiedAccess>]
    type CycleCommitOutcome =
        | KnownCommitted
        | KnownNotCommitted of reason: string
        | CommitUnknown of reason: string

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

    let private lastAssistantStep (rawMessages: obj list) : (string * obj list * bool) option =
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
        : (string * (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) list * bool) option =
        match lastAssistantStep rawMessages with
        | None -> None
        | Some(messageId, parts, completed) ->
            let catalog =
                rules |> List.map (fun rule -> rule.FieldName, rule.RuleId, rule.CatalogOrdinal)

            let calls =
                parts
                |> List.mapi (fun ordinal part -> ordinal, blogCallFromPart part)
                |> List.choose (fun (ordinal, parsed) ->
                    parsed
                    |> Option.map (fun (callId, input) ->
                        ordinal, callId, EnforcerCodec.decodeCall catalog (decodeObject input)))

            Some(messageId, calls, completed)

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
        elif List.length calls > MaxMergedToolCalls then
            Error(sprintf "blog cycle exceeds MaxMergedToolCalls=%d" MaxMergedToolCalls)
        else
            let callIds = calls |> List.map (fun (_, callId, _) -> callId)

            if List.length (List.distinct callIds) <> List.length calls then
                Error "blog cycle has duplicate ToolCallIds (ENFORCER-043)"
            else
                let merged =
                    EnforcerCycle.mergeCalls (calls |> List.map (fun (ordinal, _, call) -> ordinal, call))

                if not (EnforcerCycle.isValidCycle merged) then
                    Error "blog cycle merged text is empty after canonicalisation (ENFORCER-043)"
                elif SyntheticToml.byteCount merged.MergedText > MaxBlogTextBytes then
                    Error(sprintf "blog cycle text exceeds MaxBlogTextBytes=%d" MaxBlogTextBytes)
                elif SyntheticToml.byteCount merged.MergedEvidence > MaxEvidenceBytes then
                    Error(sprintf "blog cycle evidence exceeds MaxEvidenceBytes=%d" MaxEvidenceBytes)
                else
                    let scoresBytes =
                        if Map.isEmpty merged.MergedScores then
                            0
                        else
                            SyntheticToml.byteCount (CanonicalJson.canonicalJson (scoresToObj merged.MergedScores))

                    if scoresBytes > MaxSerializedScoresBytes then
                        Error(sprintf "blog cycle scores exceed MaxSerializedScoresBytes=%d" MaxSerializedScoresBytes)
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
                // C5: use epoch frozen at request materialization, never live PrefixEpoch.
                let epoch = coverage.ObservedPrefixEpochId

                match journal.WriteBlob merged.MergedText with
                | Error error -> CycleCommitOutcome.KnownNotCommitted error
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
                    | _, Error error -> CycleCommitOutcome.KnownNotCommitted error
                    | Ok scoreRef, Ok evidenceRef ->
                        let fact =
                            AgentFact.BlogEntryCommitted
                                {| SessionId = mainSessionId
                                   BloggerSessionId = bloggerSessionId
                                   RequestId = coverage.RequestId
                                   FrameEpochId = coverage.FrameEpochId
                                   PreviousIngestedThroughSequence = coverage.PreviousIngestedThroughSequence
                                   NextIngestedThroughSequence = coverage.NextIngestedThroughSequence
                                   PreviousCoverableTurnCutoffExclusive = coverage.PreviousCoverableTurnCutoffExclusive
                                   NextCoverableTurnCutoffExclusive = coverage.NextCoverableTurnCutoffExclusive
                                   NextCoveredPrefixDigest = coverage.NextCoveredPrefixDigest
                                   TextRef = textBlob.BlobRef
                                   TextDigest = textBlob.BlobDigest
                                   ProviderRun = providerRun
                                   ToolCallIds = toolCallIds
                                   ScoreVectorRef = scoreRef |> Option.map (fun blob -> blob.BlobRef)
                                   EvidenceRef = evidenceRef |> Option.map (fun blob -> blob.BlobRef)
                                   ObservedPrefixEpochId = epoch |}

                        match
                            AgentJournal.appendAgent (StreamId.Session mainSessionId) (Some providerRun) fact journal
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
                                    AgentFact.BlogSquashCommitted
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

                let plan =
                    CompanionProjectionBuilder.build
                        HostDigest.sha256Hex
                        bloggerSessionId
                        frameEpoch
                        kind
                        frameBodies
                        delta

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

    let private rebuildFromContext journal bloggerSessionId ctx =
        tryRebuildFromContext journal bloggerSessionId ctx |> Option.defaultValue []

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
                    | BloggerRuntimeState.Disposed -> None
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
                            "source", box "interaction-repair" ]
                  )
                  "parts", box [| createObj [ "type", box "text"; "text", box RepairInstruction ] |] ]

        rawMessages @ [ repairMsg ]

    let private isEmptyTextCycleFailure (reason: string) : bool =
        reason.IndexOf("merged text is empty", StringComparison.Ordinal) >= 0

    /// Rebuild provider-semantic turns from durable XTrace (AABB refresh source).
    let private projectionFromXTrace
        (journal: AgentJournal)
        (xTrace: XTraceProjectionState)
        : ProviderProjection.ProviderSemanticProjection =
        let byTurn =
            xTrace.Parts
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
                                let mediaType =
                                    if String.IsNullOrWhiteSpace body then
                                        None
                                    else
                                        Some body

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
    let internal mainContextFromChunk
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (observedEpoch: PrefixEpochId)
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

    /// AABB: re-chunk from current IngestedThrough against latest XTrace.
    /// Returns None when sealed or no material.
    let tryRefreshMainContextFromJournal
        (scope: IParkedTransformHost)
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        : BloggerRequestContext option =
        let key = SessionId.value bloggerSessionId
        let cell = scope.GetBloggerRuntime key
        let durableSealed =
            AgentProjection.mainSealedForBlogger mainSessionId (AgentJournal.snapshot journal).AgentProjections

        if BloggerRuntime.blocksNewRequest durableSealed cell then
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
            | Some chunk ->
                Some(
                    mainContextFromChunk
                        mainSessionId
                        bloggerSessionId
                        epoch
                        blog
                        xTrace
                        projection
                        chunk
                )

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
            | Some durable, Some owner, Some(_messageId, calls, assistantCompleted) when List.isEmpty calls ->
                // Host transform msgs do NOT include the newly created outbound assistant
                // (prompt.ts: updateMessage then trigger transform on prior msgs).
                // lastAssistant = historical tail. Empty completed-blog list means:
                // 1) pending/running blog — Host re-enters after tool completion
                // 2) abort cleanup: blog status=error+interrupted, assistant completed
                //    → NOT pure prose; fail closed if still InFlight
                // 3) outbound after prior success is non-empty extractCalls (other arm)
                // 4) pure prose terminal (no blog parts at all) — ENFORCER-060 once
                // 5) no live request — rebuild best-effort, never invent repair
                let key = SessionId.value bloggerSessionId

                let currentCtx =
                    match scope.TryPeekCurrentRequest key with
                    | Some c -> Some c
                    | None -> resolveCycleContext scope durable owner bloggerSessionId

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
                    rawMessages

                /// AABB once: prefer latest XTrace chunk; if none, keep old ctx (same prev).
                let aabbRepair (ctx: BloggerRequestContext) (reason: string) =
                    let cell = scope.GetBloggerRuntime key

                    if
                        AgentProjection.mainSealedForBlogger
                            owner
                            (AgentJournal.snapshot durable).AgentProjections
                        && not cell.ReactivatedAfterSeal
                    then
                        scope.SetBloggerRuntime(key, BloggerRuntime.forceSeal cell)
                        scope.ClearCurrentRequest key
                        scope.TryTakePendingOffer key |> ignore
                        rawMessages
                    else
                        scope.SetBloggerRuntime(key, BloggerRuntime.markRepairSpent cell)

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
                                        State = BloggerRuntimeState.InFlight fresh
                                        RepairSpent = true }
                                )
                            | _ -> ()

                        Diagnostic.emit "enforcer-cycle-repair" [ "session_id", key; "result", reason ]
                        let requestKey = BloggerRequestId.value (BloggerRequestContext.requestId fresh)

                        let rebuilt =
                            tryRebuildFromContext durable bloggerSessionId fresh
                            |> Option.defaultValue rawMessages

                        withRepairInstruction rebuilt requestKey

                if hasIncompleteBlogTool rawMessages then
                    return rawMessages
                elif hasFailedBlogAttempt rawMessages then
                    // Host cleanup after kill/abort: hanging blog → error+interrupted.
                    match currentCtx with
                    | Some ctx when not (scope.GetBloggerRuntime key).RepairSpent ->
                        return aabbRepair ctx "blog tool interrupted without completed call"
                    | Some _ -> return fatalEnd "blog tool interrupted; aabb exhausted"
                    | None -> return rebuild ()
                elif hasAnyBlogToolPart rawMessages then
                    return rebuild ()
                elif not assistantCompleted then
                    return rebuild ()
                else
                    // ENFORCER-060: completed assistant, zero blog parts → pure prose.
                    let cell = scope.GetBloggerRuntime key

                    match currentCtx with
                    | None -> return rebuild ()
                    | Some ctx when not cell.RepairSpent -> return aabbRepair ctx "no completed blog calls (ENFORCER-060)"
                    | Some _ -> return fatalEnd "protocol-repair-exhausted (ENFORCER-060)"
            | Some durable, Some owner, Some(messageId, calls, assistantCompleted) when not (List.isEmpty calls) ->
                // ENFORCER-044: merge/commit only on a LIVE tool-loop continuation.
                //
                // Host prompt.ts: transform msgs do NOT include the newly created
                // outbound assistant. lastAssistant is always the previous one.
                // A previous cycle's assistant has time.completed set; the in-flight
                // tool-loop assistant does not until the turn ends/cleanup.
                // Treating a completed historical tail as "this cycle" is the race
                // behind stale-cycle-after-abandon and cross-RequestId commit.
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
                    let cell = scope.GetBloggerRuntime key
                    let durableSealed =
                        AgentProjection.mainSealedForBlogger
                            mainSessionId
                            (AgentJournal.snapshot durable).AgentProjections

                    BloggerRuntime.blocksNewRequest durableSealed cell

                let takePendingOr (fallback: obj list) =
                    if mainBlocks () then
                        scope.SetBloggerRuntime(key, BloggerRuntime.forceSeal (scope.GetBloggerRuntime key))
                        scope.TryTakePendingOffer key |> ignore
                        scope.CancelParked key
                        fallback
                    else
                        match scope.TryTakePendingOffer key with
                        | Some ctx ->
                            // Prefer a refreshed chunk so resume eats latest progress.
                            let ctx =
                                tryRefreshMainContextFromJournal scope durable mainSessionId bloggerSessionId
                                |> Option.defaultValue ctx

                            match BloggerRuntime.adoptPendingAsCurrent (scope.GetBloggerRuntime key) ctx with
                            | Ok cell ->
                                scope.SetBloggerRuntime(key, cell)
                                scope.SetCurrentRequest(key, ctx)
                            | Error _ -> ()

                            resumeWithContext ctx
                        | None -> fallback

                if alreadyEntry || alreadyReceipt then
                    // ENFORCER-154: same provider run already committed — offer only.
                    return takePendingOr rawMessages
                elif assistantCompleted then
                    match liveCtx with
                    | Some ctx -> return resumeWithContext ctx
                    | None -> return takePendingOr rawMessages
                else
                    let mutable committed = false
                    let mutable afterSquashMain: BloggerRequestContext option = None
                    let mutable commitUnknown = false
                    let mutable injectRepair = false
                    let mutable repairCtx: BloggerRequestContext option = None

                    let fatalEnd (reason: string) =
                        Diagnostic.fatal "enforcer-cycle-failed" [ "session_id", key; "result", reason ]

                        BloggerAbandon.openRequest durable mainSessionId bloggerSessionId liveCtx reason

                        match BloggerRuntime.onFail (scope.GetBloggerRuntime key) with
                        | Ok cell -> scope.SetBloggerRuntime(key, cell)
                        | Error _ -> ()

                        scope.ClearCurrentRequest key

                    let unexpectedEnd (reason: string) = fatalEnd reason

                    match liveCtx with
                    | None ->
                        match tryOpenByBlogger durable mainSessionId bloggerSessionId with
                        | Some _ -> unexpectedEnd "missing CurrentRequest"
                        | None -> unexpectedEnd "live blog without cycle authority"
                    | Some _ ->
                        match validateCycle messageId calls with
                        | Error reason when
                            isEmptyTextCycleFailure reason
                            && not (scope.GetBloggerRuntime key).RepairSpent
                            && not (hasIncompleteBlogTool rawMessages)
                            ->
                            injectRepair <- true

                            let fresh =
                                tryRefreshMainContextFromJournal scope durable mainSessionId bloggerSessionId
                                |> Option.orElse liveCtx

                            match fresh with
                            | None -> unexpectedEnd (reason + "; aabb-refresh-empty")
                            | Some freshCtx ->
                                repairCtx <- Some freshCtx
                                let cell = scope.GetBloggerRuntime key
                                scope.SetBloggerRuntime(key, BloggerRuntime.markRepairSpent cell)
                                scope.SetCurrentRequest(key, freshCtx)

                                match scope.GetBloggerRuntime key with
                                | c ->
                                    match c.State with
                                    | BloggerRuntimeState.InFlight _ ->
                                        scope.SetBloggerRuntime(
                                            key,
                                            { c with
                                                State = BloggerRuntimeState.InFlight freshCtx
                                                RepairSpent = true }
                                        )
                                    | _ -> ()

                                Diagnostic.emit "enforcer-cycle-repair" [ "session_id", key; "result", reason ]
                        | Error reason ->
                            if isEmptyTextCycleFailure reason && (scope.GetBloggerRuntime key).RepairSpent then
                                unexpectedEnd "protocol-repair-exhausted"
                            else
                                unexpectedEnd reason
                        | Ok(merged, toolCallIds) ->
                            match liveCtx with
                            | Some(BloggerRequestContext.Squash squash) ->
                                match
                                    commitSquash
                                        durable
                                        mainSessionId
                                        bloggerSessionId
                                        providerRun
                                        squash
                                        merged.MergedText
                                with
                                | CycleCommitOutcome.KnownCommitted ->
                                    committed <- true
                                    afterSquashMain <- None

                                    match BloggerRuntime.onSquashCommitted (scope.GetBloggerRuntime key) None with
                                    | Ok(cell, _) -> scope.SetBloggerRuntime(key, cell)
                                    | Error _ -> ()

                                    scope.ClearCurrentRequest key
                                | CycleCommitOutcome.KnownNotCommitted reason -> unexpectedEnd reason
                                | CycleCommitOutcome.CommitUnknown reason ->
                                    commitUnknown <- true

                                    Diagnostic.fatal
                                        "enforcer-cycle-commit-unknown"
                                        [ "session_id", key; "result", reason ]
                            | Some(BloggerRequestContext.Main main) ->
                                let tomlDigest = BlobDigest.create (HostDigest.sha256Hex main.Toml)

                                if tomlDigest <> main.DeltaDigest then
                                    unexpectedEnd "delta digest mismatch"
                                elif main.NextIngestedThroughSequence <= main.PreviousIngestedThroughSequence then
                                    unexpectedEnd "coverage did not advance"
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
                                        committed <- true

                                        match BloggerRuntime.onCycleCommitted (scope.GetBloggerRuntime key) with
                                        | Ok cell ->
                                            // Handle may have sealed during the cycle.
                                            if
                                                AgentProjection.mainSealedForBlogger
                                                    mainSessionId
                                                    (AgentJournal.snapshot durable).AgentProjections
                                                && not cell.ReactivatedAfterSeal
                                            then
                                                scope.SetBloggerRuntime(key, BloggerRuntime.forceSeal cell)
                                                scope.TryTakePendingOffer key |> ignore
                                            else
                                                scope.SetBloggerRuntime(key, cell)
                                        | Error _ -> ()

                                        scope.ClearCurrentRequest key
                                    | CycleCommitOutcome.KnownNotCommitted reason -> unexpectedEnd reason
                                    | CycleCommitOutcome.CommitUnknown reason ->
                                        commitUnknown <- true

                                        Diagnostic.fatal
                                            "enforcer-cycle-commit-unknown"
                                            [ "session_id", key; "result", reason ]
                            | None -> unexpectedEnd "missing CurrentRequest"

                    if injectRepair then
                        match repairCtx with
                        | Some ctx ->
                            let requestKey = BloggerRequestId.value (BloggerRequestContext.requestId ctx)
                            return withRepairInstruction (resumeWithContext ctx) requestKey
                        | None -> return rawMessages
                    elif commitUnknown then
                        return rawMessages
                    elif not committed then
                        match liveCtx with
                        | Some ctx -> return resumeWithContext ctx
                        | None -> return rawMessages
                    else if mainBlocks () then
                        scope.SetBloggerRuntime(key, BloggerRuntime.forceSeal (scope.GetBloggerRuntime key))
                        scope.TryTakePendingOffer key |> ignore
                        scope.CancelParked key
                        return rawMessages
                    else
                        match scope.TryTakePendingOffer key, afterSquashMain with
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

                            return resumeWithContext ctx
                        | None, None ->
                            let! resumed = scope.ParkTransform(key, ParkedTransformLifetime)

                            if not resumed then
                                // Timeout with durable seal or no material: quiet stop.
                                // Timeout while still blocked falsely: already sealed above.
                                if mainBlocks () then
                                    scope.SetBloggerRuntime(
                                        key,
                                        BloggerRuntime.forceSeal (scope.GetBloggerRuntime key)
                                    )

                                    scope.ClearCurrentRequest key
                                    scope.TryTakePendingOffer key |> ignore
                                    return []
                                else
                                    // Still open gap after park lifetime with no offer = stuck.
                                    unexpectedEnd "park-timeout with pending material"
                                    return []
                            else
                                match scope.TryTakePendingOffer key with
                                | Some ctx ->
                                    let ctx =
                                        tryRefreshMainContextFromJournal scope durable mainSessionId bloggerSessionId
                                        |> Option.defaultValue ctx

                                    if mainBlocks () then
                                        scope.SetBloggerRuntime(
                                            key,
                                            BloggerRuntime.forceSeal (scope.GetBloggerRuntime key)
                                        )

                                        return rawMessages
                                    else
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
                | Some durable, Some ctx ->
                    return
                        tryRebuildFromContext durable bloggerSessionId ctx
                        |> Option.defaultValue rawMessages
                | _ -> return rawMessages
        }
