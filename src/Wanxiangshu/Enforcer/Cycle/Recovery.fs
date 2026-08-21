namespace Wanxiangshu.Enforcer.Cycle

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Host
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Session
open Wanxiangshu.Enforcer
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

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

    let private projectionBlogFrameKind kind =
        match kind with
        | BlogFrameKind.Entry -> ProjectionBlogFrameKind.Entry
        | BlogFrameKind.Squash -> ProjectionBlogFrameKind.Squash

    let private ensureFrameDigest (frame: BlogFrame) (text: string) =
        let digest = BlobDigest.value frame.Digest

        if HostDigest.sha256Hex text = digest then
            Ok()
        else
            Error(FrameLoadError.DigestMismatch digest)

    let private resolveFrameBlob (journal: AgentJournal) (frame: BlogFrame) =
        taskResult {
            let digest = BlobDigest.value frame.Digest

            let! text =
                journal.Writer.BlobWriter.Read frame.TextRef
                |> TaskResult.mapError (fun _ -> FrameLoadError.MissingFrameBlob digest)

            do! ensureFrameDigest frame text

            return
                { Kind = projectionBlogFrameKind frame.Kind
                  Digest = digest
                  Body = text }
        }

    let private loadBlogFrames (journal: AgentJournal) (blog: BlogProjectionState) =
        taskResult {
            let! frames =
                BlogProjection.frames blog
                |> TaskResultList.traverseM (resolveFrameBlob journal)

            return frames, blog.FrameEpochId
        }

    let private loadSessionBlogFrames (journal: AgentJournal) (session: SessionAgentProjection) =
        task {
            let blog = session.Blog |> Option.defaultValue BlogProjection.empty

            if List.isEmpty blog.Frames then
                return Ok([], blog.FrameEpochId)
            else
                return! loadBlogFrames journal blog
        }

    /// C6: unique fail-closed loader for effective BlogFrames.
    /// Silent List.choose drop of bad frames is forbidden.
    /// Kind is preserved so ProjectionSnapshot.BlogFrames can carry ProjectionBlogFrameKind.
    let loadEffectiveFrames
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        : Task<Result<ResolvedBlogFrame list * FrameEpochId, FrameLoadError>> =
        task {
            let projections = AgentJournal.snapshot journal

            let association =
                SessionAssociationProjection.tryBloggerOf mainSessionId projections.AgentProjections.Associations

            let session = projections.AgentProjections.Sessions |> Map.tryFind mainSessionId

            match association, session with
            | None, _ -> return Error FrameLoadError.MissingAssociation
            | Some _, None -> return Error FrameLoadError.MissingBlogSession
            | Some _, Some session -> return! loadSessionBlogFrames journal session
        }

    let private requestKindEvidence (bloggerSessionId: SessionId) (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main ->
            let messageId =
                CompanionIdentity.newWorkMessageId HostDigest.sha256Hex bloggerSessionId main.DeltaDigest

            "normal", 0, Some(messageId, main.Toml)
        | BloggerRequestContext.Squash squash -> "squash", squash.CoveredFrameCount, None

    let private previousTipsOf (projections: ProjectionSet) (owner: SessionId) =
        match projections.AgentProjections.Sessions |> Map.tryFind owner with
        | Some session ->
            session.Enforcement
            |> Option.map EnforcementProjection.recentTips
            |> Option.defaultValue []
            |> List.map (fun tip -> tip.FieldName, tip.CycleId)
        | None -> []

    let private hostSourceLabel isPhysical =
        if isPhysical then
            "physical-delta"
        else
            "synthetic-projection"

    let private wireText (msg: ProviderProjection.WireMessage) =
        msg.Parts
        |> List.tryPick (function
            | ProviderProjection.WireText t -> Some t
            | _ -> None)
        |> Option.defaultValue ""

    let private toHostMessage (msg: ProviderProjection.WireMessage, messageId: string option, isPhysical: bool) =
        match messageId with
        | None -> None
        | Some id ->
            Some(
                createObj
                    [ "info",
                      box (
                          createObj
                              [ "id", box id
                                "role", box msg.Role
                                "synthetic", box (not isPhysical)
                                "source", box (hostSourceLabel isPhysical) ]
                      )
                      "parts", box [| createObj [ "type", box "text"; "text", box (wireText msg) ] |] ]
            )

    let private messagesToHost (items: (ProviderProjection.WireMessage * string option * bool) list) : obj list option =
        let rec fold acc remaining =
            match remaining with
            | [] -> Some(List.rev acc)
            | head :: tail -> toHostMessage head |> Option.bind (fun hostMsg -> fold (hostMsg :: acc) tail)

        fold [] items

    let private renderValidatedHostMessages (snapshot: ProjectionSnapshot) ordered =
        let rendered =
            ProjectionRenderer.renderMessagesWithHostIds HostDigest.sha256Hex snapshot [] ordered

        let n = List.length rendered.Messages

        if
            List.length rendered.HostMessageIds <> n
            || List.length rendered.HostIsPhysical <> n
        then
            None
        else
            List.zip3 rendered.Messages rendered.HostMessageIds rendered.HostIsPhysical
            |> messagesToHost

    let private renderPlannedHostMessages (snapshot: ProjectionSnapshot) intents =
        match ProjectionPlanner.plan intents with
        | Error _ -> None
        | Ok ordered -> renderValidatedHostMessages snapshot ordered

    let private rebuildForOwner
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (owner: SessionId)
        (ctx: BloggerRequestContext)
        (projections: ProjectionSet)
        : Task<obj list option> =
        task {
            // Zero frames is legitimate (first Main before any Entry). Missing
            // association was already filtered. Blob load still fail-closed.
            match! loadEffectiveFrames journal owner with
            | Error FrameLoadError.MissingAssociation
            | Error FrameLoadError.MissingBlogSession
            | Error(FrameLoadError.MissingFrameBlob _)
            | Error(FrameLoadError.DigestMismatch _)
            | Error FrameLoadError.EpochMismatch -> return None
            | Ok(resolvedFrames, frameEpoch) ->
                let requestKind, squashCount, delta = requestKindEvidence bloggerSessionId ctx
                let previousTips = previousTipsOf projections owner
                let lang = ProviderProse.languageOf owner

                let blogFramesIntent: BlogFramesIntent =
                    { RequestKind = requestKind
                      SquashFrameCount = squashCount
                      BloggerSessionId = SessionId.value bloggerSessionId
                      FrameEpoch = FrameEpochId.value frameEpoch
                      PhysicalDelta = delta
                      PreviousTips = previousTips
                      NormalInstructionLines = ProviderProse.instructionLines lang CompanionPrompt.Normal Map.empty
                      SquashInstructionLines = ProviderProse.instructionLines lang CompanionPrompt.Squash Map.empty }

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

                return renderPlannedHostMessages snapshot [ ProjectionIntent.InsertBlogFrames blogFramesIntent ]
        }

    /// ENFORCER-051 / PROJ-008 step 3b: rebuild via Projection Algebra.
    /// Snapshot → InsertBlogFrames → Planner → Builder-shaped Host messages.
    /// Missing association / frame load → None so the caller keeps rawMessages.
    /// Never return an empty list: that blanks the Host transcript (mock lastUser=null).
    let tryRebuildFromContext
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (ctx: BloggerRequestContext)
        : Task<obj list option> =
        task {
            let projections = AgentJournal.snapshot journal

            let mainSessionId =
                SessionAssociationProjection.tryMainSessionOf bloggerSessionId projections.AgentProjections.Associations

            match mainSessionId with
            | None -> return None
            | Some owner -> return! rebuildForOwner journal bloggerSessionId owner ctx projections
        }

    /// Dead-code hygiene: never default a rebuild miss to []. Callers that still
    /// need a list must pass the Host rawMessages as fallback.
    let rebuildFromContext journal bloggerSessionId ctx (fallback: obj list) : Task<obj list> =
        task {
            let! rebuilt = tryRebuildFromContext journal bloggerSessionId ctx
            return rebuilt |> Option.defaultValue fallback
        }

    /// Map chunk NextCursor (first unconsumed semantic position) → XTrace sequence
    /// of the last COVERED part. Paired with `semanticCursorFor`'s `>`: the next
    /// delta starts strictly after this sequence (COMPANION-003 / CTX-011).
    ///
    /// Scoped to the current reanchor generation's Turn/Part labels (HOST-006).
    /// `None` = mapping failed (empty trace, or Host cursor not present on XTrace).
    /// NEVER default to 0: silent 0 with Prev>0 stages Next≤Prev and dies at commit.
    let lastCoveredSequence (xTrace: XTraceProjectionState) (nextCursor: SemanticCursor) : int64 option =
        XTraceProjection.currentGenerationParts (XTraceProjection.parts xTrace)
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

    let private hasJsonKey (raw: obj) (key: string) : bool =
        emitJsExpr (raw, key) "$0 != null && Object.prototype.hasOwnProperty.call($0, $1)"

    let private tryJsonField (raw: obj) (key: string) : obj option =
        if hasJsonKey raw key then Some(raw?(key)) else None

    let private asString (raw: obj) (key: string) : string =
        match tryJsonField raw key with
        | None -> ""
        | Some value when isNull value -> ""
        | Some value when emitJsExpr value "typeof $0 === 'string'" -> unbox<string> value
        | Some value when emitJsExpr value "typeof $0 === 'number'" -> string (unbox<float> value)
        | Some _ -> ""

    let private parseInt64Text (text: string) : int64 option =
        if String.IsNullOrWhiteSpace text then
            None
        else
            Some(int64 (float text))

    let private asInt64 (raw: obj) (key: string) : int64 option =
        match tryJsonField raw key with
        | None -> None
        | Some value when isNull value -> None
        | Some value when emitJsExpr value "typeof $0 === 'number'" -> Some(int64 (unbox<float> value))
        | Some value when emitJsExpr value "typeof $0 === 'bigint'" ->
            Some(int64 (unbox<float> (emitJsExpr value "Number($0)")))
        | Some value when emitJsExpr value "typeof $0 === 'string'" -> parseInt64Text (unbox<string> value)
        | Some _ -> None

    let private parseIntText (text: string) : int option =
        if String.IsNullOrWhiteSpace text then
            None
        else
            Some(int (float text))

    let private asInt (raw: obj) (key: string) : int option =
        match tryJsonField raw key with
        | None -> None
        | Some value when isNull value -> None
        | Some value when emitJsExpr value "typeof $0 === 'number'" -> Some(int (unbox<float> value))
        | Some value when emitJsExpr value "typeof $0 === 'string'" -> parseIntText (unbox<string> value)
        | Some _ -> None

    let private resolveDeltaDigest (openReq: OpenBloggerRequest) toml deltaDigestRaw =
        if not (String.IsNullOrWhiteSpace deltaDigestRaw) then
            BlobDigest.create deltaDigestRaw
        elif String.IsNullOrWhiteSpace toml then
            openReq.ContextDigest
        else
            BlobDigest.create (HostDigest.sha256Hex toml)

    let private decodeParsedContext (openReq: OpenBloggerRequest) (raw: obj) : BloggerRequestContext option =
        if openReq.RequestKind = "squash" then
            let covered =
                asInt raw "covered_frame_count"
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
            let toml = asString raw "toml"
            let deltaDigestRaw = asString raw "delta_digest"
            let deltaDigest = resolveDeltaDigest openReq toml deltaDigestRaw

            let prevIngest =
                asInt64 raw "prev_ingest"
                |> Option.defaultValue openReq.PreviousIngestedThroughSequence

            let nextIngest =
                asInt64 raw "next_ingest"
                |> Option.defaultValue openReq.NextIngestedThroughSequence

            Some(
                BloggerRequestContext.Main
                    { RequestId = openReq.RequestId
                      MainSessionId = openReq.MainSessionId
                      BloggerSessionId = openReq.BloggerSessionId
                      Toml = toml
                      PreviousIngestedThroughSequence = prevIngest
                      NextIngestedThroughSequence = nextIngest
                      PreviousCoverableTurnCutoffExclusive = asInt raw "prev_cutoff" |> Option.defaultValue 0
                      NextCoverableTurnCutoffExclusive = asInt raw "next_cutoff" |> Option.defaultValue 0
                      NextCoveredPrefixDigest = asString raw "next_prefix_digest"
                      FrameEpochId = openReq.FrameEpochId
                      DeltaDigest = deltaDigest
                      ObservedPrefixEpochId = openReq.ObservedPrefixEpochId }
            )

    let private decodeRequestContextJson (openReq: OpenBloggerRequest) (json: string) : BloggerRequestContext option =
        try
            decodeParsedContext openReq (Fable.Core.JS.JSON.parse json)
        with _ ->
            None

    /// C5: inverse of BloggerCoordinator.materializeRequest blob.
    /// Full typed context — never leave cutoff/digest at zero defaults.
    let tryReloadRequestContext
        (journal: AgentJournal)
        (openReq: OpenBloggerRequest)
        : Task<BloggerRequestContext option> =
        task {
            match! journal.Writer.BlobWriter.Read openReq.ContextRef with
            | Error _ -> return None
            | Ok json -> return decodeRequestContextJson openReq json
        }

    /// Live commit authority: InFlight payload only.
    /// Completed-blog transform must NEVER heal InFlight from durable open —
    /// Host msgs end on the historical last assistant (new outbound shell is
    /// not in the list). Healing open here re-binds a new RequestId to an old
    /// provider run (stale-cycle race). Crash recovery re-arms InFlight before
    /// handleContinuation when the open request is still live.
    let tryLiveCycleContext (scope: IBloggerRuntimeHost) (bloggerSessionId: SessionId) : BloggerRequestContext option =
        scope.TryPeekCurrentRequest(SessionId.value bloggerSessionId)

    let private tryOpenBloggerRequest (journal: AgentJournal) mainSessionId bloggerSessionId =
        (AgentJournal.snapshot journal).AgentProjections.Sessions
        |> Map.tryFind mainSessionId
        |> Option.bind (fun session -> session.BloggerCycles)
        |> Option.bind (fun cycles -> BloggerCycleProjection.tryOpenByBlogger bloggerSessionId cycles)

    let private reloadOpenCycleContext journal mainSessionId bloggerSessionId =
        task {
            match tryOpenBloggerRequest journal mainSessionId bloggerSessionId with
            | None -> return None
            | Some req -> return! tryReloadRequestContext journal req
        }

    /// Rebuild / empty-calls only: live InFlight, else reload open without
    /// committing. Does not claim physical flight (no side effect on authority).
    let resolveCycleContext
        (scope: IBloggerRuntimeHost)
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        : Task<BloggerRequestContext option> =
        task {
            let key = SessionId.value bloggerSessionId

            match scope.TryPeekCurrentRequest key with
            | Some ctx -> return Some ctx
            | None -> return! reloadOpenCycleContext journal mainSessionId bloggerSessionId
        }
