namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// How durable history becomes the facts an Enforcer continuation needs:
/// journal frame load, resolved frames, request-context rebuild and
/// XTrace/context recovery.
module EnforcerFrameRecovery =

    type FrameLoadError =
        | MissingAssociation
        | MissingBlogSession
        | MissingFrameBlob of digest: string
        | DigestMismatch of digest: string
        | EpochMismatch

    /// C6: unique fail-closed loader for effective BlogFrames.
    /// Silent List.choose drop of bad frames is forbidden.
    /// Kind is preserved so ProjectionSnapshot.BlogFrames can carry ProjectionBlogFrameKind.
    let loadEffectiveFrames
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        : Result<ResolvedBlogFrame list * FrameEpochId, FrameLoadError> =
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
                                    let kind =
                                        match frame.Kind with
                                        | BlogFrameKind.Entry -> ProjectionBlogFrameKind.Entry
                                        | BlogFrameKind.Squash -> ProjectionBlogFrameKind.Squash

                                    let resolved: ResolvedBlogFrame =
                                        { Kind = kind
                                          Digest = BlobDigest.value frame.Digest
                                          Body = text }

                                    load rest (resolved :: acc)

                    load blog.Frames []

    /// ENFORCER-051 / PROJ-008 step 3b: rebuild via Projection Algebra.
    /// Snapshot → InsertBlogFrames → Planner → Builder-shaped Host messages.
    /// Missing association / frame load → None so the caller keeps rawMessages.
    /// Never return an empty list: that blanks the Host transcript (mock lastUser=null).
    let tryRebuildFromContext
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
            | Ok(resolvedFrames, frameEpoch) ->
                let requestKind, squashCount, delta =
                    match ctx with
                    | BloggerRequestContext.Main main ->
                        let messageId =
                            CompanionIdentity.newWorkMessageId HostDigest.sha256Hex bloggerSessionId main.DeltaDigest

                        "normal", 0, Some(messageId, main.Toml)
                    | BloggerRequestContext.Squash squash -> "squash", squash.CoveredFrameCount, None

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

                let blogFramesIntent: BlogFramesIntent =
                    { RequestKind = requestKind
                      SquashFrameCount = squashCount
                      BloggerSessionId = SessionId.value bloggerSessionId
                      FrameEpoch = FrameEpochId.value frameEpoch
                      PhysicalDelta = delta
                      PreviousTips = previousTips }

                let emptyCurrent: ProviderProjection.ProviderSemanticProjection =
                    { ProviderId = None
                      ModelId = None
                      Variant = None
                      Tools = []
                      System = []
                      Messages = [] }

                let snapshot: ProjectionSnapshot =
                    { CurrentProjection = emptyCurrent
                      CommittedPrefix = None
                      BlogFrames = resolvedFrames
                      TransportMessages = Set.empty
                      HostReanchor = None }

                let intents = [ ProjectionIntent.InsertBlogFrames blogFramesIntent ]

                match ProjectionPlanner.plan intents with
                | Error _ -> None
                | Ok ordered ->
                    // PROJ-004：Canonical Renderer 单次产出 wire + Host id 侧信道。
                    // 禁止二次 CompanionProjectionBuilder.build。
                    let rendered =
                        ProjectionRenderer.renderMessagesWithHostIds HostDigest.sha256Hex snapshot [] ordered

                    let n = List.length rendered.Messages

                    if
                        List.length rendered.HostMessageIds <> n
                        || List.length rendered.HostIsPhysical <> n
                    then
                        None
                    else
                        // C6: rebuild frames/instruction are synthetic projections, not new
                        // user authority. New Work delta is marked physical for diagnostics;
                        // HOST-010 still binds authority pre-transform.
                        // Fail-closed：每条 rebuild 消息必须带代数 MessageId。
                        let zipped =
                            List.zip3 rendered.Messages rendered.HostMessageIds rendered.HostIsPhysical

                        let rec toHost
                            (acc: obj list)
                            (items: (ProviderProjection.WireMessage * string option * bool) list)
                            =
                            match items with
                            | [] -> Some(List.rev acc)
                            | (msg, None, _) :: _ -> None
                            | (msg, Some messageId, isPhysical) :: tail ->
                                let text =
                                    msg.Parts
                                    |> List.tryPick (function
                                        | ProviderProjection.WireText t -> Some t
                                        | _ -> None)
                                    |> Option.defaultValue ""

                                let hostMsg =
                                    createObj
                                        [ "info",
                                          box (
                                              createObj
                                                  [ "id", box messageId
                                                    "role", box msg.Role
                                                    "synthetic", box (not isPhysical)
                                                    "source",
                                                    box (
                                                        if isPhysical then
                                                            "physical-delta"
                                                        else
                                                            "synthetic-projection"
                                                    ) ]
                                          )
                                          "parts", box [| createObj [ "type", box "text"; "text", box text ] |] ]

                                toHost (hostMsg :: acc) tail

                        toHost [] zipped

    /// Dead-code hygiene: never default a rebuild miss to []. Callers that still
    /// need a list must pass the Host rawMessages as fallback.
    let rebuildFromContext journal bloggerSessionId ctx (fallback: obj list) =
        tryRebuildFromContext journal bloggerSessionId ctx
        |> Option.defaultValue fallback

    /// Map chunk NextCursor (first unconsumed semantic position) → XTrace sequence
    /// of the last COVERED part. Paired with `semanticCursorFor`'s `>`: the next
    /// delta starts strictly after this sequence (COMPANION-003 / CTX-011).
    ///
    /// Scoped to the current reanchor generation's Turn/Part labels (HOST-006).
    /// `None` = mapping failed (empty trace, or Host cursor not present on XTrace).
    /// NEVER default to 0: silent 0 with Prev>0 stages Next≤Prev and dies at commit.
    let lastCoveredSequence (xTrace: XTraceProjectionState) (nextCursor: SemanticCursor) : int64 option =
        XTraceProjection.currentGenerationParts xTrace.Parts
        |> List.tryFindBack (fun part ->
            part.Turn < nextCursor.TurnIndex
            || (part.Turn = nextCursor.TurnIndex && part.PartIndex < nextCursor.PartIndex))
        |> Option.map (fun part -> part.Cursor.Sequence)

    /// COMPANION-011: digest of X's provider-visible prefix at the coverable cutoff.
    /// When the cutoff does not move, the previous digest is kept so a mid-turn
    /// chunk cannot rewrite a proof that still describes the same turns.
    let coveredPrefixDigest
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
            | Some req -> tryReloadRequestContext journal req
