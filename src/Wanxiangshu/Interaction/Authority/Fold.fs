namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Composition.Durable.ProjectionUpdate
open Wanxiangshu.Composition.Durable

module PromptFactFold =

    let private reject = FoldRejection.reject

    let private parseAuthorityKind value =
        match value with
        | "HumanRoot" -> Ok PromptAuthority.RootAuthorityKind.HumanRoot
        | "AgentOwnerRoot" -> Ok PromptAuthority.RootAuthorityKind.AgentOwnerRoot
        | unknown -> Error(sprintf "unknown authority root kind: %s" unknown)

    let private validateAuthorityRootAccepted schemaVersion authorityKind =
        if schemaVersion <> 2 then
            Error(sprintf "unsupported AuthorityRootAccepted schema version: %d" schemaVersion)
        else
            parseAuthorityKind authorityKind

    let private validateAcceptedIdentitySeed projection authorityKind seed =
        match authorityKind, PromptAuthority.identitySeedOwner seed with
        | PromptAuthority.RootAuthorityKind.HumanRoot, _ -> Ok()
        | PromptAuthority.RootAuthorityKind.AgentOwnerRoot, None ->
            Error PromptAuthority.IdentitySeedValidationError.ExpectedInheritedFromOwner
        | PromptAuthority.RootAuthorityKind.AgentOwnerRoot, Some(ownerSessionId, _, _) ->
            AgentProjection.tryFind ownerSessionId projection
            |> Option.bind (fun session -> session.PromptAuthority)
            |> Option.bind (fun authority -> authority.ActiveLogicalRun)
            |> fun activeOwner ->
                PromptAuthority.validateInheritedIdentitySeedAgainstActiveOwner activeOwner seed
                |> Result.map ignore

    let private admitReviewRequirement
        (authorityKind: PromptAuthority.RootAuthorityKind)
        (payload: AuthorityRootAcceptedPayload)
        (projection: AgentProjectionSet)
        : AgentProjectionSet =
        match authorityKind with
        | PromptAuthority.RootAuthorityKind.HumanRoot ->
            updateRequirements
                payload.SessionId
                (ReviewRequirementProjection.addRequirement payload.SessionId payload.AuthorityRootUserMessageId)
                projection
        | PromptAuthority.RootAuthorityKind.AgentOwnerRoot -> projection

    let private classifyClaimOrigin (continuationKind: string) : PromptAuthority.PromptOrigin option =
        if continuationKind = "AgentOwnerRoot" then
            Some(PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot)
        else
            PromptAuthority.tryParseContinuationKind continuationKind
            |> Option.map PromptAuthority.PromptOrigin.Continuation

    let private validateClaimOrigin
        (projection: AgentProjectionSet)
        (identitySeed: PromptAuthority.IdentitySeed)
        (origin: PromptAuthority.PromptOrigin option)
        : Result<PromptAuthority.PromptOrigin option, PromptAuthority.IdentitySeedValidationError> =
        match origin with
        | Some(PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot) ->
            validateAcceptedIdentitySeed projection PromptAuthority.RootAuthorityKind.AgentOwnerRoot identitySeed
            |> Result.map (fun () -> origin)
        | _ -> Ok origin

    let private applyValidatedClaimOrigin projection register validation =
        match validation with
        | Error error -> reject "PluginPromptClaimed" (sprintf "%A" error)
        | Ok None -> Ok projection
        | Ok(Some resolvedOrigin) -> register resolvedOrigin

    let private foldAuthorityRootAccepted (projection: AgentProjectionSet) (payload: AuthorityRootAcceptedPayload) =
        let currentAuthority =
            AgentProjection.tryFind payload.SessionId projection
            |> Option.bind (fun session -> session.PromptAuthority)
            |> Option.defaultValue PromptAuthorityLedger.empty

        let authorityResult =
            validateAuthorityRootAccepted payload.SchemaVersion payload.AuthorityKind
            |> Result.bind (fun authorityKind ->
                validateAcceptedIdentitySeed projection authorityKind payload.IdentitySeed
                |> Result.mapError (sprintf "%A")
                |> Result.bind (fun () -> PromptAuthorityLedger.foldAuthorityRootAccepted currentAuthority payload)
                |> Result.map (fun authority -> authorityKind, authority))

        match authorityResult with
        | Error reason -> reject "AuthorityRootAccepted" reason
        | Ok(authorityKind, authority) ->
            updateSession
                payload.SessionId
                (fun session ->
                    { session with
                        PromptAuthority = Some authority
                        Fallback =
                            Some(
                                FallbackProjection.forAuthority payload.LogicalRunId payload.AuthorityRootUserMessageId
                            ) })
                projection
            |> admitReviewRequirement authorityKind payload
            |> Ok

    let fold (projection: AgentProjectionSet) (fact: PromptFactCases) : Result<AgentProjectionSet, FoldRejection> =
        match fact with
        // ── prompt dispatch ─────────────────────────────────────────────────

        | PromptFactCases.PluginPromptClaimed payload ->
            let register resolvedOrigin =
                let claim: PromptAuthority.PromptClaim =
                    { PromptKey = payload.PromptKey
                      SessionId = payload.SessionId
                      Origin = resolvedOrigin
                      LogicalRunId = payload.LogicalRunId
                      AuthorityRootUserMessageId = payload.AuthorityRootUserMessageId
                      EffectiveAgent = payload.EffectiveAgent
                      IdentitySeed = payload.IdentitySeed
                      PayloadDigest = payload.PayloadDigest
                      Receipt = None
                      ClaimedAtRuntimeStartCount = projection.RuntimeStartCount }

                Ok(updateAuthority payload.SessionId (PromptAuthorityRun.registerClaim claim) projection)

            classifyClaimOrigin payload.ContinuationKind
            |> validateClaimOrigin projection payload.IdentitySeed
            |> applyValidatedClaimOrigin projection register

        | PromptFactCases.PluginPromptSubmitted payload ->
            Ok(
                updateAuthority
                    payload.SessionId
                    (fun authority -> PromptAuthorityLedger.foldPromptSubmitted authority payload)
                    projection
            )

        | PromptFactCases.PluginPromptPhysicalAccepted payload ->
            Ok(
                updateAuthority
                    payload.SessionId
                    (fun authority -> PromptAuthorityLedger.foldPromptPhysicalAccepted authority payload)
                    projection
            )

        | PromptFactCases.PluginPromptAbandoned payload ->
            Ok(
                updateAuthority
                    payload.SessionId
                    (fun authority -> PromptAuthorityLedger.foldPromptAbandoned authority payload)
                    projection
            )

        // ── authority ───────────────────────────────────────────────────────

        | PromptFactCases.AuthorityRootAccepted payload -> foldAuthorityRootAccepted projection payload
