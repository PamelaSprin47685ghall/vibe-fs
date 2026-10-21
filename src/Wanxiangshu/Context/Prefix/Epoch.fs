namespace Wanxiangshu.Context.Prefix

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation

/// Which prefix generation is in force. The epoch advances only on an explicit
/// reanchor (Host compaction or Plan tenure handover); request-level truncation is
/// owned by the model's `view` declaration, so there is no promotable snapshot.
type ActivePrefixEpoch =
    {
        EpochId: PrefixEpochId
        /// HOST-006: which compaction pseudo-runs have already been reanchored.
        ///
        /// Durable, because a compaction message stays in the Host transcript forever.
        /// Every later reconcile observes the same run again, and the epoch check alone
        /// does NOT stop that: by then the epoch has moved on, so a freshly decided
        /// reanchor for that old compaction carries a `PreviousEpochId` that MATCHES the
        /// current one and would be accepted — advancing the epoch again and zeroing
        /// coverage the session had legitimately rebuilt.
        ///
        /// The epoch check and this set therefore guard different failures. The epoch
        /// check catches a replayed LINE (a crash between append and fold). This set
        /// catches a repeated DECISION (the same observation acted on twice). Neither
        /// subsumes the other.
        ///
        /// Bounded by the number of compactions in one session, not by turns.
        ReanchoredRuns: Set<ProviderRunIdentity>
        ReanchoredTenures: Set<string>
    }

/// Why a prefix-epoch line was refused. One case per PERSIST-010 rule.
[<RequireQualifiedAccess>]
type PrefixFoldRejection =
    /// The line was written against a different epoch than the one in force.
    | StalePrefixEpoch of expected: PrefixEpochId * actual: PrefixEpochId
    /// `NextEpochId` was not the successor of `PreviousEpochId`.
    | NonSequentialPrefixEpoch
    /// HOST-006: this compaction pseudo-run was already reanchored.
    ///
    /// Separate from `StalePrefixEpoch` because it is reachable with a perfectly
    /// current epoch — see `ReanchoredRuns`. Collapsing the two would make the fold
    /// accept a second reanchor for one compaction whenever any other epoch change
    /// happened in between.
    | CompactionAlreadyReanchored of run: ProviderRunIdentity
    | TenureAlreadyReanchored of incumbencyId: string

type PrefixReanchorCandidate =
    { PreviousEpochId: PrefixEpochId
      NextEpochId: PrefixEpochId
      CompactionRun: ProviderRunIdentity }

module PrefixEpochProjection =

    [<Import("Prefix", "wanxiangli/prefix")>]
    let private prefixOwnerConstructor: obj = jsNative

    let private prefixOwner: obj = createNew prefixOwnerConstructor ()

    let empty =
        { EpochId = PrefixEpochId.initial
          ReanchoredRuns = Set.empty
          ReanchoredTenures = Set.empty }

    let private epochOf (value: obj) : PrefixEpochId =
        match System.Int64.TryParse(unbox<string> value) with
        | true, epoch -> PrefixEpochId.create epoch
        | _ -> invalidArg "epoch" "Prefix epoch exceeds the int64 domain"

    let private stateView (state: ActivePrefixEpoch) : obj =
        createObj
            [ "epoch" ==> string (PrefixEpochId.value state.EpochId)
              "compactionRuns"
              ==> (state.ReanchoredRuns |> Set.toArray |> Array.map ProviderRunIdentity.value)
              "tenures" ==> Set.toArray state.ReanchoredTenures ]

    let private stateOf (value: obj) : ActivePrefixEpoch =
        { EpochId = epochOf value?epoch
          ReanchoredRuns =
            unbox<string array> value?compactionRuns
            |> Array.map ProviderRunIdentity.create
            |> Set.ofArray
          ReanchoredTenures = unbox<string array> value?tenures |> Set.ofArray }

    let private compaction (run: ProviderRunIdentity) : obj =
        createObj [ "kind" ==> "compaction"; "run" ==> ProviderRunIdentity.value run ]

    let private tenure (incumbencyId: string) : obj =
        createObj [ "kind" ==> "tenure"; "incumbencyId" ==> incumbencyId ]

    let private repeatedEvidence (evidence: obj) : PrefixFoldRejection =
        match string evidence?kind with
        | "compaction" ->
            PrefixFoldRejection.CompactionAlreadyReanchored(ProviderRunIdentity.create (string evidence?run))
        | "tenure" -> PrefixFoldRejection.TenureAlreadyReanchored(string evidence?incumbencyId)
        | _ -> invalidOp "Unknown prefix reanchor evidence"

    let private rejectionOf (result: obj) : PrefixFoldRejection =
        match string result?error with
        | "StaleEpoch" -> PrefixFoldRejection.StalePrefixEpoch(epochOf result?expected, epochOf result?actual)
        | "NonSequentialEpoch" -> PrefixFoldRejection.NonSequentialPrefixEpoch
        | "AlreadyReanchored" -> repeatedEvidence result?evidence
        | _ -> invalidOp "Unknown prefix fold rejection"

    let private commit previousEpoch nextEpoch evidence state =
        let candidate =
            createObj
                [ "previousEpoch" ==> string (PrefixEpochId.value previousEpoch)
                  "nextEpoch" ==> string (PrefixEpochId.value nextEpoch)
                  "evidence" ==> evidence ]

        let result: obj = prefixOwner?commit (stateView state, candidate)

        if unbox<bool> result?ok then
            Ok(stateOf result?state)
        else
            Error(rejectionOf result)

    /// PERSIST-010 `ContextReanchored`: HOST-006 containment.
    ///
    /// Retirement, not replacement. The projection cannot repoint the snapshot at a
    /// position after the Host summary: `CutoffExclusive` belongs to the voided
    /// current-generation XTrace semantic-turn numbering, and the Companion may have
    /// been behind the Host when compaction happened, so any new cutoff would be a
    /// claim the journal cannot support.
    ///
    /// The epoch still advances. This is a real cold boundary — the provider-visible
    /// prefix changed and the seal barrier broke — and COMPANION-009's byte-stability
    /// guarantee is scoped to one epoch, so staying on the same number would state
    /// something false.
    ///
    /// `observedRun` is recorded so the same compaction cannot be reanchored twice.
    /// That check has to be here and not only at the decision site: the compaction
    /// message stays in the transcript forever, so once the epoch has moved on for any
    /// other reason, a freshly decided reanchor for that old run would carry a matching
    /// `PreviousEpochId` and be accepted — advancing the epoch again and zeroing
    /// coverage the session had legitimately rebuilt.
    let applyReanchor
        (previousEpoch: PrefixEpochId)
        (nextEpoch: PrefixEpochId)
        (observedRun: ProviderRunIdentity)
        (state: ActivePrefixEpoch)
        : Result<ActivePrefixEpoch, PrefixFoldRejection> =
        commit previousEpoch nextEpoch (compaction observedRun) state

    /// HOST-006: has this compaction pseudo-run already been reanchored.
    ///
    /// Read the committed evidence through the prefix owner, without a runtime
    /// set that a restart would lose.
    let isReanchored (run: ProviderRunIdentity) (state: ActivePrefixEpoch) =
        unbox<bool> (prefixOwner?isReanchored (stateView state, compaction run))

    let applyTenureReanchor
        (previousEpoch: PrefixEpochId)
        (nextEpoch: PrefixEpochId)
        (incumbencyId: string)
        (state: ActivePrefixEpoch)
        : Result<ActivePrefixEpoch, PrefixFoldRejection> =
        commit previousEpoch nextEpoch (tenure incumbencyId) state

    let isTenureReanchored (incumbencyId: string) (state: ActivePrefixEpoch) =
        unbox<bool> (prefixOwner?isReanchored (stateView state, tenure incumbencyId))

    let proposeCompaction
        (observed: ProviderRunIdentity list)
        (state: ActivePrefixEpoch)
        : PrefixReanchorCandidate option =
        let runs = observed |> List.map ProviderRunIdentity.value |> List.toArray
        let candidate: obj = prefixOwner?proposeCompaction (stateView state, runs)

        if isNull candidate then
            None
        else
            Some
                { PreviousEpochId = epochOf candidate?previousEpoch
                  NextEpochId = epochOf candidate?nextEpoch
                  CompactionRun = ProviderRunIdentity.create (string candidate?evidence?run) }

    /// PERSIST-010 prefix-epoch absorption policy: `None` means the caller must
    /// write nothing, `Some reason` is the requirement text a refusal is
    /// reported with.
    ///
    /// `StalePrefixEpoch` is absorbed, unlike its frame counterpart. Every
    /// epoch-advancing line carries the epoch it expected, so a replayed reanchor —
    /// the crash-recovery path deliberately re-attempts it — arrives stale and means
    /// "already applied". That is what makes recovery idempotent without a second
    /// dedupe mechanism.
    let describe (rejection: PrefixFoldRejection) : string option =
        match rejection with
        | PrefixFoldRejection.StalePrefixEpoch _
        // HOST-006: the same compaction observed twice. Absorbed rather than fatal —
        // the observation repeats on every reconcile because the compaction message
        // stays in the transcript, so this is the expected steady state, not corruption.
        | PrefixFoldRejection.CompactionAlreadyReanchored _ -> None
        | PrefixFoldRejection.TenureAlreadyReanchored _ -> None
        | PrefixFoldRejection.NonSequentialPrefixEpoch ->
            Some "prefix epoch is not the successor of the previous one (PERSIST-010)"
