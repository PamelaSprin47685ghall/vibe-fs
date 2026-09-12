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

    let acceptedDispatchForPhysicalMessage
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (agentProjections: AgentProjectionSet)
        : PromptAuthority.AcceptedDispatch option =
        projectionFor sessionId agentProjections
        |> Option.bind (fun authority ->
            authority.AcceptedDispatches
            |> Seq.tryPick (fun (KeyValue(_, dispatch)) ->
                if dispatch.PhysicalUserMessageId = physicalUserMessageId then
                    Some dispatch
                else
                    None))

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
