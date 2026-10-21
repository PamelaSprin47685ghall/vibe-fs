namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// PERSIST-008: every lookup is keyed by SessionId. Nothing scans all
/// sessions.
module PromptAuthorityProjectionQueries =

    let projectionFor (sessionId: SessionId) (agentProjections: AgentProjectionSet) =
        Map.tryFind sessionId agentProjections.Sessions
        |> Option.bind (fun session -> session.PromptAuthority)

    let private profileOwner (sessionId: SessionId) (agentProjections: AgentProjectionSet) =
        FissionProjection.tryOwnerOfLane sessionId agentProjections.Fission
        |> Option.defaultValue sessionId

    /// The durable Fission owner of a lane session.
    ///
    /// This is the one place that knows a lane inherits the owner's authority:
    /// the membership is durable (`FissionAdmitted` folded it), so a restarted
    /// process resolves the same owner as a live one.
    let laneOwnerSessionId (sessionId: SessionId) (agentProjections: AgentProjectionSet) =
        FissionProjection.tryOwnerOfLane sessionId agentProjections.Fission

    /// The physical ids this session accepted as continuations of its own
    /// dispatch claims.
    ///
    /// A Fission lane never owns an authority root; its only durable evidence
    /// of the owner's logical run is the continuation it accepted. These ids
    /// are the lane's own physical cursor candidates.
    let ownContinuationPhysicals (sessionId: SessionId) (agentProjections: AgentProjectionSet) =
        projectionFor sessionId agentProjections
        |> Option.map (fun authority -> authority.AcceptedContinuationIds |> Map.toList |> List.map fst)
        |> Option.defaultValue []

    /// The owner's active logical run for a lane session, when the lane is a
    /// durable member of a Fission group whose owner differs from the lane.
    ///
    /// `None` for an ordinary session, and for a lane whose owner has no
    /// active run: an ordinary session must never inherit a root.
    let inheritedOwnerProfile
        (sessionId: SessionId)
        (agentProjections: AgentProjectionSet)
        : PromptAuthority.AuthorityExecutionProfile option =
        match laneOwnerSessionId sessionId agentProjections with
        | Some owner when owner <> sessionId ->
            projectionFor owner agentProjections
            |> Option.bind (fun authority -> authority.ActiveLogicalRun)
        | _ -> None

    let activeProfile (sessionId: SessionId) (agentProjections: AgentProjectionSet) =
        projectionFor (profileOwner sessionId agentProjections) agentProjections
        |> Option.bind (fun authority -> authority.ActiveLogicalRun)

    let lastAuthorityProfile (sessionId: SessionId) (agentProjections: AgentProjectionSet) =
        projectionFor (profileOwner sessionId agentProjections) agentProjections
        |> Option.bind (fun authority -> authority.LastAuthorityProfile)

    let pendingClaim (sessionId: SessionId) (promptKey: PromptKey) (agentProjections: AgentProjectionSet) =
        projectionFor sessionId agentProjections
        |> Option.bind (fun authority -> Map.tryFind promptKey authority.PendingClaims)

    /// The durable outcome of one logical dispatch, for resend admission.
    ///
    /// REVIEW-013/018 (process-review assignment reentry): `Accepted` means the
    /// payload physically landed and must not be sent again; `Pending` means a
    /// claim exists whose outcome is undetermined — recovery owns it, never a
    /// blind resend; `Dispatchable` means no logical dispatch (or an explicitly
    /// Abandoned one), so a new claim is allowed. Read from projection evidence;
    /// the caller never scans the Journal.
    [<RequireQualifiedAccess>]
    type DispatchStatus =
        | Accepted of evidence: PromptAuthority.AcceptedDispatch
        | Pending
        | Dispatchable

    let pendingDispatchClaim
        (sessionId: SessionId)
        (payloadDigest: string)
        (agentProjections: AgentProjectionSet)
        : PromptAuthority.PromptClaim option =
        projectionFor sessionId agentProjections
        |> Option.bind (fun authority ->
            authority.PendingClaims
            |> Seq.tryPick (fun (KeyValue(_, claim)) ->
                if claim.SessionId = sessionId && claim.PayloadDigest = payloadDigest then
                    Some claim
                else
                    None))

    /// dispatch-protocol-006: the exact dispatch that landed as this physical
    /// message. A same-payload landing on another physical message never
    /// shadows it.
    let physicalLanding
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (agentProjections: AgentProjectionSet)
        : PromptAuthority.AcceptedDispatch option =
        projectionFor sessionId agentProjections
        |> Option.bind (fun authority -> Map.tryFind physicalUserMessageId authority.PhysicalLandings)

    let dispatchStatusFor
        (sessionId: SessionId)
        (payloadDigest: string)
        (agentProjections: AgentProjectionSet)
        : DispatchStatus =
        let key = PromptAuthority.acceptedDispatchKey sessionId payloadDigest

        match projectionFor sessionId agentProjections with
        | Some authority when Map.containsKey key authority.AcceptedDispatches ->
            DispatchStatus.Accepted(Map.find key authority.AcceptedDispatches)
        | _ when pendingDispatchClaim sessionId payloadDigest agentProjections |> Option.isSome ->
            DispatchStatus.Pending
        | _ -> DispatchStatus.Dispatchable

    let issueCurrentOwnerIdentitySeed
        (agentProjections: AgentProjectionSet)
        (ownerSessionId: SessionId)
        (childAgent: string)
        : Result<PromptAuthority.IdentitySeed, string> =
        match activeProfile ownerSessionId agentProjections with
        | None -> Error "AgentOwnerRoot identity seed requires the owner's active durable Logical Run"
        | Some ownerProfile ->
            PromptAuthority.issueInheritedIdentitySeed childAgent ownerProfile
            |> Result.mapError (sprintf "Invalid inherited participant identity: %A")
            |> Result.bind (fun seed ->
                PromptAuthority.validateInheritedIdentitySeed ownerProfile seed
                |> Result.mapError (sprintf "Invalid owner identity witness: %A")
                |> Result.map (fun _ -> seed))
