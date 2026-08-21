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
/// makes are pure: `RecoverySlot.mayRecover`, `PrefixProbeSelection.select`,
/// `XPrefixProjection.forChoice`, `ProjectionPlanner.plan`,
/// `ProjectionRenderer.renderPrefix`. This surface exposes that decision pipeline
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

    // ── The transform decision (mirrors XWire.applyTransform's logic) ───────
    //
    // Every branch delegates to the same pure production functions that
    // `applyTransform` calls. The surface extracts the async Host I/O
    // boundaries (journal blob reads, session-snapshot awaits, in-place
    // message replacement) and exposes the *decision* with JS-native I/O.

    /// Return the canonical digest for a provider-visible prefix cutoff. This is
    /// the same proof input used by `transform` when validating Companion coverage.
    let coveredPrefixDigest (projection: obj) (cutoff: int) : string =
        let typed = semanticProjectionOfJs projection

        let truncated =
            { typed with
                Messages = typed.Messages |> List.truncate cutoff }

        HostDigest.sha256Hex (ProviderProjection.renderSemantic truncated)

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
                // ── HOST-BOUNDARY-021: unarmed → no-op ──
                let armed = not (isNullish input?armed) && (input?armed |> unbox<bool>)

                if not armed then
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
                    // ── HOST-BOUNDARY-020: armed + missing physical user → fail-closed ──
                    let physicalUser = text input?physicalUser

                    if String.IsNullOrEmpty physicalUser then
                        box
                            {| ok = false
                               noop = false
                               changed = false
                               consumed = false
                               promoted = false
                               probe = null
                               noProbeReason = null
                               error = "X-wire cannot plan a retry without a physical user message"
                               output = null |}
                    else
                        // ── HOST-BOUNDARY-020: armed + missing snapshot port → fail-closed ──
                        let snapshotPort =
                            not (isNullish input?snapshotPort) && (input?snapshotPort |> unbox<bool>)

                        let prefixEpoch =
                            if isNullish input?prefixEpoch then
                                None
                            else
                                match Int64.TryParse(text input?prefixEpoch) with
                                | true, value -> Some(PrefixEpochId.create value)
                                | false, _ -> None

                        let prefixEpochAvailable = Option.isSome prefixEpoch
                        let frozenBodyAvailable = not (isNullish input?frozenRecordPrefixBody)

                        if not snapshotPort || not prefixEpochAvailable || not frozenBodyAvailable then
                            let error =
                                if not snapshotPort then
                                    "X-wire cannot plan a retry without the public session snapshot"
                                elif not prefixEpochAvailable then
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

                            let recomputeDigest (cutoff: int) =
                                let truncated =
                                    { currentProjection with
                                        Messages = currentProjection.Messages |> List.truncate cutoff }

                                HostDigest.sha256Hex (ProviderProjection.renderSemantic truncated)

                            let probeResult =
                                match opportunity with
                                | RecoveryOpportunity.RecoveryAttempt ->
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
                                | RecoveryOpportunity.OrdinaryAttempt -> Error NoCandidateReason.NoCoverage

                            // ── Prefix intent (CTX-010) ──
                            let choice, noProbeReason =
                                match probeResult with
                                | Ok probe -> XProjectionChoice.UsePrefixProbe probe, None
                                | Error reason -> XProjectionChoice.UseCommittedEpoch, Some reason

                            let frozenBody = text input?frozenRecordPrefixBody
                            let memoryPreamble = text input?memoryPreamble

                            let prefixIntent =
                                XPrefixProjection.forChoice choice committedSnapshot memoryPreamble frozenBody

                            // ── Render decision (PROJ-004/006) ──
                            let intents = [ prefixIntent ]

                            match ProjectionPlanner.plan intents with
                            | Error conflict ->
                                box
                                    {| ok = false
                                       noop = false
                                       changed = false
                                       consumed = false
                                       promoted = false
                                       probe = null
                                       noProbeReason = null
                                       error = $"X-wire projection conflict: %A{conflict}"
                                       output = null |}
                            | Ok ordered ->
                                let rendered = ProjectionRenderer.renderPrefix ordered

                                let changed =
                                    match rendered with
                                    | RenderedPrefix.SyntheticPrefix _ -> true
                                    | RenderedPrefix.PhysicalPrefix -> false

                                // The typed permit was consumed before this attempt was built.
                                // No probe result can leak arming into a later physical request.
                                let consumed = true

                                // ── Reconcile: promotableProbe (CTX-012) ──
                                // AttemptPlanner.promotableProbe reads only
                                // `plan.Profile.ProjectionChoice` and the outcome.
                                // We have the choice and probe result directly,
                                // so the promote decision is: Completed + has probe.
                                let outcome =
                                    if isNullish input?outcome then
                                        None
                                    else
                                        Some(text input?outcome)

                                let promoted =
                                    match outcome with
                                    | Some "completed" ->
                                        match probeResult with
                                        | Ok _ -> true
                                        | Error _ -> false
                                    | _ -> false

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
                                        | RenderedPrefix.SyntheticPrefix activation ->
                                            let head: SemanticMessage =
                                                { Role = "user"
                                                  Parts = [ SemanticText activation.Memory ] }

                                            let tail = currentProjection.Messages |> List.skip activation.DropLeading

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
    /// Mirrors `XWire.reconcileAttempt`'s decision logic. The production function
    /// is async and writes a durable fact; this surface exposes the *decision*.
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

        if not hasPlan then
            box
                {| promoted = false
                   cleared = false
                   keptPlan = false |}
        else
            let outcome = text input?outcome
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

            match outcome with
            | "completed"
            | "tool-calls" ->
                // CTX-012: only a probe based on the current epoch can promote.
                box
                    {| promoted = hasProbe && epochMatches
                       cleared = true
                       keptPlan = false |}
            | "failed"
            | "aborted" ->
                // Terminal failure: clear plan, no promotion.
                box
                    {| promoted = false
                       cleared = true
                       keptPlan = false |}
            | _ ->
                // In-progress / unknown: keep the plan (HOST-BOUNDARY-021).
                box
                    {| promoted = false
                       cleared = false
                       keptPlan = true |}
