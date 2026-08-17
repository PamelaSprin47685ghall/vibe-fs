namespace Wanxiangshu.Context.Companion

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Attempt.Fallback

/// Context-compression decision owner. Prefix candidate selection, attempt
/// choice, recovery-slot dispatch and terminal validity share one JSON boundary;
/// the production DUs, maps and identities do not cross into semantic tests.
[<RequireQualifiedAccess>]
module CompressionSurface =

    type private AttemptPlanHandle(plan: AttemptPlan) =
        member _.Value = plan

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private text (value: obj) : string =
        if isNullish value then "" else string value

    let private intValue (value: obj) : int = int (text value)

    let private int64Value (value: obj) : int64 = int64 (text value)

    let private optionalText (value: obj) : string option =
        if isNullish value then None else Some(text value)

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

    let private probeToJs (probe: PrefixProbe) : obj =
        box
            {| probeId = probe.ProbeId
               basedOnEpoch = int (PrefixEpochId.value probe.BasedOnEpochId)
               candidate = snapshotToJs probe.Candidate |}

    let private probeOfJs (value: obj) : PrefixProbe =
        { ProbeId = text value?probeId
          BasedOnEpochId = PrefixEpochId.create (int64Value value?basedOnEpoch)
          Candidate = snapshotOfJs value?candidate }

    let private reasonName (reason: NoCandidateReason) : string =
        match reason with
        | NoCandidateReason.NoCoverage -> "NoCoverage"
        | NoCandidateReason.CoverageNotAheadOfRequest -> "CoverageNotAheadOfRequest"
        | NoCandidateReason.WouldRetreat _ -> "WouldRetreat"
        | NoCandidateReason.NotNewerThanCommitted -> "NotNewerThanCommitted"
        | NoCandidateReason.CutoffProofFailed _ -> "CutoffProofFailed"

    let private reasonText (reason: NoCandidateReason) = PrefixProbeSelection.describeNoCandidate reason

    let private reasonOf (value: obj) : NoCandidateReason =
        match text value with
        | "CoverageNotAheadOfRequest" -> NoCandidateReason.CoverageNotAheadOfRequest
        | "WouldRetreat" -> NoCandidateReason.WouldRetreat(0, 0)
        | "NotNewerThanCommitted" -> NoCandidateReason.NotNewerThanCommitted
        | "CutoffProofFailed" -> NoCandidateReason.CutoffProofFailed("", "")
        | _ -> NoCandidateReason.NoCoverage

    let private selectionResult (result: Result<PrefixProbe, NoCandidateReason>) : obj =
        match result with
        | Ok probe ->
            let candidate = probe.Candidate

            box
                {| ok = true
                   probeId = probe.ProbeId
                   basedOnEpoch = int64 (PrefixEpochId.value probe.BasedOnEpochId)
                   candidate = snapshotToJs candidate
                   cutoff = candidate.CutoffExclusive
                   sealRoot = candidate.SealRoot
                   syntheticId = candidate.SyntheticMessageId |}
        | Error reason ->
            box
                {| ok = false
                   error = reasonName reason
                   message = reasonText reason |}

    /// Build a candidate from the current Companion proof, or return its named
    /// normal no-candidate reason. `recomputeDigest` is the caller's current-X
    /// digest oracle, not a cached value.
    let select (value: obj) : obj =
        let committed =
            if isNullish value?committedSnapshot then None else Some(snapshotOfJs value?committedSnapshot)

        let recompute =
            if isNullish value?recomputeDigest then
                fun (_: int) -> ""
            else
                unbox<int -> string> value?recomputeDigest

        let result =
            PrefixProbeSelection.select
                (shaOf value?sha256)
                (SessionId.create (text value?session))
                (PrefixEpochId.create (int64Value value?committedEpoch))
                committed
                (intValue value?coverableCutoff)
                (text value?coveredDigest)
                (intValue value?requestStartCutoff)
                (BlobRef.create (if isNullish value?frozenRef then "blob-frozen-" + string (intValue value?coverableCutoff) else text value?frozenRef))
                (BlobDigest.create (text value?frozenDigest))
                recompute

        selectionResult result

    let prefixEmpty : obj =
        box {| epoch = 0; snapshot = null |}

    let private optionObj (value: 'a option) : obj =
        match value with
        | None -> null
        | Some item -> box item

    let private prefixStateToJs (state: ActivePrefixEpoch) : obj =
        box
            {| epoch = int (PrefixEpochId.value state.EpochId)
               snapshot = optionObj (state.Snapshot |> Option.map snapshotToJs) |}

    let private prefixStateOfJs (value: obj) : ActivePrefixEpoch =
        { EpochId = PrefixEpochId.create (int64Value value?epoch)
          Snapshot = if isNullish value?snapshot then None else Some(snapshotOfJs value?snapshot)
          ReanchoredRuns = Set.empty }

    let private prefixRejectionName (rejection: PrefixFoldRejection) : string =
        match rejection with
        | PrefixFoldRejection.StalePrefixEpoch _ -> "StalePrefixEpoch"
        | PrefixFoldRejection.NonSequentialPrefixEpoch -> "NonSequentialPrefixEpoch"
        | PrefixFoldRejection.CutoffRetreated _ -> "CutoffRetreated"
        | PrefixFoldRejection.CandidateNotNew -> "CandidateNotNew"
        | PrefixFoldRejection.CompactionAlreadyReanchored _ -> "CompactionAlreadyReanchored"

    let private prefixResultToJs (result: Result<ActivePrefixEpoch, PrefixFoldRejection>) : obj =
        match result with
        | Ok value -> box {| ok = true; value = prefixStateToJs value |}
        | Error rejection -> box {| ok = false; error = prefixRejectionName rejection |}

    let applyRebase (request: obj) (state: obj) : obj =
        PrefixEpochProjection.applyRebase
            (PrefixEpochId.create (int64Value request?previousEpoch))
            (PrefixEpochId.create (int64Value request?nextEpoch))
            (snapshotOfJs request?candidate)
            (prefixStateOfJs state)
        |> prefixResultToJs

    let prefixSnapshot (value: obj) : obj = snapshotOfJs value |> snapshotToJs
    let empty : obj = prefixEmpty
    let snapshot (value: obj) : obj = prefixSnapshot value

    let prefixSnapshotFromProbe (value: obj) : obj = probeOfJs value |> fun probe -> snapshotToJs probe.Candidate

    let private requestKindOf (value: obj) : ProviderRequestKind option =
        match text value |> fun value -> value.ToLowerInvariant() with
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

    let private requestKindResult value : Result<ProviderRequestKind, string> =
        if isNullish value then
            Ok ProviderRequestKind.WorkMain
        else
            match requestKindOf value with
            | Some requestKind -> Ok requestKind
            | None -> Error(sprintf "unknown request kind: %s" (text value))

    let private armingOf (value: string) =
        if value = "ArmedByAdvance" then SlotArming.ArmedByAdvance else SlotArming.NotArmed

    let private offsetOf (value: int) =
        match ((value % 4) + 4) % 4 with
        | 1 -> AgentPairCursor.FallbackOffset.Fork1
        | 2 -> AgentPairCursor.FallbackOffset.Fork2
        | 3 -> AgentPairCursor.FallbackOffset.Fork3
        | _ -> AgentPairCursor.FallbackOffset.Fork0

    let private cursorOfJs (value: obj) : AgentPairCursor.FallbackCursor =
        if isNullish value then
            AgentPairCursor.initial
        else
            { Offset = offsetOf (intValue value?offset)
              ConsecutiveFailureCount = intValue value?failures }

    let private cursorView (cursor: AgentPairCursor.FallbackCursor) : obj =
        box
            {| offset = int (AgentPairCursor.FallbackOffsetCodec.toByte cursor.Offset)
               failures = cursor.ConsecutiveFailureCount |}

    let beginSequence : string = "NotArmed"
    let afterFailureAdvance : string = "ArmedByAdvance"
    let afterRestart : string = "NotArmed"
    let isArmed (value: string) : bool = RecoverySlot.isArmed (armingOf value)

    let mayRecover (arming: string) (offset: int) (hasMaterial: bool) : bool =
        RecoverySlot.mayRecover (armingOf arming) (offsetOf offset) hasMaterial

    let private outcomeResult (value: obj) : Result<AttemptOutcome, string> =
        match text value with
        | "Completed" -> Ok AttemptOutcome.Completed
        | "CompletedInvalid" -> Ok AttemptOutcome.CompletedInvalid
        | "Failed" -> Ok AttemptOutcome.Failed
        | "Aborted" -> Ok AttemptOutcome.Aborted
        | unknown -> Error(sprintf "unknown attempt outcome: %s" unknown)

    let private decisionName (decision: SlotDecision) : string =
        match decision with
        | SlotDecision.CommitSquashThenMain -> "CommitSquashThenMain"
        | SlotDecision.MainWithoutSquash -> "MainWithoutSquash"
        | SlotDecision.CommitMain _ -> "CommitMain"
        | SlotDecision.RepairOnce -> "RepairOnce"
        | SlotDecision.AbandonRoundProduct -> "AbandonRoundProduct"
        | SlotDecision.FailSlot -> "FailSlot"

    let private decisionToJs (decision: SlotDecision) : obj =
        box
            {| name = decisionName decision
               advancesCursor = RecoverySlot.advancesCursor decision
               nextArmingName =
                if RecoverySlot.nextArming decision = SlotArming.ArmedByAdvance then
                    "ArmedByAdvance"
                else
                    "NotArmed"
               clearsFailureCount =
                match decision with
                | SlotDecision.CommitMain clears -> clears
                | _ -> false |}

    let onSquash (outcome: string) : obj =
        match outcomeResult (box outcome) with
        | Error error -> box {| ok = false; error = error |}
        | Ok outcome -> RecoverySlot.onSquashOutcome outcome |> decisionToJs

    let onMain (value: obj) : obj =
        match requestKindResult value?kind, outcomeResult value?outcome with
        | Ok kind, Ok outcome ->
            let consumed = not (isNullish value?aabbConsumed) && unbox<bool> value?aabbConsumed
            RecoverySlot.onMainOutcome kind consumed outcome |> decisionToJs
        | Error error, _
        | _, Error error -> box {| ok = false; error = error |}

    let requestKindLabels : string array =
        [| ProviderRequestKind.WorkMain
           ProviderRequestKind.BloggerMain
           ProviderRequestKind.BloggerSquash
           ProviderRequestKind.InteractionRepair
           ProviderRequestKind.StrengthReplica |]
        |> Array.map ProviderRequestKind.label

    let requestKindMayCarryProbe (kind: string) : bool =
        requestKindOf kind |> Option.map ProviderRequestKind.mayCarryProbe |> Option.defaultValue false

    let requestKindLabel (kind: string) : string = requestKindOf kind |> Option.map ProviderRequestKind.label |> Option.defaultValue ""

    let armingName (value: string) : string = value

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

    let cursor = box {| isRecoverySlot = (fun offset -> offset % 2 <> 0) |}


    let private roleResult value : Result<Role, string> =
        if isNullish value then
            Ok Role.Coder
        else
            match Roles.tryParseRole (text value) with
            | Some role -> Ok role
            | None -> Error(sprintf "unknown role: %s" (text value))

    let private tierResult value : Result<AgentTier, string> =
        if isNullish value then
            Ok AgentTier.Fast
        else
            match Roles.tryParseTier (text value) with
            | Some tier -> Ok tier
            | None -> Error(sprintf "unknown tier: %s" (text value))

    let private attemptProbeOf (value: obj) : PrefixProbe = probeOfJs value

    let private attemptPlanCore (value: obj) : Result<AttemptPlan, string> =
        match roleResult value?role, tierResult value?tier, requestKindResult value?kind with
        | Ok role, Ok tier, Ok requestKind ->
            let peerTier = if tier = AgentTier.Fast then AgentTier.Deep else AgentTier.Fast

            let authority: PromptAuthority.AuthorityExecutionProfile =
                { SessionId = SessionId.create "surface-session"
                  LogicalRunId = LogicalRunId.create "surface-run"
                  AuthorityRootUserMessageId = AuthorityRootUserMessageId.create "surface-root"
                  AuthorityKind = PromptAuthority.RootAuthorityKind.HumanRoot
                  SelectedAgent = Roles.managedAgentName tier role
                  PeerAgent = Roles.managedAgentName peerTier role
                  CanonicalRole = role
                  SelectedTier = tier }

            let selectProbe () =
                if not (isNullish value?probe) then
                    Ok(attemptProbeOf value?probe)
                else
                    Error(reasonOf value?noCandidateReason)

            Ok(
                AttemptPlanner.plan
                    authority
                    (cursorOfJs value?cursor)
                    (PhysicalUserMessageId.create "surface-user")
                    (ProviderRunIdentity.create "surface-provider-run")
                    (PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.HumanRoot)
                    requestKind
                    (not (isNullish value?mayRecover) && unbox<bool> value?mayRecover)
                    selectProbe
            )
        | Error error, _, _
        | _, Error error, _
        | _, _, Error error -> Error error

    let private attemptPlanView (plan: AttemptPlan) : obj =
        let choice, probeId =
            match plan.Profile.ProjectionChoice with
            | XProjectionChoice.UseCommittedEpoch -> "UseCommittedEpoch", None
            | XProjectionChoice.UsePrefixProbe probe -> "UsePrefixProbe", Some probe.ProbeId

        box
            {| choice = choice
               probeId = optionObj probeId
               noProbeReason = optionObj (plan.NoProbeReason |> Option.map reasonName)
               effectiveAgent = plan.Profile.EffectiveAgent |}

    /// Build the production AttemptPlan from plain request labels. The caller
    /// supplies either a probe or a named no-candidate result; the planner itself
    /// still owns the choice and defers probe selection until it is allowed.
    let attemptPlan (value: obj) : obj =
        match attemptPlanCore value with
        | Ok plan -> attemptPlanView plan
        | Error error -> box {| ok = false; error = error |}

    /// JSON/opaque owner API for semantic tests. `handle` is deliberately opaque:
    /// only `promotableProbeId` can consume it, while the profile observations stay
    /// plain JSON.
    let private attemptPlanWithHandle (value: obj) : obj =
        match attemptPlanCore value with
        | Error error -> box {| ok = false; error = error |}
        | Ok plan ->
            let view = attemptPlanView plan
            let viewObject = unbox<obj> view

            box
                {| choice = viewObject?choice
                   probeId = viewObject?probeId
                   noProbeReason = viewObject?noProbeReason
                   effectiveAgent = viewObject?effectiveAgent
                   handle = box (AttemptPlanHandle plan) |}

    let private promotableProbeId (value: obj) (outcome: string) : obj =
        let handleValue = value?handle

        if isNullish handleValue then
            null
        else
            let handle = unbox<AttemptPlanHandle> handleValue

            match outcomeResult (box outcome) with
            | Error _ -> null
            | Ok outcome ->
                match AttemptPlanner.promotableProbe handle.Value outcome with
                | None -> null
                | Some probe -> box probe.ProbeId

    let attemptPlanner =
        box
            {| plan = (fun value -> attemptPlanWithHandle value)
               promotableProbeId = (fun value outcome -> promotableProbeId value outcome) |}

    /// Prefix probe fixture constructor at the owner boundary.
    let prefixProbe (value: obj) : obj =
        box
            {| probeId = text value?probeId
               basedOnEpoch = int64Value value?basedOnEpoch
               candidate = snapshotToJs (snapshotOfJs value?candidate) |}

    let terminalValidity (value: string) : obj =
        match TerminalValidity.check value with
        | Ok() -> box {| valid = true; rejection = null |}
        | Error rejection ->
            box
                {| valid = false
                   rejection =
                    match rejection with
                    | TerminalValidity.Rejection.Empty -> "Empty"
                    | TerminalValidity.Rejection.XmlOnly -> "XmlOnly" |}
    let compactionSettingPaths : string array =
        HostCompactionPolicy.requiredSettings
        |> List.map (fun setting -> String.concat "." setting.Path)
        |> List.toArray

    let compactionSettings : obj array =
        HostCompactionPolicy.requiredSettings
        |> List.map (fun setting ->
            box
                {| path = String.concat "." setting.Path
                   required = setting.Required
                   clause = setting.Clause
                   reason = setting.Reason |})
        |> List.toArray

    let compactionAutoContinueEnabled = HostCompactionPolicy.autoContinueEnabled

    let compactionJudgeFirstTurn (value: obj) : obj =
        let unavailable =
            if isNullish value?unavailable then
                None
            else
                HostCompactionPolicy.requiredSettings
                |> List.tryFind (fun setting -> String.concat "." setting.Path = text value?unavailable)

        let verdict =
            HostCompactionPolicy.judgeFirstTurn
                unavailable
                (SessionId.create (text value?session))
                (intValue value?pseudoRuns)

        let name, message =
            match verdict with
            | CompactionGateVerdict.Satisfied -> "Satisfied", HostCompactionPolicy.describeVerdict verdict
            | CompactionGateVerdict.SettingUnavailable _ -> "SettingUnavailable", HostCompactionPolicy.describeVerdict verdict
            | CompactionGateVerdict.CompactedDespiteSettings _ ->
                "CompactedDespiteSettings", HostCompactionPolicy.describeVerdict verdict

        box {| name = name; message = message |}

    let compactionIsContainable (isCompaction: bool) : bool = HostCompactionPolicy.isContainableCompaction isCompaction

    let compactionNextReanchor (observed: string array) (reanchored: string array) : obj =
        let handled =
            reanchored
            |> Array.toList
            |> List.map ProviderRunIdentity.create
            |> Set.ofList

        HostCompactionPolicy.nextReanchor
            (observed |> Array.toList |> List.map ProviderRunIdentity.create)
            (fun run -> Set.contains run handled)
        |> Option.map ProviderRunIdentity.value
        |> optionObj

    let openingFloor (value: obj) : obj =
        let hasOpenLife = not (isNullish value?hasOpenLife) && unbox<bool> value?hasOpenLife
        let planCommitted = not (isNullish value?planCommitted) && unbox<bool> value?planCommitted
        let openingSequence = int64Value value?openingSequence
        let headSequence = int64Value value?xTraceHeadSequence

        let parts =
            if isNullish value?parts then
                []
            else
                (unbox<obj array> value?parts)
                |> Array.toList
                |> List.map (fun item ->
                    { Cursor = { Sequence = int64Value item?sequence }
                      Kind = text item?kind
                      ToolCallId = optionalText item?toolCallId |> Option.map ToolCallId.create })

        MagicTodo.effectiveOpeningFloor
            hasOpenLife
            planCommitted
            { Sequence = openingSequence }
            None
            None
            headSequence
            parts
        |> Option.map (fun cursor -> int cursor.Sequence)
        |> optionObj

    let bloggerEffectiveStart (ingestedThrough: int) (workRecordStartSequence: int) : int =
        MagicTodo.bloggerEffectiveStart
            { IngestedThrough = { Sequence = int64 ingestedThrough } }
            { Sequence = int64 workRecordStartSequence }
        |> fun cursor -> int cursor.Sequence

    let diagnosticEmit (operation: string) (fields: obj array) : unit =
        let pairs =
            fields
            |> Array.toList
            |> List.map (fun pair ->
                let values = unbox<obj array> pair
                text values.[0], text values.[1])

        Wanxiangshu.OpenCode.Diagnostic.emit operation pairs

    let diagnosticFatal (operation: string) (fields: obj array) : unit =
        let pairs =
            fields
            |> Array.toList
            |> List.map (fun pair ->
                let values = unbox<obj array> pair
                text values.[0], text values.[1])

        Wanxiangshu.OpenCode.Diagnostic.fatal operation pairs
