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
        | None -> Error "public authority participant identity cannot be Bookkeeper"
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
        match Map.tryFind key projection.PendingClaims with
        | None -> projection
        | Some claim ->
            { projection with
                PendingClaims = Map.add key { claim with Receipt = Some receipt } projection.PendingClaims }

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
                { projection with
                    LastAuthorityProfile = Some canonical
                    ActiveLogicalRun = Some canonical
                    UserQuestions = Map.empty
                    PendingClaims =
                        projection.PendingClaims
                        |> Map.filter (fun _ claim -> claim.LogicalRunId.IsNone)
                    AcceptedContinuationIds = Map.empty
                    ClaimSequences = PromptAuthority.rootClaimSequences canonical.SessionId projection })

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
                { projection with
                    ActiveLogicalRun = None
                    UserQuestions = Map.empty
                    PendingClaims =
                        projection.PendingClaims
                        |> Map.filter (fun _ claim -> claim.LogicalRunId.IsNone)
                    AcceptedContinuationIds = Map.empty
                    ClaimSequences = PromptAuthority.rootClaimSequences active.SessionId projection }

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

    /// Durable replay of `AuthorityLogicalRunClosed`. The verdict is the same
    /// exact-run law as `closeAuthority` (wanxiangli/authority); a replayed
    /// closure with no active run changes nothing, whether or not it names the
    /// last root, and a closure naming another run is rejected.
    let replayLogicalRunClosed
        (logicalRunId: LogicalRunId)
        (authorityRoot: AuthorityRootUserMessageId)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : Result<PromptAuthority.PromptAuthorityProjection option, string> =
        match
            AuthorityLaw.close projection.ActiveLogicalRun projection.LastAuthorityProfile logicalRunId authorityRoot,
            projection.ActiveLogicalRun
        with
        | "close", Some _ -> closeAuthority logicalRunId authorityRoot projection |> Result.map Some
        | ("already-closed" | "no-authority"), None -> Ok None
        | "mismatch", Some _ -> Error "AuthorityLogicalRunClosed conflicts with the active logical run"
        | decision, _ -> failwithf "wanxiangli/authority returned an inconsistent closure %s" decision

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

    let private acceptedContinuationEvidence
        (physicalMessageId: PhysicalUserMessageId)
        (origin: PromptAuthority.PromptOrigin)
        (projection: PromptAuthority.PromptAuthorityProjection)
        =
        match origin with
        | PromptAuthority.PromptOrigin.Continuation continuation ->
            { projection with
                AcceptedContinuationIds = Map.add physicalMessageId continuation projection.AcceptedContinuationIds }
        | PromptAuthority.PromptOrigin.AuthorityRoot _
        | PromptAuthority.PromptOrigin.HostInternal
        | PromptAuthority.PromptOrigin.UnknownOrigin -> projection

    /// PROMPT-005 `PhysicalAccepted`: a real Host message id resolved a claim.
    ///
    /// The landing is kept as typed `AcceptedDispatch` evidence keyed by
    /// (session, payload digest) — business layers ask `dispatchStatusFor`
    /// instead of scanning the Journal. Only continuations additionally enter
    /// `AcceptedContinuationIds`: an Authority Root claim resolving does not
    /// belong in a continuation map, and recording the root a continuation
    /// belonged to is deliberately gone — that map existed solely to let
    /// ReviewWitness guess confirmation from a shared root, which REVIEW-003
    /// forbids. Review confirmation now requires the provider input seal
    /// (REVIEW-010), so no substitute lookup is provided here.
    let acceptClaim
        (key: PromptKey)
        (physicalMessageId: PhysicalUserMessageId)
        (projection: PromptAuthority.PromptAuthorityProjection)
        =
        match Map.tryFind key projection.PendingClaims with
        | None -> projection
        | Some claim ->
            let withoutClaim = Map.remove key projection.PendingClaims

            let landed: PromptAuthority.AcceptedDispatch =
                { PromptKey = claim.PromptKey
                  SessionId = claim.SessionId
                  Origin = claim.Origin
                  IdentitySeed = claim.IdentitySeed
                  PayloadDigest = claim.PayloadDigest
                  PhysicalUserMessageId = physicalMessageId }

            let withEvidence =
                { projection with
                    PendingClaims = withoutClaim
                    AcceptedDispatches =
                        Map.add
                            (PromptAuthority.acceptedDispatchKey claim.SessionId claim.PayloadDigest)
                            landed
                            projection.AcceptedDispatches
                    PhysicalLandings = Map.add physicalMessageId landed projection.PhysicalLandings }

            acceptedContinuationEvidence physicalMessageId claim.Origin withEvidence

    /// PROMPT-005 `Abandoned`. Must not change the Active Logical Run.
    ///
    /// An explicit abandon of a still-pending claim also drops that payload's
    /// occasion slot, so a later reentry may claim the same logical dispatch
    /// again. The exact physical landings are facts and stay untouched.
    let abandonClaim (key: PromptKey) (projection: PromptAuthority.PromptAuthorityProjection) =
        match Map.tryFind key projection.PendingClaims with
        | None -> projection
        | Some claim ->
            { projection with
                PendingClaims = Map.remove key projection.PendingClaims
                AcceptedDispatches =
                    Map.remove
                        (PromptAuthority.acceptedDispatchKey claim.SessionId claim.PayloadDigest)
                        projection.AcceptedDispatches }

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
