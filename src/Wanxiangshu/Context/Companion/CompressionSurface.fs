namespace Wanxiangshu.Context.Companion

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Attempt.Fallback

/// Context-compression decision owner. Attempt choice, recovery-slot dispatch
/// and terminal validity cross this JSON boundary; prefix selection and epoch
/// behavior are owned by `PrefixSurface`.
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

    let private snapshotOfJs (value: obj) : PrefixSnapshot =
        { FrozenRecordPrefixRef = BlobRef.create (text value?ref)
          FrozenRecordPrefixDigest = BlobDigest.create (text value?frozenDigest)
          CutoffExclusive = intValue value?cutoff
          CoveredPrefixDigest = text value?prefixDigest
          SealRoot = text value?sealRoot
          SyntheticMessageId = text value?syntheticId }

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

    let private reasonOf (value: obj) : NoCandidateReason =
        match text value with
        | "CoverageNotAheadOfRequest" -> NoCandidateReason.CoverageNotAheadOfRequest
        | "WouldRetreat" -> NoCandidateReason.WouldRetreat(0, 0)
        | "NotNewerThanCommitted" -> NoCandidateReason.NotNewerThanCommitted
        | "CutoffProofFailed" -> NoCandidateReason.CutoffProofFailed("", "")
        | _ -> NoCandidateReason.NoCoverage

    let private optionObj (value: 'a option) : obj =
        match value with
        | None -> null
        | Some item -> box item

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

    let private requestKindLabels: string array =
        [| ProviderRequestKind.WorkMain
           ProviderRequestKind.BloggerMain
           ProviderRequestKind.BloggerSquash
           ProviderRequestKind.InteractionRepair
           ProviderRequestKind.StrengthReplica |]
        |> Array.map ProviderRequestKind.label

    let private requestKindMayCarryProbe (kind: string) : bool =
        requestKindOf kind
        |> Option.map ProviderRequestKind.mayCarryProbe
        |> Option.defaultValue false

    let private requestKindLabel (kind: string) : string =
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

    let private requestKindResult value : Result<ProviderRequestKind, string> =
        if isNullish value then
            Ok ProviderRequestKind.WorkMain
        else
            match requestKindOf value with
            | Some requestKind -> Ok requestKind
            | None -> Error(sprintf "unknown request kind: %s" (text value))

    let private armingOf (value: string) =
        if value = "ArmedByAdvance" then
            SlotArming.ArmedByAdvance
        else
            SlotArming.NotArmed

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

    let beginSequence: string = "NotArmed"
    let afterFailureAdvance: string = "ArmedByAdvance"
    let afterRestart: string = "NotArmed"
    let isArmed (value: string) : bool = RecoverySlot.isArmed (armingOf value)

    let mayRecover (arming: string) (offset: int) (hasMaterial: bool) : bool =
        RecoverySlot.mayRecover (armingOf arming) (offsetOf offset) hasMaterial

    let recoveryOpportunity (arming: string) (offset: int) : string =
        match RecoverySlot.opportunity (armingOf arming) (offsetOf offset) with
        | RecoveryOpportunity.OrdinaryAttempt -> "OrdinaryAttempt"
        | RecoveryOpportunity.RecoveryAttempt -> "RecoveryAttempt"

    let private bloggerDispatchErrorName (error: BloggerSlotDispatchError) : string =
        match error with
        | BloggerSlotDispatchError.MissingProjection -> "MissingProjection"
        | BloggerSlotDispatchError.NoActiveBloggerRun -> "NoActiveBloggerRun"

    let nextBloggerRequest (failedKind: string) (opportunity: string) (hasSquashMaterial: bool) : string =
        let nextOpportunity =
            if opportunity = "RecoveryAttempt" then
                RecoveryOpportunity.RecoveryAttempt
            else
                RecoveryOpportunity.OrdinaryAttempt

        match requestKindOf failedKind with
        | None -> bloggerDispatchErrorName BloggerSlotDispatchError.MissingProjection
        | Some kind ->
            match RecoverySlot.nextBloggerRequest kind nextOpportunity hasSquashMaterial with
            | Ok next -> ProviderRequestKind.label next
            | Error error -> bloggerDispatchErrorName error

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

    let armingName (value: string) : string = value

    let cursor =
        box {| isRecoverySlot = (fun offset -> AgentPairCursor.isRecoverySlot (offsetOf offset)) |}


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
            let peerTier =
                if tier = AgentTier.Fast then
                    AgentTier.Deep
                else
                    AgentTier.Fast

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
                    (if not (isNullish value?mayRecover) && unbox<bool> value?mayRecover then
                         RecoveryOpportunity.RecoveryAttempt
                     else
                         RecoveryOpportunity.OrdinaryAttempt)
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

    let private terminalValidityResult (value: string) : Result<unit, TerminalValidity.Rejection> =
        TerminalValidity.check value

    let private terminalRejectionName rejection =
        match rejection with
        | TerminalValidity.Rejection.Empty -> "Empty"
        | TerminalValidity.Rejection.XmlOnly -> "XmlOnly"

    let terminalValidityCheck (value: string) : obj =
        match terminalValidityResult value with
        | Ok() -> box {| ok = true |}
        | Error rejection ->
            box
                {| ok = false
                   error = terminalRejectionName rejection |}

    let terminalValidityIsValid (value: string) : bool =
        match terminalValidityResult value with
        | Ok() -> true
        | Error _ -> false

    let terminalValidityDescription (value: string) : string =
        match value with
        | "Empty" -> TerminalValidity.describe TerminalValidity.Rejection.Empty
        | "XmlOnly" -> TerminalValidity.describe TerminalValidity.Rejection.XmlOnly
        | _ -> "unknown terminal rejection"

    let terminalValidity (value: string) : obj =
        match terminalValidityResult value with
        | Ok() -> box {| valid = true; rejection = null |}
        | Error rejection ->
            box
                {| valid = false
                   rejection = terminalRejectionName rejection |}

    let terminalRequestOwnership (value: obj) : string =
        let requestId = BloggerRequestId.create (text value?requestId)

        let openRequestId =
            optionalText value?openRequestId |> Option.map BloggerRequestId.create

        let openPromptKey = optionalText value?openPromptKey |> Option.map PromptKey.create

        let parent: BloggerTerminalParentEvidence option =
            optionalText value?parentPromptKey
            |> Option.map (fun promptKey ->
                let isInteractionRepair = optionalText value?parentOrigin = Some "InteractionRepair"

                { PromptKey = PromptKey.create promptKey
                  IsRequestScopedRepair =
                    isInteractionRepair
                    && PromptAuthority.repairPayloadBelongsToRequest requestId (text value?parentPayloadDigest) })

        match BloggerRequestOwnership.decide requestId openRequestId openPromptKey parent with
        | BloggerTerminalRequestOwnership.Current -> "Current"
        | BloggerTerminalRequestOwnership.Superseded -> "Superseded"
        | BloggerTerminalRequestOwnership.Unproven -> "Unproven"

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
