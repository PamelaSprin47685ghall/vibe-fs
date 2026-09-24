namespace Wanxiangshu.Context.Prefix

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Participant.Provider.Attempt

/// Prefix-stability owner surface. Prefix epoch state, rebase and reanchor
/// facts cross as JSON; the production epoch and identity representations stay
/// behind this boundary.
[<RequireQualifiedAccess>]
module PrefixSurface =

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private text (value: obj) : string =
        if isNullish value then "" else string value

    let private int64Value (value: obj) : int64 = int64 (text value)
    let private intValue (value: obj) : int = int (text value)

    let private shaOf (value: obj) : string -> string =
        if isNullish value then
            fun input -> "«" + input + "»"
        else
            unbox<string -> string> value

    let private snapshotOfJs (value: obj) : PrefixSnapshot =
        { FrozenRecordPrefixRef = BlobRef.create (text value?ref)
          FrozenRecordPrefixDigest = BlobDigest.create (text value?frozenDigest)
          CutoffExclusive = intValue value?cutoff
          CoveredPrefixDigest = text value?prefixDigest
          SealRoot = text value?sealRoot
          SyntheticMessageId = text value?syntheticId }

    let private snapshotToJs (snapshot: PrefixSnapshot) : obj =
        box
            {| ref = BlobRef.value snapshot.FrozenRecordPrefixRef
               frozenDigest = BlobDigest.value snapshot.FrozenRecordPrefixDigest
               cutoff = snapshot.CutoffExclusive
               prefixDigest = snapshot.CoveredPrefixDigest
               sealRoot = snapshot.SealRoot
               syntheticId = snapshot.SyntheticMessageId |}

    let private optionObj (value: 'a option) : obj =
        match value with
        | None -> null
        | Some item -> box item

    let private runsOfJs (value: obj) : Set<ProviderRunIdentity> =
        if isNullish value then
            Set.empty
        else
            value
            |> unbox<obj array>
            |> Array.map (fun run -> ProviderRunIdentity.create (text run))
            |> Set.ofArray

    let private stateOfJs (value: obj) : ActivePrefixEpoch =
        { EpochId = PrefixEpochId.create (int64Value value?epoch)
          Snapshot =
            if isNullish value?snapshot then
                None
            else
                Some(snapshotOfJs value?snapshot)
          ReanchoredRuns = runsOfJs value?reanchoredRuns }

    let private stateToJs (state: ActivePrefixEpoch) : obj =
        box
            {| epoch = PrefixEpochId.value state.EpochId
               snapshot = state.Snapshot |> Option.map snapshotToJs |> optionObj
               reanchoredRuns = state.ReanchoredRuns |> Set.toArray |> Array.map ProviderRunIdentity.value |}

    let private rejectionName (rejection: PrefixFoldRejection) : string =
        match rejection with
        | PrefixFoldRejection.StalePrefixEpoch _ -> "StalePrefixEpoch"
        | PrefixFoldRejection.NonSequentialPrefixEpoch -> "NonSequentialPrefixEpoch"
        | PrefixFoldRejection.CutoffRetreated _ -> "CutoffRetreated"
        | PrefixFoldRejection.CandidateNotNew -> "CandidateNotNew"
        | PrefixFoldRejection.CompactionAlreadyReanchored _ -> "CompactionAlreadyReanchored"

    let private resultToJs (result: Result<ActivePrefixEpoch, PrefixFoldRejection>) : obj =
        match result with
        | Ok value -> box {| ok = true; value = stateToJs value |}
        | Error rejection ->
            box
                {| ok = false
                   error = rejectionName rejection |}

    let empty: obj =
        box
            {| epoch = 0L
               snapshot = null
               reanchoredRuns = [||] |}

    let snapshot (value: obj) : obj = snapshotOfJs value |> snapshotToJs

    let private requestKindOf (value: obj) : ProviderRequestKind option =
        match text value |> fun item -> item.ToLowerInvariant() with
        | "workmain"
        | "work-main" -> Some ProviderRequestKind.WorkMain
        | "bloggermain"
        | "blogger-main" -> Some ProviderRequestKind.BloggerMain
        | "bloggersquash"
        | "blogger-squash" -> Some ProviderRequestKind.BloggerSquash
        | "interactionrepair"
        | "interaction-repair" -> Some ProviderRequestKind.InteractionRepair
        | "strengthreplica"
        | "strength-replica" -> Some ProviderRequestKind.StrengthReplica
        | _ -> None

    let requestKindLabels: string array =
        [| ProviderRequestKind.WorkMain
           ProviderRequestKind.BloggerMain
           ProviderRequestKind.BloggerSquash
           ProviderRequestKind.InteractionRepair
           ProviderRequestKind.StrengthReplica |]
        |> Array.map ProviderRequestKind.label

    let requestKindMayCarryProbe (kind: string) : bool =
        requestKindOf kind
        |> Option.map ProviderRequestKind.mayCarryProbe
        |> Option.defaultValue false

    let requestKindLabel (kind: string) : string =
        requestKindOf kind
        |> Option.map ProviderRequestKind.label
        |> Option.defaultValue ""

    let requestKind =
        box
            {| workMain = "work-main"
               bloggerMain = "blogger-main"
               bloggerSquash = "blogger-squash"
               interactionRepair = "interaction-repair"
               strengthReplica = "strength-replica"
               all = requestKindLabels
               mayCarryProbe = (fun kind -> requestKindMayCarryProbe kind)
               label = (fun kind -> requestKindLabel kind) |}

    let private noCandidateName (reason: NoCandidateReason) : string =
        match reason with
        | NoCandidateReason.NoCoverage -> "NoCoverage"
        | NoCandidateReason.WouldRetreat _ -> "WouldRetreat"
        | NoCandidateReason.NotNewerThanCommitted -> "NotNewerThanCommitted"
        | NoCandidateReason.CutoffProofFailed _ -> "CutoffProofFailed"
        | NoCandidateReason.BeyondPhaseBoundary _ -> "BeyondPhaseBoundary"
        | NoCandidateReason.MaterialBeyondBoundary _ -> "MaterialBeyondBoundary"

    let private selectionToJs (result: Result<PrefixProbe, NoCandidateReason>) : obj =
        match result with
        | Ok probe ->
            let candidate = probe.Candidate

            box
                {| ok = true
                   probeId = probe.ProbeId
                   basedOnEpoch = PrefixEpochId.value probe.BasedOnEpochId
                   candidate = snapshotToJs candidate
                   cutoff = candidate.CutoffExclusive
                   sealRoot = candidate.SealRoot
                   syntheticId = candidate.SyntheticMessageId |}
        | Error reason ->
            box
                {| ok = false
                   error = noCandidateName reason
                   message = PrefixProbeSelection.describeNoCandidate reason |}

    /// CTX-011 owner bridge: decode one selection request and encode the typed
    /// candidate or refusal returned by the pure selector.
    let select (value: obj) : obj =
        let committed =
            if isNullish value?committedSnapshot then
                None
            else
                Some(snapshotOfJs value?committedSnapshot)

        let recompute =
            if isNullish value?recomputeDigest then
                fun (_: int) -> ""
            else
                unbox<int -> string> value?recomputeDigest

        let window =
            if isNullish value?phaseBoundary then
                // Failure recovery: the proven coverage is the only bound (CTX-029).
                ProbeBound.CoverageOnly
            else
                ProbeBound.PhaseBoundary(intValue value?phaseBoundary)

        PrefixProbeSelection.select
            (shaOf value?sha256)
            (SessionId.create (text value?session))
            (PrefixEpochId.create (int64Value value?committedEpoch))
            committed
            window
            (intValue value?coverableCutoff)
            (text value?coveredDigest)
            (intValue value?materialCutoff)
            (intValue value?requestStartCutoff)
            (BlobRef.create (
                if isNullish value?frozenRef then
                    "blob-frozen-" + string (intValue value?coverableCutoff)
                else
                    text value?frozenRef
            ))
            (BlobDigest.create (text value?frozenDigest))
            recompute
        |> selectionToJs

    let applyRebase (request: obj) (state: obj) : obj =
        PrefixEpochProjection.applyRebase
            (PrefixEpochId.create (int64Value request?previousEpoch))
            (PrefixEpochId.create (int64Value request?nextEpoch))
            (snapshotOfJs request?candidate)
            (stateOfJs state)
        |> resultToJs

    let applyReanchor (request: obj) (state: obj) : obj =
        PrefixEpochProjection.applyReanchor
            (PrefixEpochId.create (int64Value request?previousEpoch))
            (PrefixEpochId.create (int64Value request?nextEpoch))
            (ProviderRunIdentity.create (text request?observedRun))
            (stateOfJs state)
        |> resultToJs

    let epochOf (state: obj) : int64 =
        PrefixEpochId.value (stateOfJs state).EpochId

    let hasSnapshot (state: obj) : bool =
        PrefixEpochProjection.hasSnapshot (stateOfJs state)

    let reanchoredRuns (state: obj) : string array =
        (stateOfJs state).ReanchoredRuns
        |> Set.toArray
        |> Array.map ProviderRunIdentity.value

    let isReanchored (run: string) (state: obj) : bool =
        PrefixEpochProjection.isReanchored (ProviderRunIdentity.create (text run)) (stateOfJs state)

    let private renderedToJs (rendered: PrefixRendered) : obj =
        match rendered with
        | PrefixRendered.Physical ->
            box
                {| replacesPrefix = false
                   dropLeading = 0
                   memoryId = null
                   memoryText = null |}
        | PrefixRendered.Synthetic value ->
            box
                {| replacesPrefix = true
                   dropLeading = value.CutoffExclusive
                   memoryId = value.SyntheticMessageId
                   memoryText = value.Memory |}

    let forSnapshot (snapshot: obj) (memoryPreamble: string) (memoryBody: string) : obj =
        let value =
            if isNullish snapshot then
                None
            else
                Some(snapshotOfJs snapshot)

        XPrefixProjection.forSnapshot value memoryPreamble memoryBody
        |> XPrefixProjection.render
        |> renderedToJs

    let private choiceOfJs (choice: obj) : XProjectionChoice =
        if text choice?kind = "probe" then
            let probeId =
                let id = text choice?probeId

                if System.String.IsNullOrWhiteSpace id then
                    "probe-1"
                else
                    id

            XProjectionChoice.UsePrefixProbe
                { ProbeId = probeId
                  BasedOnEpochId = PrefixEpochId.initial
                  Candidate = snapshotOfJs choice?candidate }
        else
            XProjectionChoice.UseCommittedEpoch

    let forChoice (choice: obj) (committed: obj) (memoryPreamble: string) (memoryBody: string) : obj =
        let value =
            if isNullish committed then
                None
            else
                Some(snapshotOfJs committed)

        XPrefixProjection.forChoice (choiceOfJs choice) value memoryPreamble memoryBody
        |> XPrefixProjection.render
        |> renderedToJs

    let requiredBlob (choice: obj) (committed: obj) : obj =
        let value =
            if isNullish committed then
                None
            else
                Some(snapshotOfJs committed)

        XPrefixProjection.requiredBlob (choiceOfJs choice) value
        |> Option.map BlobRef.value
        |> optionObj

    /// Projection of the caller's own retention decision.
    ///
    /// The unbounded `todowrite` exemption is gone (context-compression-020
    /// revised): the caller keeps the real Opening and the last K phases, and this
    /// surface only carries that verdict across the JS boundary. It must not grow a
    /// second rule of its own, or the two would drift and one would silently win.
    let retainedPrefixMessages (messages: obj array) : bool array =
        messages
        |> Array.toList
        |> List.map (fun message ->
            let facts: XPrefixProjection.RawPrefixMessageFacts =
                { Retained = not (isNullish message?retained) && unbox<bool> message?retained }

            facts.Retained)
        |> List.toArray

    /// context-compression-028/029: the K-window decision across the JS boundary.
    ///
    /// The surface translates shapes only. It must not clamp, default or re-derive
    /// anything: the clamp against real coverage is the one place the clause's
    /// "desire vs. evidence" split would silently disappear.
    let desiredCutoff (k: int) (phaseTurnStarts: int array) : obj =
        match Wanxiangshu.Context.Prefix.PhaseWindow.desiredCutoff k (Array.toList phaseTurnStarts) with
        | Wanxiangshu.Context.Prefix.PhaseWindowDecision.NoPhases -> box {| kind = "NoPhases" |}
        | Wanxiangshu.Context.Prefix.PhaseWindowDecision.KeepFrom cutoff ->
            box
                {| kind = "KeepFrom"
                   cutoffExclusive = cutoff |}

    /// context-compression-028: the window in force when the owner opens.
    let defaultK: int = Wanxiangshu.Context.Prefix.PhaseWindow.defaultK

    /// One committed phase admitted into the bounded window; identity order is the
    /// commit order and the result is trimmed to `k`.
    let appendPhase (k: int) (callId: string) (window: string array) : string array =
        let committed =
            if isNull window then [] else Array.toList window

        let next =
            Wanxiangshu.Context.Prefix.PhaseWindow.emptyWindow
            |> fun start ->
                committed
                |> List.fold
                    (fun acc item ->
                        Wanxiangshu.Context.Prefix.PhaseWindow.appendPhase
                            k
                            (ToolCallId.create item)
                            acc)
                    start
            |> Wanxiangshu.Context.Prefix.PhaseWindow.appendPhase k (ToolCallId.create callId)

        next.PhaseCallIds |> List.map ToolCallId.value |> List.toArray

    /// The window's desire, with `turnByCallId[i]` the canonical turn of
    /// `window[i]` (null = no addressable turn in the current generation).
    let desiredCutoffOfWindow (window: string array) (turnByCallId: obj array) : obj =
        let turns =
            if isNull turnByCallId then [] else Array.toList turnByCallId

        let committed =
            { Wanxiangshu.Context.Prefix.PhaseWindow.PhaseCallIds =
                (if isNull window then [] else Array.toList window)
                |> List.map ToolCallId.create }

        let turnOf (callId: ToolCallId) =
            Array.tryFindIndex (fun item -> item = ToolCallId.value callId) window
            |> Option.bind (fun position ->
                if position < List.length turns then
                    let value = List.item position turns
                    if isNullish value then None else Some(unbox<int> value)
                else
                    None)

        match Wanxiangshu.Context.Prefix.PhaseWindow.desiredCutoffOf turnOf committed with
        | Wanxiangshu.Context.Prefix.PhaseWindowDecision.NoPhases -> box {| kind = "NoPhases" |}
        | Wanxiangshu.Context.Prefix.PhaseWindowDecision.KeepFrom cutoff ->
            box
                {| kind = "KeepFrom"
                   cutoffExclusive = cutoff |}

    let validateK (k: int) : obj =
        match Wanxiangshu.Context.Prefix.PhaseWindow.validateK k with
        | Ok() -> box {| ok = true; error = null |}
        | Error reason -> box {| ok = false; error = reason |}
