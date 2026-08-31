namespace Wanxiangshu.Participant.Provider.Attempt

open Fable.Core.JsInterop
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Persona

/// JS-native proof surface for PROMPT-008 / ENF-001 / ENF-003 / ENF-004.
///
/// The caller supplies only the role, tier and physical request kind. The
/// AttemptPlanner remains the sole constructor of the profile; this boundary
/// translates its derived fields to plain strings and arrays.
module AttemptPlannerSurface =

    let private stringOf (value: obj) : string =
        if isNull value then "" else string value

    let private permissionLabel (permission: ToolPermission) : string =
        match permission with
        | ToolPermission.Fork -> "Fork"
        | ToolPermission.Join -> "Join"
        | ToolPermission.Horizon -> "Horizon"
        | ToolPermission.TodoWrite -> "TodoWrite"
        | ToolPermission.Fission -> "Fission"
        | ToolPermission.Read -> "Read"
        | ToolPermission.Write -> "Write"
        | ToolPermission.Edit -> "Edit"
        | ToolPermission.Glob -> "Glob"
        | ToolPermission.Grep -> "Grep"
        | ToolPermission.Move -> "Move"
        | ToolPermission.Remove -> "Remove"
        | ToolPermission.Inspect -> "Inspect"
        | ToolPermission.Behavior -> "Behavior"
        | ToolPermission.Exec -> "Exec"
        | ToolPermission.Pty -> "Pty"
        | ToolPermission.Network -> "Network"
        | ToolPermission.Judge -> "Judge"
        | ToolPermission.Chronicle -> "Chronicle"
        | ToolPermission.Fetch -> "Fetch"
        | ToolPermission.Finality -> "Finality"
        | ToolPermission.BashHoneypot -> "BashHoneypot"
        | ToolPermission.Sphinx -> "Sphinx"

    let private requestKindOf (label: string) : ProviderRequestKind option =
        match label.ToLowerInvariant() with
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

    let ordinaryRequestPurpose (origin: string) (bloggerPurpose: string) =
        let promptOrigin =
            if origin = "InteractionRepair" then
                PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.InteractionRepair
            else
                PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.HumanRoot

        let requestKind =
            match bloggerPurpose with
            | "main" -> ProviderRequestKind.BloggerMain
            | "squash" -> ProviderRequestKind.BloggerSquash
            | _ -> AttemptPlanner.ordinaryRequestKind promptOrigin

        ProviderRequestKind.label requestKind

    let private profileOf (role: Role) (tier: AgentTier) (requestKind: ProviderRequestKind) =
        ParticipantIdentity.resolveAtRoot (Roles.managedAgentName tier role)
        |> Result.mapError (fun error -> sprintf "invalid participant identity: %A" error)
        |> Result.bind (fun participantIdentity ->
            PromptAuthority.createAuthorityExecutionProfile
                (SessionId.create "surface-session")
                (LogicalRunId.create "surface-run")
                (AuthorityRootUserMessageId.create "surface-root")
                PromptAuthority.RootAuthorityKind.HumanRoot
                participantIdentity)
        |> Result.map (fun authority ->
            AttemptPlanner.plan
                authority
                AgentPairCursor.initial
                (PhysicalUserMessageId.create "surface-user")
                (ProviderRunIdentity.create "surface-provider-run")
                (PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.HumanRoot)
                requestKind
                RecoveryOpportunity.OrdinaryAttempt
                (fun () -> Error NoCandidateReason.NoCoverage))

    let private participantIdentityToJs (identity: ParticipantIdentityEvidence) : obj =
        box
            {| selectedAgent = ParticipantIdentity.selectedAgent identity
               peerAgent = ParticipantIdentity.peerAgent identity
               canonicalRole = ParticipantIdentity.roleLabel identity
               selectedTier = ParticipantIdentity.initialTier identity |> Roles.wireTierLabel
               persona = ParticipantIdentity.persona identity
               personaCatalogVersion = ParticipantIdentity.personaCatalogVersion identity
               origin =
                match ParticipantIdentity.origin identity with
                | PersonaOrigin.ResolvedAtRoot -> "ResolvedAtRoot"
                | PersonaOrigin.InheritedFromOwner -> "InheritedFromOwner" |}

    /// Build one derived profile from JSON-shaped input.
    /// `{ role, tier, kind }` are labels; unknown labels fail closed.
    let plan (input: obj) : obj =
        let roleLabel = stringOf input?role
        let tierLabel = stringOf input?tier
        let kindLabel = stringOf input?kind

        match Roles.tryParseRole roleLabel, Roles.tryParseTier tierLabel, requestKindOf kindLabel with
        | Some role, Some tier, Some requestKind ->
            match profileOf role tier requestKind with
            | Error error -> box {| ok = false; error = error |}
            | Ok planned ->
                let profile = planned.Profile

                box
                    {| ok = true
                       canonicalRole = Roles.roleLabel profile.CanonicalRole
                       participantIdentity = participantIdentityToJs profile.Authority.ParticipantIdentity
                       systemPromptId = SystemPromptId.value profile.SystemPromptId
                       toolCapabilities =
                        profile.ToolCapabilitySet
                        |> Set.toList
                        |> List.map permissionLabel
                        |> List.sort
                        |> List.toArray
                       requestKind = ProviderRequestKind.label profile.RequestKind |}
        | None, _, _ -> box {| ok = false; error = "unknown role" |}
        | _, None, _ -> box {| ok = false; error = "unknown tier" |}
        | _, _, None ->
            box
                {| ok = false
                   error = "unknown request kind" |}
