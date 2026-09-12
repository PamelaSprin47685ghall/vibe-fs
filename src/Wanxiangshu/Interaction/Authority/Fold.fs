namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type PromptAuthorityProjectionChange =
    | PromptAuthoritySet of SessionId * PromptAuthority.PromptAuthorityProjection
    | ProviderFailuresSet of SessionId * ProviderFailureProjection

[<RequireQualifiedAccess>]
type PromptAuthorityFoldRejection =
    | ClaimOriginRejected of PromptAuthority.IdentitySeedValidationError
    | AuthorityRootSchemaRejected of string
    | AuthorityRootSeedRejected of PromptAuthority.IdentitySeedValidationError
    | AuthorityRootLedgerRejected of string

[<RequireQualifiedAccess>]
module PromptAuthorityFoldRejection =
    let fact (rejection: PromptAuthorityFoldRejection) : string =
        match rejection with
        | PromptAuthorityFoldRejection.ClaimOriginRejected _ -> "PluginPromptClaimed"
        | PromptAuthorityFoldRejection.AuthorityRootSchemaRejected _
        | PromptAuthorityFoldRejection.AuthorityRootSeedRejected _
        | PromptAuthorityFoldRejection.AuthorityRootLedgerRejected _ -> "AuthorityRootAccepted"

    let message (rejection: PromptAuthorityFoldRejection) : string =
        match rejection with
        | PromptAuthorityFoldRejection.ClaimOriginRejected error -> sprintf "%A" error
        | PromptAuthorityFoldRejection.AuthorityRootSchemaRejected reason -> reason
        | PromptAuthorityFoldRejection.AuthorityRootSeedRejected error -> sprintf "%A" error
        | PromptAuthorityFoldRejection.AuthorityRootLedgerRejected reason -> reason

module PromptFactFold =

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

    let private validateAcceptedIdentitySeed
        (authorityOf: SessionId -> PromptAuthority.PromptAuthorityProjection option)
        authorityKind
        seed
        =
        match authorityKind, PromptAuthority.identitySeedOwner seed with
        | PromptAuthority.RootAuthorityKind.HumanRoot, _ -> Ok()
        | PromptAuthority.RootAuthorityKind.AgentOwnerRoot, None ->
            Error PromptAuthority.IdentitySeedValidationError.ExpectedInheritedFromOwner
        | PromptAuthority.RootAuthorityKind.AgentOwnerRoot, Some(ownerSessionId, _, _) ->
            authorityOf ownerSessionId
            |> Option.bind (fun authority -> authority.ActiveLogicalRun)
            |> fun activeOwner ->
                PromptAuthority.validateInheritedIdentitySeedAgainstActiveOwner activeOwner seed
                |> Result.map ignore

    let private classifyClaimOrigin (continuationKind: string) : PromptAuthority.PromptOrigin option =
        if continuationKind = "AgentOwnerRoot" then
            Some(PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot)
        else
            PromptAuthority.tryParseContinuationKind continuationKind
            |> Option.map PromptAuthority.PromptOrigin.Continuation

    let private validateClaimOrigin
        authorityOf
        (identitySeed: PromptAuthority.IdentitySeed)
        (origin: PromptAuthority.PromptOrigin option)
        : Result<PromptAuthority.PromptOrigin option, PromptAuthority.IdentitySeedValidationError> =
        match origin with
        | Some(PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot) ->
            validateAcceptedIdentitySeed authorityOf PromptAuthority.RootAuthorityKind.AgentOwnerRoot identitySeed
            |> Result.map (fun () -> origin)
        | _ -> Ok origin

    let private applyValidatedClaimOrigin register validation =
        match validation with
        | Error error -> Error(PromptAuthorityFoldRejection.ClaimOriginRejected error)
        | Ok None -> Ok []
        | Ok(Some resolvedOrigin) -> register resolvedOrigin

    let private foldAuthorityRootAccepted
        (authorityOf: SessionId -> PromptAuthority.PromptAuthorityProjection option)
        (payload: AuthorityRootAcceptedPayload)
        : Result<PromptAuthorityProjectionChange list, PromptAuthorityFoldRejection> =
        let currentAuthority =
            authorityOf payload.SessionId |> Option.defaultValue PromptAuthorityLedger.empty

        validateAuthorityRootAccepted payload.SchemaVersion payload.AuthorityKind
        |> Result.mapError PromptAuthorityFoldRejection.AuthorityRootSchemaRejected
        |> Result.bind (fun authorityKind ->
            validateAcceptedIdentitySeed authorityOf authorityKind payload.IdentitySeed
            |> Result.mapError PromptAuthorityFoldRejection.AuthorityRootSeedRejected
            |> Result.bind (fun () ->
                PromptAuthorityLedger.foldAuthorityRootAccepted currentAuthority payload
                |> Result.mapError PromptAuthorityFoldRejection.AuthorityRootLedgerRejected)
            |> Result.map (fun authority ->
                [ PromptAuthorityProjectionChange.PromptAuthoritySet(payload.SessionId, authority)
                  PromptAuthorityProjectionChange.ProviderFailuresSet(
                      payload.SessionId,
                      ProviderFailureProjection.forAuthority payload.LogicalRunId payload.AuthorityRootUserMessageId
                  ) ]))

    let fold
        (authorityOf: SessionId -> PromptAuthority.PromptAuthorityProjection option)
        (runtimeStartCount: int)
        (fact: PromptFactCases)
        : Result<PromptAuthorityProjectionChange list, PromptAuthorityFoldRejection> =
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
                      IdentitySeed = payload.IdentitySeed
                      PayloadDigest = payload.PayloadDigest
                      Receipt = None
                      ClaimedAtRuntimeStartCount = runtimeStartCount }

                let currentAuthority =
                    authorityOf payload.SessionId |> Option.defaultValue PromptAuthorityLedger.empty

                let registered = PromptAuthorityRun.registerClaim claim currentAuthority
                Ok [ PromptAuthorityProjectionChange.PromptAuthoritySet(payload.SessionId, registered) ]

            classifyClaimOrigin payload.ContinuationKind
            |> validateClaimOrigin authorityOf payload.IdentitySeed
            |> applyValidatedClaimOrigin register

        | PromptFactCases.PluginPromptSubmitted payload ->
            let currentAuthority =
                authorityOf payload.SessionId |> Option.defaultValue PromptAuthorityLedger.empty

            let folded = PromptAuthorityLedger.foldPromptSubmitted currentAuthority payload
            Ok [ PromptAuthorityProjectionChange.PromptAuthoritySet(payload.SessionId, folded) ]

        | PromptFactCases.PluginPromptPhysicalAccepted payload ->
            let currentAuthority =
                authorityOf payload.SessionId |> Option.defaultValue PromptAuthorityLedger.empty

            let folded =
                PromptAuthorityLedger.foldPromptPhysicalAccepted currentAuthority payload

            Ok [ PromptAuthorityProjectionChange.PromptAuthoritySet(payload.SessionId, folded) ]

        | PromptFactCases.PluginPromptAbandoned payload ->
            let currentAuthority =
                authorityOf payload.SessionId |> Option.defaultValue PromptAuthorityLedger.empty

            let folded = PromptAuthorityLedger.foldPromptAbandoned currentAuthority payload
            Ok [ PromptAuthorityProjectionChange.PromptAuthoritySet(payload.SessionId, folded) ]

        // ── authority ───────────────────────────────────────────────────────

        | PromptFactCases.AuthorityRootAccepted payload -> foldAuthorityRootAccepted authorityOf payload
