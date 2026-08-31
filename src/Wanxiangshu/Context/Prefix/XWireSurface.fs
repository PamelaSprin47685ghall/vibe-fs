namespace Wanxiangshu.Context.Prefix

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Context.Companion
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Participant.Provider.Attempt

/// HOST-BOUNDARY-020/021: X-wire transform decision surface.
///
/// The production `XWire.applyTransform` is async and coupled to `AgentJournal`,
/// `PluginRuntimeScope`, and `ISessionSnapshotPort` — it orchestrates blob reads,
/// session-snapshot awaits, and in-place message replacement. The *decisions* it
/// makes are pure: `RecoverySlot.mayRecover`, `PrefixProbeSelection.select`, and
/// `XPrefixProjection.forChoice` / `XPrefixProjection.render`. This surface exposes that decision pipeline
/// as a single JS-callable function with JS-native input/output, so the
/// fail-closed and no-op laws can be proven without a live runtime.
///
/// The surface IS the production algorithm: every branch delegates to the same
/// pure functions `applyTransform` calls. No support-model copy.
[<RequireQualifiedAccess>]
module XWireSurface =

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    [<Emit("Boolean($0)")>]
    let private isTruthy (value: obj) : bool = jsNative

    let private text (value: obj) : string =
        if isNullish value then "" else string value

    let private intValue (value: obj) : int = int (text value)

    // ── JS → Domain decoders (same shapes PrefixSurface uses) ───────────────

    let private snapshotOfJs (value: obj) : PrefixSnapshot =
        { FrozenRecordPrefixRef = BlobRef.create (text value?ref)
          FrozenRecordPrefixDigest = BlobDigest.create (text value?frozenDigest)
          CutoffExclusive = intValue value?cutoff
          CoveredPrefixDigest = text value?prefixDigest
          SealRoot = text value?sealRoot
          SyntheticMessageId = text value?syntheticId }

    let private snapshotOptionOfJs (value: obj) : PrefixSnapshot option =
        if isNullish value then None else Some(snapshotOfJs value)

    let private semanticMessageOfJs (msg: obj) : SemanticMessage =
        let role = text msg?role

        let parts =
            if isNullish msg?parts then
                []
            else
                msg?parts
                |> unbox<obj array>
                |> Array.toList
                |> List.choose (fun part ->
                    let kind = text part?kind

                    match kind with
                    | "text" -> Some(SemanticText(text part?text))
                    | "reasoning" -> Some(SemanticReasoning(text part?text))
                    | "tool-call" -> Some(SemanticToolCall(text part?name, text part?args))
                    | "tool-result" -> Some(SemanticToolResult(text part?result))
                    | "media" ->
                        let mt =
                            if isNullish part?mediaType then
                                None
                            else
                                Some(text part?mediaType)

                        Some(SemanticMedia(mt, text part?contentDigest))
                    | _ -> None)

        { Role = role; Parts = parts }

    let private semanticProjectionOfJs (value: obj) : ProviderSemanticProjection =
        let messages =
            if isNullish value?messages then
                []
            else
                value?messages
                |> unbox<obj array>
                |> Array.toList
                |> List.map semanticMessageOfJs

        { ProviderId = None
          ModelId = None
          Variant = None
          Tools = []
          System = []
          Messages = messages }

    // ── Domain → JS encoders ────────────────────────────────────────────────

    let private semanticPartToJs (part: SemanticPart) : obj =
        match part with
        | SemanticText t -> box {| kind = "text"; text = t |}
        | SemanticReasoning t -> box {| kind = "reasoning"; text = t |}
        | SemanticToolCall(name, args) ->
            box
                {| kind = "tool-call"
                   name = name
                   args = args |}
        | SemanticToolResult result ->
            box
                {| kind = "tool-result"
                   result = result |}
        | SemanticMedia(mediaType, digest) ->
            box
                {| kind = "media"
                   mediaType = mediaType
                   contentDigest = digest |}

    let private semanticMessageToJs (msg: SemanticMessage) : obj =
        box
            {| role = msg.Role
               parts = msg.Parts |> List.map semanticPartToJs |> List.toArray |}

    let private noCandidateReasonLabel (reason: NoCandidateReason) : string =
        match reason with
        | NoCandidateReason.NoCoverage -> "NoCoverage"
        | NoCandidateReason.CoverageNotAheadOfRequest -> "CoverageNotAheadOfRequest"
        | NoCandidateReason.WouldRetreat _ -> "WouldRetreat"
        | NoCandidateReason.NotNewerThanCommitted -> "NotNewerThanCommitted"
        | NoCandidateReason.CutoffProofFailed(expected, recomputed) -> $"CutoffProofFailed:{expected}:{recomputed}"

    let private probeToJs (probe: PrefixProbe) : obj =
        let c = probe.Candidate

        box
            {| probeId = probe.ProbeId
               basedOnEpoch = PrefixEpochId.value probe.BasedOnEpochId
               candidate =
                box
                    {| ref = BlobRef.value c.FrozenRecordPrefixRef
                       frozenDigest = BlobDigest.value c.FrozenRecordPrefixDigest
                       cutoff = c.CutoffExclusive
                       prefixDigest = c.CoveredPrefixDigest
                       sealRoot = c.SealRoot
                       syntheticId = c.SyntheticMessageId |} |}

    // ── sha256: exact production owner used by XWire.applyTransform ─────────

    let private sha256Hex (value: string) : string = HostDigest.sha256Hex value

    let private attemptOutcomeOfJs (value: obj) : AttemptOutcome option =
        if isNullish value then
            None
        else
            match text value with
            | "completed"
            | "tool-calls" -> Some AttemptOutcome.Completed
            | "completed-invalid" -> Some AttemptOutcome.CompletedInvalid
            | "failed" -> Some AttemptOutcome.Failed
            | "aborted" -> Some AttemptOutcome.Aborted
            | _ -> None

    // ── The transform decision (delegates to XWire's pure decisions) ───────
    //
    // Every branch delegates to the same pure production functions that
    // `applyTransform` calls. The surface extracts the async Host I/O
    // boundaries (journal blob reads, session-snapshot awaits, in-place
    // message replacement) and exposes the *decision* with JS-native I/O.

    /// Return the canonical digest for a provider-visible prefix cutoff. This is
    /// the same proof input used by `transform` when validating Companion coverage.
    let coveredPrefixDigest (projection: obj) (cutoff: int) : string =
        let typed = semanticProjectionOfJs projection

        let snapshot = { CurrentProjection = typed }

        ProjectionRenderer.cutoffDigest HostDigest.sha256Hex snapshot cutoff

    let presentationHorizon (hasProbe: bool) : string =
        match XWire.presentationHorizonForProbe hasProbe with
        | PrefixPresentationHorizon.Current -> "Current"
        | PrefixPresentationHorizon.TentativeCold -> "TentativeCold"

    let retiredRetryMessageIds (horizon: string) (rawMessages: obj array) : string array =
        let typedHorizon =
            match horizon with
            | "TentativeCold" -> PrefixPresentationHorizon.TentativeCold
            | _ -> PrefixPresentationHorizon.Current

        XWire.retryTransportRetirement typedHorizon (Array.toList rawMessages)
        |> Set.toArray

    let replacePrefixByHostIds
        (rawMessages: obj array)
        (coveredHostMessageIds: string array)
        (openingHostMessageId: obj)
        (syntheticMessageId: string)
        (memory: string)
        : obj array =
        XWire.replacePrefixByHostIds
            (if isNull rawMessages then [] else Array.toList rawMessages)
            (if isNull coveredHostMessageIds then
                 []
             else
                 Array.toList coveredHostMessageIds)
            (if isNullish openingHostMessageId then
                 None
             else
                 Some(text openingHostMessageId))
            syntheticMessageId
            memory
        |> List.toArray

    let suppressHostMessagesByIds (rawMessages: obj array) (hostMessageIds: string array) : obj array =
        XWire.suppressHostMessagesByIds
            (if isNull rawMessages then [] else Array.toList rawMessages)
            (if isNull hostMessageIds then
                 Set.empty
             else
                 Set.ofArray hostMessageIds)
        |> List.toArray

    /// HOST-BOUNDARY-020/021: the X-wire transform decision.
    ///
    /// Input fields (JS object):
    ///   journal:       truthy = a durable journal is available.
    ///   sessionId:     the managed session id found in the transform output.
    ///   armed:         true when the recovery slot is armed.
    ///   prefixEpoch:   current durable prefix epoch (the probe's base epoch).
    ///   offset:        fallback cursor offset (0-3). Recovery slots are 1 and 3.
    ///   physicalUser:  the physical user message id (required when armed).
    ///   snapshotPort:  truthy = the public session snapshot port is available.
    ///   currentProjection: X provider-visible semantic projection (messages array).
    ///   committedSnapshot: committed prefix snapshot (or null).
    ///   coverableCutoff:  Companion's coverable turn cutoff.
    ///   coveredDigest:    Companion's covered prefix digest.
    ///   requestStartCutoff: turns preceding this request's physical user message.
    ///   frozenRecordPrefixRef: blob ref for the frozen record prefix.
    ///   frozenRecordPrefixDigest: blob digest for the frozen record prefix.
    ///   frozenRecordPrefixBody: already-materialized frozen record prefix body.
    ///   memoryPreamble: localized companion memory preamble.
    ///   outcome:        "completed" | "failed" | "aborted" | "in-progress" | null.
    ///
    /// Output fields (JS object):
    ///   ok, noop, changed, consumed, promoted, probe, noProbeReason, error, output.
    ///   `promoted` is plan-time promotability at the observed prefix epoch;
    ///   durable promotion is committed only by `reconcile`, which rechecks
    ///   the probe epoch.
    let transform (input: obj) : obj =
        // ── HOST-BOUNDARY-021: no journal → no-op ──
        let journal = isTruthy input?journal

        if not journal then
            box
                {| ok = true
                   noop = true
                   changed = false
                   consumed = false
                   promoted = false
                   probe = null
                   noProbeReason = null
                   error = null
                   output = input?currentProjection |}
        else
            // ── HOST-BOUNDARY-021: no session id → no-op ──
            let sessionId = text input?sessionId

            if String.IsNullOrEmpty sessionId then
                box
                    {| ok = true
                       noop = true
                       changed = false
                       consumed = false
                       promoted = false
                       probe = null
                       noProbeReason = null
                       error = null
                       output = input?currentProjection |}
            else
                // ── PAR-011: only the exact Host-accepted physical retry owns arming ──
                let armed = not (isNullish input?armed) && (input?armed |> unbox<bool>)
                let physicalUser = text input?physicalUser
                let armedPhysicalUser = text input?armedPhysicalUser

                if
                    not armed
                    || String.IsNullOrEmpty physicalUser
                    || not (String.Equals(physicalUser, armedPhysicalUser, StringComparison.Ordinal))
                then
                    box
                        {| ok = true
                           noop = true
                           changed = false
                           consumed = false
                           promoted = false
                           probe = null
                           noProbeReason = null
                           error = null
                           output = input?currentProjection |}
                else
                    // The transform is pre-inference: there is intentionally no
                    // current assistant run/public-snapshot dependency here.
                    let prefixEpoch =
                        if isNullish input?prefixEpoch then
                            None
                        else
                            match Int64.TryParse(text input?prefixEpoch) with
                            | true, value -> Some(PrefixEpochId.create value)
                            | false, _ -> None

                    let prefixEpochAvailable = Option.isSome prefixEpoch
                    let frozenBodyAvailable = not (isNullish input?frozenRecordPrefixBody)

                    if not prefixEpochAvailable || not frozenBodyAvailable then
                        let error =
                            if not prefixEpochAvailable then
                                "X-wire cannot plan a retry without the current prefix epoch"
                            else
                                "X-wire cannot plan a retry without the frozen record prefix body"

                        box
                            {| ok = false
                               noop = false
                               changed = false
                               consumed = false
                               promoted = false
                               probe = null
                               noProbeReason = null
                               error = error
                               output = null |}
                    else
                        // ── Recovery decision: failure-local slot opportunity (CTX-006) ──
                        let offsetByte =
                            if isNullish input?offset then
                                0uy
                            else
                                byte (intValue input?offset)

                        let offset =
                            match AgentPairCursor.FallbackOffsetCodec.ofByte offsetByte with
                            | Ok o -> o
                            | Error _ -> AgentPairCursor.FallbackOffset.Fork0

                        let coverableCutoff = intValue input?coverableCutoff
                        let opportunity = RecoverySlot.opportunity SlotArming.ArmedByAdvance offset

                        // ── Probe selection (CTX-011) ──
                        let committedSnapshot = snapshotOptionOfJs input?committedSnapshot
                        let committedEpoch = prefixEpoch |> Option.defaultValue PrefixEpochId.initial

                        let coveredDigest = text input?coveredDigest
                        let requestStartCutoff = intValue input?requestStartCutoff
                        let frozenRef = BlobRef.create (text input?frozenRecordPrefixRef)
                        let frozenDigest = BlobDigest.create (text input?frozenRecordPrefixDigest)

                        let currentProjection = semanticProjectionOfJs input?currentProjection

                        let projectionSnapshot = { CurrentProjection = currentProjection }

                        let recomputeDigest =
                            ProjectionRenderer.cutoffDigest HostDigest.sha256Hex projectionSnapshot

                        let candidateResult =
                            PrefixProbeSelection.select
                                sha256Hex
                                (SessionId.create sessionId)
                                committedEpoch
                                committedSnapshot
                                coverableCutoff
                                coveredDigest
                                requestStartCutoff
                                frozenRef
                                frozenDigest
                                recomputeDigest

                        let probeResult = XWire.selectProbe opportunity candidateResult

                        // ── Prefix intent (CTX-010) ──
                        let choice, noProbeReason =
                            match probeResult with
                            | Ok probe -> XProjectionChoice.UsePrefixProbe probe, None
                            | Error reason -> XProjectionChoice.UseCommittedEpoch, Some reason

                        let frozenBody = text input?frozenRecordPrefixBody
                        let memoryPreamble = text input?memoryPreamble

                        let prefixIntent =
                            XPrefixProjection.forChoice choice committedSnapshot memoryPreamble frozenBody

                        // ── Prefix-owner render decision ──
                        let rendered = XPrefixProjection.render prefixIntent

                        let changed =
                            match rendered with
                            | PrefixRendered.Synthetic _ -> true
                            | PrefixRendered.Physical -> false

                        // The typed permit was consumed before this attempt was built.
                        // No probe result can leak arming into a later physical request.
                        let consumed = true

                        // ── Reconcile: promotableProbe (CTX-012) ──
                        // AttemptPlanner.promotableProbe reads only
                        // `plan.Profile.ProjectionChoice` and the outcome.
                        // We have the choice and probe result directly,
                        // so the promote decision is: Completed + has probe.
                        let reconcileDecision =
                            XWire.reconciliationDecision
                                true
                                (attemptOutcomeOfJs input?outcome)
                                (Result.isOk probeResult)
                                true

                        let promoted = reconcileDecision.Promoted

                        let probeJs =
                            match probeResult with
                            | Ok probe -> probeToJs probe
                            | Error _ -> null

                        let noProbeJs =
                            noProbeReason |> Option.map noCandidateReasonLabel |> Option.defaultValue null

                        // ── Output: the transformed projection ──
                        let output =
                            if changed then
                                match rendered with
                                | PrefixRendered.Synthetic activation ->
                                    let head: SemanticMessage =
                                        { Role = "user"
                                          Parts = [ SemanticText activation.Memory ] }

                                    let tail = currentProjection.Messages |> List.skip activation.CutoffExclusive

                                    let transformed =
                                        { currentProjection with
                                            Messages = head :: tail }

                                    box
                                        {| messages =
                                            transformed.Messages |> List.map semanticMessageToJs |> List.toArray |}
                                | _ -> input?currentProjection
                            else
                                input?currentProjection

                        box
                            {| ok = true
                               noop = false
                               changed = changed
                               consumed = consumed
                               promoted = promoted
                               probe = probeJs
                               noProbeReason = noProbeJs
                               error = null
                               output = output |}

    /// HOST-BOUNDARY-021: reconcile decision — does a completed attempt promote
    /// a prefix rebase, and does a failed/aborted attempt clear the plan?
    ///
    /// Delegates to the pure decision used by `XWire.reconcileAttempt`. The
    /// production function is async and writes a durable fact; this surface
    /// exposes that shared decision.
    ///
    /// Input fields (JS object):
    ///   hasPlan:    true when an attempt plan exists for this (session, run).
    ///   outcome:    "completed" | "failed" | "aborted" | "in-progress" | "unknown".
    ///   hasProbe:   true when the plan carries a prefix probe.
    ///   currentEpoch: current durable prefix epoch.
    ///   probeEpoch: probe's durable base epoch.
    ///
    /// Output fields (JS object):
    ///   promoted:  true when a PrefixRebaseCommitted fact would be written.
    ///   cleared:   true when the plan is cleared (terminal failure/abandon).
    ///   keptPlan:  true when the plan is kept across a non-terminal reread.
    let reconcile (input: obj) : obj =
        let hasPlan = not (isNullish input?hasPlan) && (input?hasPlan |> unbox<bool>)
        let hasProbe = not (isNullish input?hasProbe) && (input?hasProbe |> unbox<bool>)

        let currentEpoch =
            if isNullish input?currentEpoch then
                None
            else
                Some(PrefixEpochId.create (int64 (text input?currentEpoch)))

        let probeEpoch =
            if isNullish input?probeEpoch then
                None
            else
                Some(PrefixEpochId.create (int64 (text input?probeEpoch)))

        let epochMatches =
            match currentEpoch, probeEpoch with
            | Some current, Some probe -> current = probe
            | _ -> false

        let decision =
            XWire.reconciliationDecision hasPlan (attemptOutcomeOfJs input?outcome) hasProbe epochMatches

        box
            {| promoted = decision.Promoted
               cleared = decision.Cleared
               keptPlan = decision.KeptPlan |}
