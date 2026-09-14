namespace Wanxiangshu.Enforcer.Cycle

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection

open Wanxiangshu.Execution.Session
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation.Identity

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

            return frame.Digest, text
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
    let loadEffectiveFrames
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        : Task<Result<(BlobDigest * string) list * FrameEpochId, FrameLoadError>> =
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

            CompanionRequestKind.Normal, Some(messageId, main.Items)
        | BloggerRequestContext.Squash squash -> CompanionRequestKind.Squash squash.CoveredFrameCount, None

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
        let rendered = ProjectionRenderer.renderMessagesWithHostIds snapshot [] ordered

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
                let requestKind, delta = requestKindEvidence bloggerSessionId ctx
                let previousTips = previousTipsOf projections owner
                let lang = ProviderProse.languageOf owner

                let emptyCurrent: ProviderProjection.ProviderSemanticProjection =
                    { ProviderId = None
                      ModelId = None
                      Variant = None
                      Tools = []
                      System = []
                      Messages = [] }

                let snapshot: ProjectionSnapshot = { CurrentProjection = emptyCurrent }

                let intent =
                    CompanionProjectionBuilder.projectionIntent
                        HostDigest.sha256Hex
                        bloggerSessionId
                        frameEpoch
                        requestKind
                        resolvedFrames
                        delta
                        previousTips
                        (ProviderProse.instructionLines lang CompanionPrompt.Normal Map.empty)
                        (ProviderProse.instructionLines lang CompanionPrompt.Squash Map.empty)

                return
                    intent
                    |> Option.bind (fun value -> renderPlannedHostMessages snapshot [ value ])
        }

    /// ENFORCER-051 / PROJ-008 step 3b: rebuild via Projection Algebra.
    /// Companion-owned rows → Planner → generic renderer → Host messages.
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

    /// Why a durable open request could not be reloaded as a typed context.
    /// W6: corrupt, version-incompatible and unreadable durable input are
    /// distinct typed rejections — never a silent None/zero default.
    [<RequireQualifiedAccess>]
    type CycleContextReloadRejection =
        /// The context blob could not be read (I/O unavailable).
        | BlobUnreadable of reason: string
        /// The context blob is not parseable JSON.
        | BlobCorrupt of reason: string
        /// The durable request kind names no known context shape.
        | UnsupportedRequestKind of kind: string
        /// The blob's delta items do not decode.
        | ItemsUndecodable of reason: string
        /// The decoded candidate violates the request invariants.
        | InvariantViolated of BloggerRequestRejection

    let reloadRejectionLabel (rejection: CycleContextReloadRejection) : string =
        match rejection with
        | CycleContextReloadRejection.BlobUnreadable reason -> $"context blob unreadable: {reason}"
        | CycleContextReloadRejection.BlobCorrupt reason -> $"context blob corrupt: {reason}"
        | CycleContextReloadRejection.UnsupportedRequestKind kind -> $"unsupported request kind: {kind}"
        | CycleContextReloadRejection.ItemsUndecodable reason -> $"delta items undecodable: {reason}"
        | CycleContextReloadRejection.InvariantViolated rejection ->
            match rejection with
            | BloggerRequestRejection.CoverageDidNotAdvance(previous, next) ->
                $"coverage did not advance: previous {previous} next {next}"
            | BloggerRequestRejection.DeltaDigestMismatch(expected, actual) ->
                $"delta digest mismatch: expected {expected} actual {actual}"
            | BloggerRequestRejection.EpochNotCoherent(field, value) ->
                $"epoch not coherent: {field} value {value}"
            | BloggerRequestRejection.EmptySquashCoverage -> "squash covers no frames"
            | BloggerRequestRejection.SquashCoverageMismatch(declared, carried) ->
                $"squash coverage mismatch: declared {declared} carried {carried}"

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

    /// Decode unverified durable data, validate the §4.2 invariants, then
    /// construct through the same validating constructor as live derivation
    /// (W6). Every rejection is typed; nothing falls back to None or a zero
    /// default. Squash shape follows the trusted durable coverage; Main
    /// fields come from the blob with durable defaults only where the blob
    /// is silent.
    let private decodeSquashContext
        (openReq: OpenBloggerRequest)
        (raw: obj)
        : Result<BloggerRequestContext, CycleContextReloadRejection> =
        let covered =
            asInt raw "covered_frame_count"
            |> Option.defaultValue (List.length openReq.SelectedFrameDigests)

        let candidate: BloggerSquashRequestInput =
            { RequestId = openReq.RequestId
              MainSessionId = openReq.MainSessionId
              BloggerSessionId = openReq.BloggerSessionId
              FrameEpochId = openReq.FrameEpochId
              CoveredFrameCount = covered
              FrameDigests = openReq.SelectedFrameDigests
              ObservedPrefixEpochId = openReq.ObservedPrefixEpochId }

        BloggerRequestMaterial.createSquash candidate
        |> Result.map BloggerRequestContext.Squash
        |> Result.mapError CycleContextReloadRejection.InvariantViolated

    let private decodeMainContext
        (openReq: OpenBloggerRequest)
        (raw: obj)
        : Result<BloggerRequestContext, CycleContextReloadRejection> =
        let toml = asString raw "toml"
        let deltaDigestRaw = asString raw "delta_digest"
        let deltaDigest = resolveDeltaDigest openReq toml deltaDigestRaw

        match BloggerDeltaItemWire.tryListOfJs raw?items with
        | Error reason -> Error(CycleContextReloadRejection.ItemsUndecodable reason)
        | Ok typedItems ->
            let prevIngest =
                asInt64 raw "prev_ingest"
                |> Option.defaultValue openReq.PreviousIngestedThroughSequence

            let nextIngest =
                asInt64 raw "next_ingest"
                |> Option.defaultValue openReq.NextIngestedThroughSequence

            let candidate: BloggerMainRequestInput =
                { RequestId = openReq.RequestId
                  MainSessionId = openReq.MainSessionId
                  BloggerSessionId = openReq.BloggerSessionId
                  Items = typedItems
                  Toml = toml
                  PreviousIngestedThroughSequence = prevIngest
                  NextIngestedThroughSequence = nextIngest
                  PreviousCoverableTurnCutoffExclusive =
                    asInt raw "prev_cutoff"
                    |> Option.defaultValue 0
                  NextCoverableTurnCutoffExclusive = asInt raw "next_cutoff" |> Option.defaultValue 0
                  NextCoveredPrefixDigest = asString raw "next_prefix_digest"
                  FrameEpochId = openReq.FrameEpochId
                  DeltaDigest = deltaDigest
                  ObservedPrefixEpochId = openReq.ObservedPrefixEpochId }

            BloggerRequestMaterial.createMain candidate
            |> Result.map BloggerRequestContext.Main
            |> Result.mapError CycleContextReloadRejection.InvariantViolated

    let private decodeParsedContext
        (openReq: OpenBloggerRequest)
        (raw: obj)
        : Result<BloggerRequestContext, CycleContextReloadRejection> =
        match OpenBloggerRequest.providerRequestKind openReq with
        | Ok ProviderRequestKind.BloggerSquash -> decodeSquashContext openReq raw
        | Ok ProviderRequestKind.BloggerMain -> decodeMainContext openReq raw
        | Ok _ -> Error(CycleContextReloadRejection.UnsupportedRequestKind openReq.RequestKind)
        | Error _ -> Error(CycleContextReloadRejection.UnsupportedRequestKind openReq.RequestKind)

    let private decodeRequestContextJson
        (openReq: OpenBloggerRequest)
        (json: string)
        : Result<BloggerRequestContext, CycleContextReloadRejection> =
        try
            decodeParsedContext openReq (Fable.Core.JS.JSON.parse json)
        with ex ->
            Error(CycleContextReloadRejection.BlobCorrupt ex.Message)

    /// C5: inverse of BloggerCoordinator.materializeRequest blob.
    /// Decodes unverified durable data and validates the same invariants as
    /// live construction; a corrupt, version-incompatible or unreadable blob
    /// becomes its own typed rejection — never None or a zero default.
    let tryReloadRequestContextDetailed
        (journal: AgentJournal)
        (openReq: OpenBloggerRequest)
        : Task<Result<BloggerRequestContext, CycleContextReloadRejection>> =
        task {
            match! journal.Writer.BlobWriter.Read openReq.ContextRef with
            | Error reason -> return Error(CycleContextReloadRejection.BlobUnreadable reason)
            | Ok json -> return decodeRequestContextJson openReq json
        }

    /// C5: inverse of BloggerCoordinator.materializeRequest blob.
    /// Full typed context — never leave cutoff/digest at zero defaults.
    /// Rebuild/empty-calls shape: a typed rejection fails closed to None so
    /// the caller keeps its rawMessages fallback (Continuation.fs reads this
    /// contract; the detailed rejection is available from
    /// `tryReloadRequestContextDetailed`).
    let tryReloadRequestContext
        (journal: AgentJournal)
        (openReq: OpenBloggerRequest)
        : Task<BloggerRequestContext option> =
        task {
            match! tryReloadRequestContextDetailed journal openReq with
            | Ok ctx -> return Some ctx
            | Error _ -> return None
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
