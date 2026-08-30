namespace Wanxiangshu.Context.Prefix

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Participant.Provider.Projection

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
        | NoCandidateReason.CoverageNotAheadOfRequest -> "CoverageNotAheadOfRequest"
        | NoCandidateReason.WouldRetreat _ -> "WouldRetreat"
        | NoCandidateReason.NotNewerThanCommitted -> "NotNewerThanCommitted"
        | NoCandidateReason.CutoffProofFailed _ -> "CutoffProofFailed"

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

        PrefixProbeSelection.select
            (shaOf value?sha256)
            (SessionId.create (text value?session))
            (PrefixEpochId.create (int64Value value?committedEpoch))
            committed
            (intValue value?coverableCutoff)
            (text value?coveredDigest)
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

    let private intentToJs (intent: ProjectionIntent) : obj =
        match intent with
        | ProjectionIntent.KeepPhysicalPrefix ->
            box
                {| replacesPrefix = false
                   dropLeading = 0
                   memoryId = null
                   memoryText = null |}
        | ProjectionIntent.ActivatePrefixEpoch value ->
            box
                {| replacesPrefix = true
                   dropLeading = value.CutoffExclusive
                   memoryId = value.SyntheticMessageId
                   memoryText = value.Memory |}
        | _ ->
            box
                {| replacesPrefix = false
                   dropLeading = 0
                   memoryId = null
                   memoryText = null |}

    let forSnapshot (snapshot: obj) (memoryBody: string) : obj =
        let value =
            if isNullish snapshot then
                None
            else
                Some(snapshotOfJs snapshot)

        XPrefixProjection.forSnapshot value CompanionPrompt.MemoryPreamble memoryBody
        |> intentToJs

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

    let forChoice (choice: obj) (committed: obj) (memoryBody: string) : obj =
        let value =
            if isNullish committed then
                None
            else
                Some(snapshotOfJs committed)

        XPrefixProjection.forChoice (choiceOfJs choice) value CompanionPrompt.MemoryPreamble memoryBody
        |> intentToJs

    let requiredBlob (choice: obj) (committed: obj) : obj =
        let value =
            if isNullish committed then
                None
            else
                Some(snapshotOfJs committed)

        XPrefixProjection.requiredBlob (choiceOfJs choice) value
        |> Option.map BlobRef.value
        |> optionObj

    let retainTodoWriteRounds (messages: obj array) : bool array =
        messages
        |> Array.toList
        |> List.map (fun message ->
            let containsTodoWrite =
                not (isNullish message?containsTodoWrite)
                && unbox<bool> message?containsTodoWrite

            let callIds =
                if isNullish message?callIds then
                    Set.empty
                else
                    message?callIds
                    |> unbox<string array>
                    |> Array.map ToolCallId.create
                    |> Set.ofArray

            let facts: XPrefixProjection.RawPrefixMessageFacts =
                { ContainsTodoWrite = containsTodoWrite
                  ToolCallIds = callIds }

            facts)
        |> XPrefixProjection.retainTodoWriteRounds
        |> List.toArray
