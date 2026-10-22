namespace Wanxiangshu.Interaction.Authority

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Typed projection of the stateless `wanxiangli/authority` decision law (CUT-09):
/// logical-root registration, exact closure and origin precedence. The
/// projection record, fact codec and durable writes stay here.
module internal AuthorityLaw =

    type private Law =
        abstract register: active: obj * requested: obj -> string
        abstract close: active: obj * last: obj * logicalRunId: string * rootId: string -> string

        abstract closeChildWork:
            active: obj * activeIsManager: bool * last: obj * logicalRunId: string * rootId: string -> string

        abstract resolveOrigin: input: obj -> obj
        abstract expectedClosureKind: rootKind: string * isManager: bool * agentOwnerLife: obj -> obj
        abstract encodeClosureWitness: witness: obj -> string
        abstract admitClosure: closing: obj * recordedRootKind: string * witnessText: string -> obj

    [<Import("Authority", "wanxiangli/authority")>]
    let private authorityClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : Law = jsNative

    let private law: Law = construct authorityClass

    [<Emit("JSON.stringify($0)")>]
    let private canonicalJson (value: obj) : string = jsNative

    let private kindLabel (kind: PromptRootAuthorityKind) =
        match kind with
        | HumanRoot -> "HumanRoot"
        | AgentOwnerRoot -> "AgentOwnerRoot"

    let private kindOfLabel (label: string) =
        match label with
        | "HumanRoot" -> HumanRoot
        | "AgentOwnerRoot" -> AgentOwnerRoot
        | other -> failwithf "wanxiangli/authority returned an unknown root kind %s" other

    /// The library compares roots by exact identifiers; the identity seed enters
    /// as its canonical JSON (the lossless `toInput` view), so two seeds are the
    /// same key exactly when they are the same seed.
    let private root (profile: PromptAuthority.AuthorityExecutionProfile) : obj =
        createObj
            [ "sessionId" ==> SessionId.value profile.SessionId
              "logicalRunId" ==> LogicalRunId.value profile.LogicalRunId
              "rootId" ==> AuthorityRootUserMessageId.value profile.AuthorityRootUserMessageId
              "kind" ==> kindLabel profile.AuthorityKind
              "seed" ==> canonicalJson (box (PromptIdentitySeed.toInput profile.IdentitySeed)) ]

    let private optionalRoot (profile: PromptAuthority.AuthorityExecutionProfile option) : obj =
        match profile with
        | Some value -> root value
        | None -> null

    let register active requested =
        law.register (optionalRoot active, root requested)

    let close active last (logicalRunId: LogicalRunId) (authorityRoot: AuthorityRootUserMessageId) =
        law.close (
            optionalRoot active,
            optionalRoot last,
            LogicalRunId.value logicalRunId,
            AuthorityRootUserMessageId.value authorityRoot
        )

    let closeChildWork
        (active: PromptAuthority.AuthorityExecutionProfile option)
        last
        (logicalRunId: LogicalRunId)
        (authorityRoot: AuthorityRootUserMessageId)
        =
        let activeIsManager =
            active |> Option.exists (fun profile -> profile.CanonicalRole = Role.Manager)

        law.closeChildWork (
            optionalRoot active,
            activeIsManager,
            optionalRoot last,
            LogicalRunId.value logicalRunId,
            AuthorityRootUserMessageId.value authorityRoot
        )

    let private originView (origin: PromptOrigin) : obj =
        match origin with
        | PromptOrigin.AuthorityRoot kind -> createObj [ "kind" ==> "AuthorityRoot"; "root" ==> kindLabel kind ]
        | PromptOrigin.Continuation _ ->
            createObj
                [ "kind" ==> "Continuation"
                  "continuation" ==> PromptAuthority.originLabel origin ]
        | PromptOrigin.HostInternal -> createObj [ "kind" ==> "HostInternal" ]
        | PromptOrigin.UnknownOrigin -> createObj [ "kind" ==> "UnknownOrigin" ]

    let private continuationOfLabel (label: string) : PromptOrigin =
        PromptAuthority.tryParseContinuationKind label
        |> Option.map PromptOrigin.Continuation
        |> Option.defaultWith (fun () -> failwithf "wanxiangli/authority returned an unknown continuation %s" label)

    let private originOfView (view: obj) : PromptOrigin =
        match unbox<string> view?kind with
        | "AuthorityRoot" -> PromptOrigin.AuthorityRoot(kindOfLabel (unbox<string> view?root))
        | "Continuation" -> continuationOfLabel (unbox<string> view?continuation)
        | "HostInternal" -> PromptOrigin.HostInternal
        | "UnknownOrigin" -> PromptOrigin.UnknownOrigin
        | other -> failwithf "wanxiangli/authority returned an unknown origin %s" other

    let resolveOrigin
        (acceptedContinuation: PromptContinuationKind option)
        (claimOrigin: PromptOrigin option)
        (hasPromptKey: bool)
        (hostCompaction: bool)
        (activeKind: PromptRootAuthorityKind option)
        : PromptOrigin =
        law.resolveOrigin (
            createObj
                [ "acceptedContinuation"
                  ==> (acceptedContinuation
                       |> Option.map (fun kind -> PromptAuthority.originLabel (PromptOrigin.Continuation kind))
                       |> Option.toObj)
                  "claimOrigin" ==> (claimOrigin |> Option.map originView |> Option.toObj)
                  "hasPromptKey" ==> hasPromptKey
                  "hostCompaction" ==> hostCompaction
                  "activeKind" ==> (activeKind |> Option.map kindLabel |> Option.toObj) ]
        )
        |> originOfView

    /// The agent-owner life the library's law vocabulary uses.
    let private agentOwnerLifeArg (life: string option) : obj =
        match life with
        | Some "Child" -> box "child"
        | Some "Attached" -> box "attached"
        | Some "InternalLeaf" -> box "internal-leaf"
        | _ -> null

    /// interaction-authority-018: the closure kind an accepted root expects.
    ///
    /// The kind comes from the durable acceptance contract, not from the
    /// profile: the profile says who runs, the acceptance says how the run
    /// ends. An agent-owner root whose life cannot be proven has no kind and
    /// therefore admits no closure.
    let expectedClosureKind (rootKind: string) (isManager: bool) (agentOwnerLife: string option) : string option =
        law.expectedClosureKind (rootKind, isManager, agentOwnerLifeArg agentOwnerLife)
        |> Option.ofObj
        |> Option.map unbox<string>

    let private closing
        (rootKind: string)
        (sessionId: string)
        (logicalRunId: string)
        (authorityRoot: string)
        (ownerLogicalRunId: string option)
        (kind: string)
        : obj =
        createObj
            [ "kind" ==> kind
              "sessionId" ==> sessionId
              "logicalRunId" ==> logicalRunId
              "rootId" ==> authorityRoot
              "ownerLogicalRunId" ==> (ownerLogicalRunId |> Option.toObj)
              // A Manager life is the life of the Manager's own session.
              "lifeId" ==> (if kind = "HumanRootManagerLife" then sessionId else null) ]

    let private admitKnownRootClosure
        (closingRoot: obj)
        (kind: string)
        (recordedRootKind: string)
        (witness: string)
        : Result<unit, string> =
        let verdict = law.admitClosure (closingRoot, recordedRootKind, witness)

        if unbox<bool> verdict?ok then
            Ok()
        else
            Error(sprintf "%s closure refused: %s" kind (unbox<string> verdict?reason))

    /// Admit a closure (new or durably replayed) for exactly this accepted root.
    let admitClosure
        (contract: PromptAuthority.AcceptedRootClosure)
        (sessionId: string)
        (logicalRunId: string)
        (recordedRootKind: string)
        (witness: string)
        : Result<unit, string> =
        match contract.ExpectedClosureKind, recordedRootKind with
        | "", _ -> Error "the accepted root has no expected closure kind"
        | _, ("HumanRoot" | "AgentOwnerRoot") ->
            admitKnownRootClosure
                (closing
                    contract.RootKind
                    sessionId
                    logicalRunId
                    (AuthorityRootUserMessageId.value contract.AuthorityRootUserMessageId)
                    contract.OwnerLogicalRunId
                    contract.ExpectedClosureKind)
                contract.ExpectedClosureKind
                recordedRootKind
                witness
        | _, other -> Error(sprintf "closure RootKind is unknown: %s" other)

    /// The canonical witness of a completed Manager life: its delivered S4 retirement.
    let managerLifeWitness (expectedKind: string) (sessionId: string) (retirementId: string) : string option =
        match expectedKind with
        | "HumanRootManagerLife" ->
            Some(
                law.encodeClosureWitness (
                    createObj
                        [ "kind" ==> "LifeCompleted"
                          "lifeId" ==> sessionId
                          "factId" ==> retirementId ]
                )
            )
        | _ -> None

    /// The canonical witness of a managed (non-Manager) HumanRoot run: the
    /// durable terminal of that run.
    let managedRunWitness (expectedKind: string) (logicalRunId: string) (factId: string) : string option =
        match expectedKind with
        | "HumanRootManagedRun" ->
            Some(
                law.encodeClosureWitness (
                    createObj
                        [ "kind" ==> "ManagedLogicalRunTerminal"
                          "logicalRunId" ==> logicalRunId
                          "factId" ==> factId ]
                )
            )
        | _ -> None

    /// The canonical witness of a completed agent-owner child work.
    let childWorkWitness
        (expectedKind: string)
        (ownerLogicalRunId: string)
        (childLogicalRunId: string)
        (factId: string)
        : string option =
        match expectedKind with
        | "AgentOwnerChildWork" ->
            Some(
                law.encodeClosureWitness (
                    createObj
                        [ "kind" ==> "ChildLogicalRunTerminal"
                          "ownerLogicalRunId" ==> ownerLogicalRunId
                          "childLogicalRunId" ==> childLogicalRunId
                          "factId" ==> factId ]
                )
            )
        | _ -> None

    /// The canonical witness of a completed agent-owner attached work.
    let attachedWorkWitness
        (expectedKind: string)
        (ownerLogicalRunId: string)
        (childLogicalRunId: string)
        (attachmentKind: string)
        (associationGeneration: string)
        (factId: string)
        : string option =
        match expectedKind with
        | "AgentOwnerAttachedWork" ->
            Some(
                law.encodeClosureWitness (
                    createObj
                        [ "kind" ==> "AttachedLogicalRunTerminal"
                          "ownerLogicalRunId" ==> ownerLogicalRunId
                          "childLogicalRunId" ==> childLogicalRunId
                          "attachmentKind" ==> attachmentKind
                          "associationGeneration" ==> associationGeneration
                          "factId" ==> factId ]
                )
            )
        | _ -> None

    /// The canonical witness of a completed agent-owner internal leaf.
    let internalLeafWitness
        (expectedKind: string)
        (ownerLogicalRunId: string)
        (leafLogicalRunId: string)
        (decisionOrTransactionId: string)
        (factId: string)
        : string option =
        match expectedKind with
        | "AgentOwnerInternalLeaf" ->
            Some(
                law.encodeClosureWitness (
                    createObj
                        [ "kind" ==> "InternalLeafTerminal"
                          "ownerLogicalRunId" ==> ownerLogicalRunId
                          "leafLogicalRunId" ==> leafLogicalRunId
                          "decisionOrTransactionId" ==> decisionOrTransactionId
                          "factId" ==> factId ]
                )
            )
        | _ -> None

/// Pure authority-run lifecycle transitions. Types and identity rules live in
/// PromptAuthority; this module owns claim / run / projection transitions only.
[<RequireQualifiedAccess>]
module PromptAuthorityRun =

    /// Create the Authority Root profile for a proven physical message.
    ///
    /// Takes a PhysicalUserMessageId and promotes it, because PROMPT-005 allows
    /// promotion only once `PhysicalAccepted` is established. The type signature
    /// therefore marks this as the one place that transition happens; a
    /// TransportReceipt cannot reach it at all.
    let createAuthorityRoot
        (sha256: string -> string)
        (runtimeId: RuntimeId)
        (sessionId: SessionId)
        (rootKind: PromptAuthority.RootAuthorityKind)
        (physicalMessageId: PhysicalUserMessageId)
        (identitySeed: PromptAuthority.IdentitySeed)
        : Result<PromptAuthority.AuthorityExecutionProfile, string> =
        let authorityRoot = PhysicalUserMessageId.promoteToAuthorityRoot physicalMessageId

        PromptAuthority.createAuthorityExecutionProfileFromSeed
            sessionId
            (PromptAuthority.stableLogicalRunId sha256 runtimeId sessionId authorityRoot)
            authorityRoot
            rootKind
            identitySeed

    let private requireInheritedOwnerIdentity identitySeed =
        match identitySeed with
        | PromptAuthority.IdentitySeed.RootSelection _ ->
            Error "AgentOwnerRoot requires an inherited owner identity seed"
        | PromptAuthority.IdentitySeed.InheritedFromOwner _ ->
            Ok(PromptAuthority.identitySeedParticipantIdentity identitySeed)

    let private admitPublicRole participantIdentity =
        match ParticipantIdentity.role participantIdentity with
        | None -> Error "public authority requires an office identity"
        | Some _ -> Ok participantIdentity

    /// Claim a prompt that will become a new Authority Root (PROMPT-004
    /// AgentOwnerRoot).
    ///
    /// No LogicalRunId yet: the run's id derives from the physical message,
    /// which does not exist until the Host accepts. `None` says exactly that.
    let claimAgentOwnerRoot
        (key: PromptKey)
        (sessionId: SessionId)
        (payloadDigest: string)
        (identitySeed: PromptAuthority.IdentitySeed)
        : Result<PromptAuthority.PromptClaim, string> =
        requireInheritedOwnerIdentity identitySeed
        |> Result.bind admitPublicRole
        |> Result.map (fun _ ->
            { PromptKey = key
              SessionId = sessionId
              Origin = PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot
              LogicalRunId = None
              AuthorityRootUserMessageId = None
              IdentitySeed = identitySeed
              PayloadDigest = payloadDigest
              Receipt = None
              ClaimedAtRuntimeStartCount = 0 })

    /// Claim a continuation (PROMPT-003). It inherits the run and the root, and
    /// preserves the authority profile's identity seed.
    let claimContinuation
        (key: PromptKey)
        (sessionId: SessionId)
        (continuation: PromptAuthority.ContinuationKind)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        (payloadDigest: string)
        : PromptAuthority.PromptClaim =
        { PromptKey = key
          SessionId = sessionId
          Origin = PromptAuthority.PromptOrigin.Continuation continuation
          LogicalRunId = Some profile.LogicalRunId
          AuthorityRootUserMessageId = Some profile.AuthorityRootUserMessageId
          IdentitySeed = profile.IdentitySeed
          PayloadDigest = payloadDigest
          Receipt = None
          ClaimedAtRuntimeStartCount = 0 }

    let private dispatchRequired what (value: 'T option) =
        value
        |> Option.defaultWith (fun () -> failwithf "wanxiangli/dispatch asked for %s that the event lacks" what)

    let private landedDispatch
        (physicalMessageId: PhysicalUserMessageId option)
        (claim: PromptAuthority.PromptClaim)
        : PromptAuthority.AcceptedDispatch =
        { PromptKey = claim.PromptKey
          SessionId = claim.SessionId
          Origin = claim.Origin
          IdentitySeed = claim.IdentitySeed
          PayloadDigest = claim.PayloadDigest
          PhysicalUserMessageId =
            physicalMessageId
            |> Option.defaultWith (fun () -> failwith "wanxiangli/dispatch landed a claim without a physical id") }

    let private recordContinuationEdit
        (physicalMessageId: PhysicalUserMessageId option)
        (claim: PromptAuthority.PromptClaim)
        (state: PromptAuthority.PromptAuthorityProjection)
        : PromptAuthority.PromptAuthorityProjection =
        match claim.Origin with
        | PromptAuthority.PromptOrigin.Continuation kind ->
            { state with
                AcceptedContinuationIds =
                    Map.add (dispatchRequired "a physical id" physicalMessageId) kind state.AcceptedContinuationIds }
        | _ -> failwith "wanxiangli/dispatch recorded a continuation for a non-continuation claim"

    let private applyClaimEdit
        (key: PromptKey)
        (receipt: TransportReceipt option)
        (physicalMessageId: PhysicalUserMessageId option)
        (claim: PromptAuthority.PromptClaim)
        occasion
        (state: PromptAuthority.PromptAuthorityProjection)
        edit
        : PromptAuthority.PromptAuthorityProjection =
        match edit with
        | DispatchLaw.Edit.RecordReceipt ->
            { state with
                PendingClaims =
                    Map.add
                        key
                        { claim with
                            Receipt = Some(dispatchRequired "a receipt" receipt) }
                        state.PendingClaims }
        | DispatchLaw.Edit.DropPending ->
            { state with
                PendingClaims = Map.remove key state.PendingClaims }
        | DispatchLaw.Edit.LandOccasion ->
            { state with
                AcceptedDispatches = Map.add occasion (landedDispatch physicalMessageId claim) state.AcceptedDispatches }
        | DispatchLaw.Edit.LandExact ->
            let physical = dispatchRequired "a physical id" physicalMessageId

            { state with
                PhysicalLandings = Map.add physical (landedDispatch physicalMessageId claim) state.PhysicalLandings }
        | DispatchLaw.Edit.RecordContinuation -> recordContinuationEdit physicalMessageId claim state
        | DispatchLaw.Edit.ClearOccasion ->
            { state with
                AcceptedDispatches = Map.remove occasion state.AcceptedDispatches }

    /// Apply the ledger edits `wanxiangli/dispatch` settles for one claim event
    /// (CUT-11). A key that is not pending (already landed, abandoned or never
    /// claimed) gets no edits: a landing never regresses and a duplicate receipt
    /// never dispatches again.
    let private settleClaim
        (event: string)
        (key: PromptKey)
        (receipt: TransportReceipt option)
        (physicalMessageId: PhysicalUserMessageId option)
        (projection: PromptAuthority.PromptAuthorityProjection)
        =
        let pending = Map.tryFind key projection.PendingClaims

        let continuation =
            match pending with
            | Some { Origin = PromptAuthority.PromptOrigin.Continuation _ } -> true
            | _ -> false

        match pending with
        | None ->
            DispatchLaw.settle event false false
            |> List.iter (fun _ -> failwith "wanxiangli/dispatch edited a claim that is not pending")

            projection
        | Some claim ->
            let occasion =
                PromptAuthority.acceptedDispatchKey claim.SessionId claim.PayloadDigest

            DispatchLaw.settle event true continuation
            |> List.fold (applyClaimEdit key receipt physicalMessageId claim occasion) projection

    /// Run boundary (a new root or an exact closure): `wanxiangli/dispatch`
    /// reset decides which dispatch resources survive. Run-scoped claims,
    /// counters, continuation map and questions end with the run; root claims
    /// without a logical run and this session's root counters stay, so a root
    /// PromptKey never repeats.
    let private resetRunResources (sessionId: SessionId) (projection: PromptAuthority.PromptAuthorityProjection) =
        let keepClaims, keepCounters, clearContinuations, clearQuestions =
            DispatchLaw.reset
                (SessionId.value sessionId)
                (projection.PendingClaims
                 |> Map.toList
                 |> List.map (fun (key, claim) ->
                     PromptKey.value key, claim.LogicalRunId |> Option.map LogicalRunId.value))
                (projection.ClaimSequences
                 |> Map.toList
                 |> List.map (fun (scope, counter) ->
                     scope, SessionId.value counter.SessionId, counter.LogicalRunId |> Option.map LogicalRunId.value))

        { projection with
            PendingClaims =
                projection.PendingClaims
                |> Map.filter (fun key _ -> keepClaims.Contains(PromptKey.value key))
            ClaimSequences =
                projection.ClaimSequences
                |> Map.filter (fun scope _ -> keepCounters.Contains scope)
            AcceptedContinuationIds =
                if clearContinuations then
                    Map.empty
                else
                    projection.AcceptedContinuationIds
            UserQuestions =
                if clearQuestions then
                    Map.empty
                else
                    projection.UserQuestions }

    /// PROMPT-005 `Submitted`: the Host call returned a transport receipt.
    ///
    /// Recorded on the claim rather than discarded. PROMPT-011 has to tell "the Host
    /// accepted something we cannot locate" from "we never got that far", and those
    /// are different operator diagnoses even though both stay pending.
    let submitClaim
        (key: PromptKey)
        (receipt: TransportReceipt)
        (projection: PromptAuthority.PromptAuthorityProjection)
        =
        settleClaim "Submit" key (Some receipt) None projection

    /// An exact duplicate root is idempotent. A fresh root is admitted only
    /// after the prior logical run has released the active binding.
    type AuthorityRegistrationRejection =
        | ActiveRunIdentityConflict of
            active: PromptAuthority.AuthorityExecutionProfile *
            requested: PromptAuthority.AuthorityExecutionProfile

    let describeRegistrationRejection =
        function
        | ActiveRunIdentityConflict(active, requested) ->
            sprintf
                "active logical run must close before replacement: active=%s requested=%s"
                (LogicalRunId.value active.LogicalRunId)
                (LogicalRunId.value requested.LogicalRunId)

    let resolveAuthorityProfile
        (requested: PromptAuthority.AuthorityExecutionProfile)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : Result<PromptAuthority.AuthorityExecutionProfile, AuthorityRegistrationRejection> =
        // An exact duplicate root, or a durable replay of the same physical
        // root, keeps the active run; anything else conflicts (wanxiangli/authority).
        match projection.ActiveLogicalRun, AuthorityLaw.register projection.ActiveLogicalRun requested with
        | None, "admit" -> Ok requested
        | Some active, "replay" -> Ok active
        | Some active, "conflict" -> Error(ActiveRunIdentityConflict(active, requested))
        | _, decision -> failwithf "wanxiangli/authority returned an inconsistent registration %s" decision

    let registerAuthority
        (profile: PromptAuthority.AuthorityExecutionProfile)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : Result<PromptAuthority.PromptAuthorityProjection, AuthorityRegistrationRejection> =
        resolveAuthorityProfile profile projection
        |> Result.map (fun canonical ->
            match projection.ActiveLogicalRun with
            | Some _ -> projection
            | None ->
                { resetRunResources canonical.SessionId projection with
                    LastAuthorityProfile = Some canonical
                    ActiveLogicalRun = Some canonical })

    /// Apply the library's closure decision to the projection. Only `close`
    /// changes state; a repeated closure is idempotent.
    let private settleClosure
        (closeRun:
            PromptAuthority.AuthorityExecutionProfile -> Result<PromptAuthority.PromptAuthorityProjection, string>)
        (decision: string)
        (logicalRunId: LogicalRunId)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : Result<PromptAuthority.PromptAuthorityProjection, string> =
        match decision, projection.ActiveLogicalRun with
        | "close", Some active -> closeRun active
        | "already-closed", None -> Ok projection
        | "mismatch", Some active ->
            Error(
                sprintf
                    "logical-run close mismatch: active=%s requested=%s"
                    (LogicalRunId.value active.LogicalRunId)
                    (LogicalRunId.value logicalRunId)
            )
        | "no-authority", None ->
            Error(sprintf "logical-run close has no matching authority: %s" (LogicalRunId.value logicalRunId))
        | "not-child-work", Some _ -> Error "child-work closure requires a non-Manager AgentOwnerRoot authority"
        | _ -> failwithf "wanxiangli/authority returned an inconsistent closure %s" decision

    /// Close exactly the active Logical Run named by durable terminal evidence.
    /// Run-scoped continuation resources are discarded; LastAuthorityProfile is
    /// intentionally retained as history but may never substitute for ActiveLogicalRun.
    let closeAuthority
        (logicalRunId: LogicalRunId)
        (authorityRoot: AuthorityRootUserMessageId)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : Result<PromptAuthority.PromptAuthorityProjection, string> =
        let closeRun (active: PromptAuthority.AuthorityExecutionProfile) =
            Ok
                { resetRunResources active.SessionId projection with
                    ActiveLogicalRun = None }

        settleClosure
            closeRun
            (AuthorityLaw.close projection.ActiveLogicalRun projection.LastAuthorityProfile logicalRunId authorityRoot)
            logicalRunId
            projection

    let closeCompletedAgentOwnerChildWork
        (logicalRunId: LogicalRunId)
        (authorityRoot: AuthorityRootUserMessageId)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : Result<PromptAuthority.PromptAuthorityProjection, string> =
        match
            AuthorityLaw.closeChildWork
                projection.ActiveLogicalRun
                projection.LastAuthorityProfile
                logicalRunId
                authorityRoot
        with
        | "not-child-work" when projection.ActiveLogicalRun.IsSome ->
            Error "child-work closure requires a non-Manager AgentOwnerRoot authority"
        | _ -> closeAuthority logicalRunId authorityRoot projection

    /// Durable replay of `AuthorityLogicalRunClosed`.
    ///
    /// The verdict is the same exact-run law as `closeAuthority`
    /// (wanxiangli/authority), but a replay after the active run is gone is
    /// still judged: the durable acceptance contract and the durable closure
    /// record say whether this closure is the one that already happened
    /// (idempotent) or a different one (conflict, fail-closed).
    ///
    /// Silently ignoring a closure that names an unknown run is not allowed:
    /// that would let a conflicting closure look like a duplicate and release
    /// the identity binding the run still owns.
    ///
    /// Record the closure that has just been applied, so a later replay can
    /// tell it from a conflicting one.
    let private recordClosure
        (projection: PromptAuthority.PromptAuthorityProjection)
        (logicalRunId: LogicalRunId)
        (rootKind: string)
        (closureWitness: string)
        : PromptAuthority.PromptAuthorityProjection =
        { projection with
            ClosedRuns =
                Map.add
                    logicalRunId
                    ({ RootKind = rootKind
                       ClosureWitness = closureWitness }
                    : PromptAuthority.AuthorityClosureRecord)
                    projection.ClosedRuns }

    /// interaction-authority-018: the canonical closure witness of a completed
    /// Manager life (its delivered S4 retirement), or `None` when the active
    /// root does not expect a Manager-life closure.
    let managerLifeClosureWitness
        (expectedKind: string)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        (retirementId: string)
        : string option =
        AuthorityLaw.managerLifeWitness expectedKind (SessionId.value profile.SessionId) retirementId

    /// interaction-authority-018: the canonical closure witness of a completed
    /// managed HumanRoot run, a child work, an attached work or an internal
    /// leaf. `None` when the accepted root does not expect that closure kind.
    let managedRunClosureWitness (expectedKind: string) (logicalRunId: string) (factId: string) =
        AuthorityLaw.managedRunWitness expectedKind logicalRunId factId

    let childWorkClosureWitness
        (expectedKind: string)
        (ownerLogicalRunId: string)
        (childLogicalRunId: string)
        (factId: string)
        =
        AuthorityLaw.childWorkWitness expectedKind ownerLogicalRunId childLogicalRunId factId

    let attachedWorkClosureWitness
        (expectedKind: string)
        (ownerLogicalRunId: string)
        (childLogicalRunId: string)
        (attachmentKind: string)
        (associationGeneration: string)
        (factId: string)
        =
        AuthorityLaw.attachedWorkWitness
            expectedKind
            ownerLogicalRunId
            childLogicalRunId
            attachmentKind
            associationGeneration
            factId

    let internalLeafClosureWitness
        (expectedKind: string)
        (ownerLogicalRunId: string)
        (leafLogicalRunId: string)
        (decisionOrTransactionId: string)
        (factId: string)
        =
        AuthorityLaw.internalLeafWitness expectedKind ownerLogicalRunId leafLogicalRunId decisionOrTransactionId factId

    /// interaction-authority-018: the unique durable source terminal a closure
    /// of this kind may cite.
    ///
    /// Exactly one source must match the accepted root's identity: no source
    /// means the closure has no durable ground, and two sources mean the
    /// closure would be ambiguous. Both are refused rather than resolved by
    /// taking the first one.
    let uniqueTerminalSource
        (sources: PromptAuthority.AuthorityTerminalSource list)
        (sourceKind: string)
        (ownerLogicalRunId: string option)
        (childLogicalRunId: string)
        : Result<string * PromptAuthority.AuthorityTerminalSource, string> =
        let matches =
            sources
            |> List.filter (fun source ->
                source.SourceKind = sourceKind
                && source.ChildLogicalRunId = Some childLogicalRunId
                && source.OwnerLogicalRunId = ownerLogicalRunId)

        match sourceKind, ownerLogicalRunId, matches with
        | "HumanRootManagedRun", None, [ source ] -> Ok("", source)
        | ("AgentOwnerChildWork" | "AgentOwnerAttachedWork" | "AgentOwnerInternalLeaf"), Some owner, [ source ] when
            not (System.String.IsNullOrWhiteSpace owner)
            ->
            Ok(owner, source)
        | _, _, [] ->
            Error(
                sprintf "no durable %s source terminal of run %s matches the accepted root" sourceKind childLogicalRunId
            )
        | _, _, [ _ ] ->
            Error(sprintf "the %s source terminal of run %s has an invalid owner identity" sourceKind childLogicalRunId)
        | _ ->
            Error(
                sprintf
                    "%s source terminals of run %s are ambiguous; the closure cannot name one"
                    sourceKind
                    childLogicalRunId
            )

    /// interaction-authority-018: the closure witness of one accepted root,
    /// built from the unique durable source terminal of its closure kind.
    ///
    /// `None` when the root expects a kind that has no durable source, when
    /// the source does not match the root exactly, or when more than one
    /// source matches. A closure is never written from a guessed source.
    let closureWitnessOfSource
        (expectedKind: string)
        (sources: PromptAuthority.AuthorityTerminalSource list)
        (ownerLogicalRunId: string option)
        (childLogicalRunId: string)
        : string option =
        let managedWitness (owner: string) (source: PromptAuthority.AuthorityTerminalSource) =
            managedRunClosureWitness expectedKind childLogicalRunId source.FactId

        let childWitness (owner: string) (source: PromptAuthority.AuthorityTerminalSource) =
            childWorkClosureWitness expectedKind owner childLogicalRunId source.FactId

        let attachedWitness (owner: string) (source: PromptAuthority.AuthorityTerminalSource) =
            source.AttachmentKind
            |> Option.bind (fun attachmentKind ->
                source.AssociationGeneration
                |> Option.map (fun generation ->
                    attachedWorkClosureWitness
                        expectedKind
                        owner
                        childLogicalRunId
                        attachmentKind
                        generation
                        source.FactId))
            |> Option.flatten

        let internalLeafWitnessFor (owner: string) (source: PromptAuthority.AuthorityTerminalSource) =
            source.DecisionOrTransactionId
            |> Option.map (fun decision ->
                internalLeafClosureWitness expectedKind owner childLogicalRunId decision source.FactId)
            |> Option.flatten

        let noWitness (owner: string) (source: PromptAuthority.AuthorityTerminalSource) : string option = None

        let sourceKind, witness =
            match expectedKind with
            | "HumanRootManagedRun" -> "HumanRootManagedRun", managedWitness
            | "AgentOwnerChildWork" -> "AgentOwnerChildWork", childWitness
            | "AgentOwnerAttachedWork" -> "AgentOwnerAttachedWork", attachedWitness
            | "AgentOwnerInternalLeaf" -> "AgentOwnerInternalLeaf", internalLeafWitnessFor
            | _ -> "", noWitness

        uniqueTerminalSource sources sourceKind ownerLogicalRunId childLogicalRunId
        |> Result.toOption
        |> Option.bind (fun (owner, source) -> witness owner source)

    let private validateClosureSource
        (projection: PromptAuthority.PromptAuthorityProjection)
        (logicalRunId: LogicalRunId)
        (contract: PromptAuthority.AcceptedRootClosure)
        (closureWitness: string)
        : Result<unit, string> =
        if contract.ExpectedClosureKind = "HumanRootManagerLife" then
            Ok()
        else
            let sources =
                projection.TerminalSources
                |> Map.tryFind logicalRunId
                |> Option.defaultValue Map.empty
                |> Map.toList
                |> List.map snd

            match
                closureWitnessOfSource
                    contract.ExpectedClosureKind
                    sources
                    contract.OwnerLogicalRunId
                    (LogicalRunId.value logicalRunId)
            with
            | Some expected when expected = closureWitness -> Ok()
            | _ ->
                Error "AuthorityLogicalRunClosed does not cite the unique durable source terminal of its accepted root"

    let replayLogicalRunClosed
        (logicalRunId: LogicalRunId)
        (authorityRoot: AuthorityRootUserMessageId)
        (rootKind: string)
        (closureWitness: string)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : Result<PromptAuthority.PromptAuthorityProjection option, string> =
        let contract = Map.tryFind logicalRunId projection.AcceptedRootClosures

        let closeActive (active: PromptAuthority.AuthorityExecutionProfile) =
            match contract with
            | None ->
                Error(
                    sprintf
                        "AuthorityLogicalRunClosed names run %s, whose accepted root recorded no closure kind"
                        (LogicalRunId.value logicalRunId)
                )
            | Some accepted ->
                validateClosureSource projection logicalRunId accepted closureWitness
                |> Result.bind (fun () ->
                    AuthorityLaw.admitClosure
                        accepted
                        (SessionId.value active.SessionId)
                        (LogicalRunId.value logicalRunId)
                        rootKind
                        closureWitness)
                |> Result.bind (fun () -> closeAuthority logicalRunId authorityRoot projection)
                |> Result.map (fun closed -> Some(recordClosure closed logicalRunId rootKind closureWitness))

        let decision =
            AuthorityLaw.close projection.ActiveLogicalRun projection.LastAuthorityProfile logicalRunId authorityRoot

        match Map.tryFind logicalRunId projection.ClosedRuns, decision, projection.ActiveLogicalRun with
        | Some recorded, _, _ when
            recorded.RootKind = rootKind
            && recorded.ClosureWitness = closureWitness
            && (contract |> Option.map (fun accepted -> accepted.AuthorityRootUserMessageId)) = Some authorityRoot
            ->
            Ok None
        | Some _, _, _ ->
            Error(
                sprintf
                    "AuthorityLogicalRunClosed conflicts with the recorded closure of run %s"
                    (LogicalRunId.value logicalRunId)
            )
        | None, "close", Some active -> closeActive active
        | None, ("already-closed" | "no-authority"), None ->
            Error(
                sprintf
                    "AuthorityLogicalRunClosed names run %s, which holds no closure and no authority"
                    (LogicalRunId.value logicalRunId)
            )
        | None, "mismatch", Some _ -> Error "AuthorityLogicalRunClosed conflicts with the active logical run"
        | None, _, _ -> failwithf "wanxiangli/authority returned an inconsistent closure %s" decision

    /// Register a claim and consume its ClaimSequence.
    ///
    /// PROMPT-011: the sequence must advance whether or not the claim later
    /// resolves. Advancing only on success would let an abandoned dispatch and its
    /// retry derive the same PromptKey, so recovery would find one metadata anchor
    /// for two distinct logical acts.
    let registerClaim (claim: PromptAuthority.PromptClaim) (projection: PromptAuthority.PromptAuthorityProjection) =
        let scope =
            PromptAuthority.claimScopeDigest claim.SessionId claim.LogicalRunId claim.Origin claim.PayloadDigest

        { projection with
            PendingClaims = Map.add claim.PromptKey claim projection.PendingClaims
            ClaimSequences =
                Map.add
                    scope
                    { SessionId = claim.SessionId
                      LogicalRunId = claim.LogicalRunId
                      Count = PromptAuthority.nextClaimSequence scope projection }
                    projection.ClaimSequences }

    /// PROMPT-005 `PhysicalAccepted`: a real Host message id resolved a claim.
    ///
    /// The landing is kept as typed `AcceptedDispatch` evidence keyed by
    /// (session, payload digest) — business layers ask `dispatchStatusFor`
    /// instead of scanning the Journal. Only continuations additionally enter
    /// `AcceptedContinuationIds`; REVIEW-003 forbids using that map as review
    /// confirmation. Which edits apply is `wanxiangli/dispatch` settle.
    let acceptClaim
        (key: PromptKey)
        (physicalMessageId: PhysicalUserMessageId)
        (projection: PromptAuthority.PromptAuthorityProjection)
        =
        settleClaim "Accept" key None (Some physicalMessageId) projection

    /// PROMPT-005 `Abandoned`. Must not change the Active Logical Run.
    ///
    /// An explicit abandon of a still-pending claim also drops that payload's
    /// occasion slot, so a later reentry may claim the same logical dispatch
    /// again. The exact physical landings are facts and stay untouched.
    let abandonClaim (key: PromptKey) (projection: PromptAuthority.PromptAuthorityProjection) =
        settleClaim "Abandon" key None None projection

    /// PROMPT-009 resolution order, evaluated top to bottom:
    ///
    ///   accepted physical message id
    ///   → claimed PromptKey
    ///   → Host compaction / synthetic
    ///   → registered AgentOwnerRoot
    ///   → UnknownOrigin
    ///
    /// HumanRoot is absent on purpose: PROMPT-004 requires proven external
    /// acceptance carrying an explicit agent, which this pure function cannot
    /// observe. Anything unproven lands on UnknownOrigin and fails closed.
    let resolveKnownOrigin
        (physicalMessageId: PhysicalUserMessageId)
        (promptKey: PromptKey option)
        (hostCompaction: bool)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : PromptAuthority.PromptOrigin =
        let pendingOrigin =
            promptKey
            |> Option.bind (fun key -> Map.tryFind key projection.PendingClaims)
            |> Option.map (fun claim -> claim.Origin)

        AuthorityLaw.resolveOrigin
            (Map.tryFind physicalMessageId projection.AcceptedContinuationIds)
            pendingOrigin
            promptKey.IsSome
            hostCompaction
            (projection.ActiveLogicalRun |> Option.map (fun profile -> profile.AuthorityKind))
