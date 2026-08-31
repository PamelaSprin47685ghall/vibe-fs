namespace Wanxiangshu.Persistence.Journal

open Thoth.Json
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Persona

[<RequireQualifiedAccess>]
module PromptFactCodec =

    let private taggedString tag value =
        Encode.array [| Encode.string tag; Encode.string value |]

    let private roleLabel role =
        match role with
        | Some value -> ManagedAgentCatalog.roleLabel value
        | None -> "None"

    let private tierLabel = ManagedAgentCatalog.tierLabel

    let private originLabel origin =
        match origin with
        | PersonaOrigin.ResolvedAtRoot -> "ResolvedAtRoot"
        | PersonaOrigin.InheritedFromOwner -> "InheritedFromOwner"

    let private identityErrorMessage error =
        match error with
        | ParticipantIdentityError.BlankParticipantName -> "participant identity SelectedAgent is blank"
        | ParticipantIdentityError.LegacyParticipantName name ->
            sprintf "participant identity SelectedAgent is legacy: %s" name
        | ParticipantIdentityError.MalformedParticipantName name ->
            sprintf "participant identity SelectedAgent is malformed: %s" name
        | ParticipantIdentityError.UnknownParticipantName name ->
            sprintf "participant identity SelectedAgent is unknown: %s" name
        | ParticipantIdentityError.UnsupportedPersonaCatalogVersion version ->
            sprintf "participant identity PersonaCatalogVersion is unsupported: %d" version
        | ParticipantIdentityError.RoleMismatch(expected, actual) ->
            sprintf "participant identity Role mismatch: expected %s, got %s" (roleLabel expected) (roleLabel actual)
        | ParticipantIdentityError.TierMismatch(expected, actual) ->
            sprintf
                "participant identity InitialTier mismatch: expected %s, got %s"
                (tierLabel expected)
                (tierLabel actual)
        | ParticipantIdentityError.PeerMismatch(expected, actual) ->
            sprintf "participant identity PeerAgent mismatch: expected %s, got %s" expected actual
        | ParticipantIdentityError.BlankPersona -> "participant identity Persona is blank"
        | ParticipantIdentityError.PersonaMismatch(expected, actual) ->
            sprintf "participant identity Persona mismatch: expected %s, got %s" expected actual
        | ParticipantIdentityError.OriginMismatch(expected, actual) ->
            sprintf
                "participant identity Origin mismatch: expected %s, got %s"
                (originLabel expected)
                (originLabel actual)
        | ParticipantIdentityError.OwnerRequired -> "participant identity owner evidence is required"
        | ParticipantIdentityError.OwnerPersonaMismatch(expected, actual) ->
            sprintf "participant identity owner Persona mismatch: expected %s, got %s" expected actual
        | ParticipantIdentityError.OwnerCatalogVersionMismatch(expected, actual) ->
            sprintf "participant identity owner PersonaCatalogVersion mismatch: expected %d, got %d" expected actual
        | ParticipantIdentityError.LegacyRoleMismatch(expected, actual) ->
            sprintf "legacy AuthorityRootAccepted v1 CanonicalRole mismatch: expected %s, got %s" expected actual
        | ParticipantIdentityError.LegacyTierMismatch(expected, actual) ->
            sprintf "legacy AuthorityRootAccepted v1 SelectedTier mismatch: expected %s, got %s" expected actual
        | ParticipantIdentityError.UnsupportedLegacyAuthorityKind authorityKind ->
            sprintf "legacy AuthorityRootAccepted v1 AuthorityKind is unsupported: %s" authorityKind
        | ParticipantIdentityError.UnprovableLegacyAuthorityIdentity reason -> reason

    let private roleEncoder role =
        match role with
        | Some value -> Encode.string (ManagedAgentCatalog.roleLabel value)
        | None -> Encode.nil

    let private validateAuthorityIdentitySeed authorityKind seed =
        let evidence = PromptAuthority.identitySeedParticipantIdentity seed

        match authorityKind, seed, ParticipantIdentity.origin evidence with
        | "HumanRoot", PromptIdentitySeed.RootSelection _, PersonaOrigin.ResolvedAtRoot
        | "AgentOwnerRoot", PromptIdentitySeed.InheritedFromOwner _, PersonaOrigin.InheritedFromOwner -> Ok()
        | "HumanRoot", _, _ -> Error "AuthorityRootAccepted HumanRoot requires a root-selection identity seed"
        | "AgentOwnerRoot", _, _ ->
            Error "AuthorityRootAccepted AgentOwnerRoot requires an inherited owner identity seed"
        | unknown, _, _ -> Error(sprintf "AuthorityRootAccepted AuthorityKind is unknown: %s" unknown)

    let private identityEncoder evidence =
        let input = ParticipantIdentity.toInput evidence

        Encode.object
            [ "InitialTier", Encode.string (tierLabel input.InitialTier)
              "Origin", Encode.string (originLabel input.Origin)
              "PeerAgent", Encode.string input.PeerAgent
              "Persona", Encode.string input.Persona
              "PersonaCatalogVersion", Encode.int input.PersonaCatalogVersion
              "Role", roleEncoder input.Role
              "SelectedAgent", Encode.string input.SelectedAgent ]

    let private identitySeedEncoder seed =
        match PromptAuthority.identitySeedInput seed with
        | PromptIdentitySeedInput.RootSelectionInput _ ->
            Encode.array
                [| Encode.string "RootSelection"
                   identityEncoder (PromptAuthority.identitySeedParticipantIdentity seed) |]
        | PromptIdentitySeedInput.InheritedFromOwnerInput witness ->
            Encode.array
                [| Encode.string "InheritedFromOwner"
                   Encode.object
                       [ "OwnerAuthorityRootUserMessageId",
                         taggedString
                             "AuthorityRootUserMessageId"
                             (AuthorityRootUserMessageId.value witness.OwnerAuthorityRootUserMessageId)
                         "OwnerLogicalRunId", taggedString "LogicalRunId" (LogicalRunId.value witness.OwnerLogicalRunId)
                         "OwnerSessionId", taggedString "SessionId" (SessionId.value witness.OwnerSessionId)
                         "ParticipantIdentity", identityEncoder (PromptAuthority.identitySeedParticipantIdentity seed) ] |]

    let private authorityPayloadEncoder payload =
        if payload.SchemaVersion <> 2 then
            failwith (sprintf "AuthorityRootAccepted encoder requires SchemaVersion 2, got %d" payload.SchemaVersion)

        match validateAuthorityIdentitySeed payload.AuthorityKind payload.IdentitySeed with
        | Error reason -> failwith reason
        | Ok() -> ()

        Encode.object
            [ "AuthorityKind", Encode.string payload.AuthorityKind
              "AuthorityRootUserMessageId",
              taggedString
                  "AuthorityRootUserMessageId"
                  (AuthorityRootUserMessageId.value payload.AuthorityRootUserMessageId)
              "IdentitySeed", identitySeedEncoder payload.IdentitySeed
              "LogicalRunId", taggedString "LogicalRunId" (LogicalRunId.value payload.LogicalRunId)
              "SchemaVersion", Encode.int 2
              "SessionId", taggedString "SessionId" (SessionId.value payload.SessionId) ]

    let private taggedStringDecoder expected create =
        Decode.index 0 Decode.string
        |> Decode.andThen (fun actual ->
            if actual = expected then
                Decode.index 1 Decode.string |> Decode.map create
            else
                Decode.fail (sprintf "expected %s, got %s" expected actual))

    let private roleDecoder =
        Decode.option Decode.string
        |> Decode.andThen (function
            | None -> Decode.succeed None
            | Some label ->
                match ManagedAgentCatalog.tryParseRole label with
                | Some role -> Decode.succeed (Some role)
                | None -> Decode.fail (sprintf "participant identity Role is unknown: %s" label))

    let private tierDecoder =
        Decode.string
        |> Decode.andThen (fun label ->
            match ManagedAgentCatalog.tryParseTier label with
            | Some tier -> Decode.succeed tier
            | None -> Decode.fail (sprintf "participant identity InitialTier is unknown: %s" label))

    let private originDecoder =
        Decode.string
        |> Decode.andThen (function
            | "ResolvedAtRoot" -> Decode.succeed PersonaOrigin.ResolvedAtRoot
            | "InheritedFromOwner" -> Decode.succeed PersonaOrigin.InheritedFromOwner
            | value -> Decode.fail (sprintf "participant identity Origin is unknown: %s" value))

    let private identityInputDecoder =
        Decode.object (fun get ->
            { SelectedAgent = get.Required.Field "SelectedAgent" Decode.string
              PeerAgent = get.Required.Field "PeerAgent" Decode.string
              Role = get.Required.Field "Role" roleDecoder
              InitialTier = get.Required.Field "InitialTier" tierDecoder
              Persona = get.Required.Field "Persona" Decode.string
              PersonaCatalogVersion = get.Required.Field "PersonaCatalogVersion" Decode.int
              Origin = get.Required.Field "Origin" originDecoder })

    let private identitySeedDecoder =
        Decode.index 0 Decode.string
        |> Decode.andThen (function
            | "RootSelection" ->
                Decode.index 1 identityInputDecoder
                |> Decode.map PromptIdentitySeedInput.RootSelectionInput
            | "InheritedFromOwner" ->
                Decode.index
                    1
                    (Decode.object (fun get ->
                        PromptIdentitySeedInput.InheritedFromOwnerInput
                            { OwnerSessionId =
                                get.Required.Field "OwnerSessionId" (taggedStringDecoder "SessionId" SessionId.create)
                              OwnerLogicalRunId =
                                get.Required.Field
                                    "OwnerLogicalRunId"
                                    (taggedStringDecoder "LogicalRunId" LogicalRunId.create)
                              OwnerAuthorityRootUserMessageId =
                                get.Required.Field
                                    "OwnerAuthorityRootUserMessageId"
                                    (taggedStringDecoder "AuthorityRootUserMessageId" AuthorityRootUserMessageId.create)
                              ParticipantIdentity = get.Required.Field "ParticipantIdentity" identityInputDecoder }))
            | kind -> Decode.fail (sprintf "identity seed kind is unknown: %s" kind))
        |> Decode.andThen (fun input ->
            match PromptAuthority.rehydrateIdentitySeed input with
            | Ok seed -> Decode.succeed seed
            | Error error -> Decode.fail (identityErrorMessage error))

    let private currentPayloadDecoder =
        Decode.object (fun get ->
            { SchemaVersion = 2
              SessionId = get.Required.Field "SessionId" (taggedStringDecoder "SessionId" SessionId.create)
              LogicalRunId = get.Required.Field "LogicalRunId" (taggedStringDecoder "LogicalRunId" LogicalRunId.create)
              AuthorityRootUserMessageId =
                get.Required.Field
                    "AuthorityRootUserMessageId"
                    (taggedStringDecoder "AuthorityRootUserMessageId" AuthorityRootUserMessageId.create)
              AuthorityKind = get.Required.Field "AuthorityKind" Decode.string
              IdentitySeed = get.Required.Field "IdentitySeed" identitySeedDecoder })
        |> Decode.andThen (fun payload ->
            match validateAuthorityIdentitySeed payload.AuthorityKind payload.IdentitySeed with
            | Ok() -> Decode.succeed payload
            | Error reason -> Decode.fail reason)

    let private legacyPayloadFieldsDecoder =
        Decode.object (fun get ->
            let sessionId =
                get.Required.Field "SessionId" (taggedStringDecoder "SessionId" SessionId.create)

            let logicalRunId =
                get.Required.Field "LogicalRunId" (taggedStringDecoder "LogicalRunId" LogicalRunId.create)

            let authorityRootUserMessageId =
                get.Required.Field
                    "AuthorityRootUserMessageId"
                    (taggedStringDecoder "AuthorityRootUserMessageId" AuthorityRootUserMessageId.create)

            let identity =
                { AuthorityKind = get.Required.Field "AuthorityKind" Decode.string
                  SelectedAgent = get.Required.Field "SelectedAgent" Decode.string
                  PeerAgent = get.Required.Field "PeerAgent" Decode.string
                  CanonicalRole = get.Required.Field "CanonicalRole" Decode.string
                  SelectedTier = get.Required.Field "SelectedTier" Decode.string }

            sessionId, logicalRunId, authorityRootUserMessageId, identity)

    let private legacyPayloadDecoder =
        legacyPayloadFieldsDecoder
        |> Decode.andThen (fun (sessionId, logicalRunId, authorityRootUserMessageId, legacyIdentity) ->
            match ParticipantIdentity.upgradeLegacyV1Root legacyIdentity with
            | Ok identity ->
                Decode.succeed
                    { SchemaVersion = 2
                      SessionId = sessionId
                      LogicalRunId = logicalRunId
                      AuthorityRootUserMessageId = authorityRootUserMessageId
                      AuthorityKind = legacyIdentity.AuthorityKind
                      IdentitySeed = PromptIdentitySeed.RootSelection identity }
            | Error error -> Decode.fail (identityErrorMessage error))

    let private authorityPayloadDecoder =
        Decode.object (fun get -> get.Optional.Field "SchemaVersion" Decode.int)
        |> Decode.andThen (function
            | None -> legacyPayloadDecoder
            | Some 2 -> currentPayloadDecoder
            | Some version -> Decode.fail (sprintf "AuthorityRootAccepted schema version is unsupported: %d" version))

    let private authorityFactDecoder =
        Decode.index 1 authorityPayloadDecoder
        |> Decode.map PromptFactCases.AuthorityRootAccepted

    let withCoder (baseExtra: ExtraCoders) : ExtraCoders =
        let seedExtra = Extra.withCustom identitySeedEncoder identitySeedDecoder baseExtra

        let autoEncoder =
            Encode.Auto.generateEncoderCached<PromptFactCases> (extra = seedExtra)

        let autoDecoder =
            Decode.Auto.generateDecoderCached<PromptFactCases> (extra = seedExtra)

        let encoder fact =
            match fact with
            | PromptFactCases.AuthorityRootAccepted payload ->
                Encode.array [| Encode.string "AuthorityRootAccepted"; authorityPayloadEncoder payload |]
            | other -> autoEncoder other

        let decoder =
            Decode.index 0 Decode.string
            |> Decode.andThen (function
                | "AuthorityRootAccepted" -> authorityFactDecoder
                | _ -> autoDecoder)

        Extra.withCustom encoder decoder seedExtra
