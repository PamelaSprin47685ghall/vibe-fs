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
        | Some value -> Roles.roleLabel value
        | None -> "None"

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
        | ParticipantIdentityError.UnsupportedLegacyAuthorityKind authorityKind ->
            sprintf "legacy AuthorityRootAccepted v1 AuthorityKind is unsupported: %s" authorityKind
        | ParticipantIdentityError.UnprovableLegacyAuthorityIdentity reason -> reason

    let private roleEncoder role =
        match role with
        | Some value -> Encode.string (Roles.roleLabel value)
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
            [ "InitialTier", Encode.string "deep"
              "Origin", Encode.string (originLabel input.Origin)
              "Persona", Encode.string input.Persona
              "PersonaCatalogVersion", Encode.int input.PersonaCatalogVersion
              "Role", roleEncoder input.Role
              "SelectedAgent", Encode.string input.SelectedAgent ]

    let private identitySeedEncoder seed =
        match PromptIdentitySeed.toInput seed with
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

    let private optionalStringEncoder value =
        match value with
        | Some text -> Encode.string text
        | None -> Encode.nil

    let private authorityPayloadEncoder payload =
        if payload.SchemaVersion <> 2 && payload.SchemaVersion <> 3 then
            failwith (
                sprintf "AuthorityRootAccepted encoder requires SchemaVersion 2 or 3, got %d" payload.SchemaVersion
            )

        match validateAuthorityIdentitySeed payload.AuthorityKind payload.IdentitySeed with
        | Error reason -> failwith reason
        | Ok() -> ()

        Encode.object
            [ "AgentOwnerLife", optionalStringEncoder payload.AgentOwnerLife
              "AuthorityKind", Encode.string payload.AuthorityKind
              "AuthorityRootUserMessageId",
              taggedString
                  "AuthorityRootUserMessageId"
                  (AuthorityRootUserMessageId.value payload.AuthorityRootUserMessageId)
              "ExpectedClosureKind", Encode.string payload.ExpectedClosureKind
              "IdentitySeed", identitySeedEncoder payload.IdentitySeed
              "LogicalRunId", taggedString "LogicalRunId" (LogicalRunId.value payload.LogicalRunId)
              "OwnerLogicalRunId", optionalStringEncoder payload.OwnerLogicalRunId
              "SchemaVersion", Encode.int payload.SchemaVersion
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
                match Roles.tryParseRole label with
                | Some role -> Decode.succeed (Some role)
                | None -> Decode.fail (sprintf "participant identity Role is unknown: %s" label))

    let private originDecoder =
        Decode.string
        |> Decode.andThen (function
            | "ResolvedAtRoot" -> Decode.succeed PersonaOrigin.ResolvedAtRoot
            | "InheritedFromOwner" -> Decode.succeed PersonaOrigin.InheritedFromOwner
            | value -> Decode.fail (sprintf "participant identity Origin is unknown: %s" value))

    let private identityInputDecoder =
        Decode.object (fun get ->
            let _ = get.Optional.Field "PeerAgent" Decode.string
            let _ = get.Optional.Field "EffectiveAgent" Decode.string

            { SelectedAgent = get.Required.Field "SelectedAgent" Decode.string
              Role = get.Required.Field "Role" roleDecoder
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
            match PromptIdentitySeed.rehydrate input with
            | Ok seed -> Decode.succeed seed
            | Error error -> Decode.fail (identityErrorMessage error))

    let private isManagerSeed seed =
        match ParticipantIdentity.role (PromptAuthority.identitySeedParticipantIdentity seed) with
        | Some role -> role = Roles.Manager
        | None -> false

    /// The closure kind of a legacy schema-2 acceptance, derived from what the
    /// record proves. A legacy agent-owner root is refused: its life cannot be
    /// derived, and a guessed kind would close a life that never was.
    let private legacyExpectedClosureKind authorityKind seed =
        match authorityKind with
        | "HumanRoot" -> AuthorityLifeClassification.expectedClosureKind "HumanRoot" (isManagerSeed seed) None
        | "AgentOwnerRoot" ->
            Error "legacy AuthorityRootAccepted schema 2 AgentOwnerRoot cannot prove its expected closure kind"
        | unknown -> Error(sprintf "AuthorityRootAccepted AuthorityKind is unknown: %s" unknown)

    /// The closure kind a schema-2 acceptance records: the recorded one when it
    /// proves one, otherwise the kind the record can derive.
    let private schema2ExpectedClosureKind (payload: AuthorityRootAcceptedPayload) : Result<string, string> =
        if String.IsNullOrEmpty payload.ExpectedClosureKind then
            legacyExpectedClosureKind payload.AuthorityKind payload.IdentitySeed
        else
            Ok payload.ExpectedClosureKind

    /// The recorded closure kind must belong to the recorded root kind.
    let private recordedRootKindMatches (payload: AuthorityRootAcceptedPayload) : Result<unit, string> =
        if AuthorityLifeClassification.rootKindOfKind payload.ExpectedClosureKind = payload.AuthorityKind then
            Ok()
        else
            Error(
                sprintf
                    "AuthorityRootAccepted ExpectedClosureKind %s does not match AuthorityKind %s"
                    payload.ExpectedClosureKind
                    payload.AuthorityKind
            )

    /// A schema-3 acceptance must name a closure kind the law knows.
    let private validateRecordedClosureKind (payload: AuthorityRootAcceptedPayload) : Result<unit, string> =
        if
            AuthorityLifeClassification.closureKindLabels
            |> List.contains payload.ExpectedClosureKind
        then
            recordedRootKindMatches payload
        else
            Error(sprintf "AuthorityRootAccepted ExpectedClosureKind is unknown: %s" payload.ExpectedClosureKind)

    let private currentPayloadDecoder =
        Decode.object (fun get ->
            let expectedClosureKind = get.Optional.Field "ExpectedClosureKind" Decode.string

            { SchemaVersion = 2
              SessionId = get.Required.Field "SessionId" (taggedStringDecoder "SessionId" SessionId.create)
              LogicalRunId = get.Required.Field "LogicalRunId" (taggedStringDecoder "LogicalRunId" LogicalRunId.create)
              AuthorityRootUserMessageId =
                get.Required.Field
                    "AuthorityRootUserMessageId"
                    (taggedStringDecoder "AuthorityRootUserMessageId" AuthorityRootUserMessageId.create)
              AuthorityKind = get.Required.Field "AuthorityKind" Decode.string
              ExpectedClosureKind = expectedClosureKind |> Option.defaultValue ""
              OwnerLogicalRunId = get.Optional.Field "OwnerLogicalRunId" Decode.string
              AgentOwnerLife = get.Optional.Field "AgentOwnerLife" Decode.string
              IdentitySeed = get.Required.Field "IdentitySeed" identitySeedDecoder })
        |> Decode.andThen (fun payload ->
            let validated =
                validateAuthorityIdentitySeed payload.AuthorityKind payload.IdentitySeed
                |> Result.bind (fun () ->
                    schema2ExpectedClosureKind payload
                    |> Result.map (fun kind ->
                        { payload with
                            ExpectedClosureKind = kind }))

            match validated with
            | Ok decoded -> Decode.succeed decoded
            | Error reason -> Decode.fail reason)

    let private currentPayloadDecoderV3 =
        Decode.object (fun get ->
            { SchemaVersion = 3
              SessionId = get.Required.Field "SessionId" (taggedStringDecoder "SessionId" SessionId.create)
              LogicalRunId = get.Required.Field "LogicalRunId" (taggedStringDecoder "LogicalRunId" LogicalRunId.create)
              AuthorityRootUserMessageId =
                get.Required.Field
                    "AuthorityRootUserMessageId"
                    (taggedStringDecoder "AuthorityRootUserMessageId" AuthorityRootUserMessageId.create)
              AuthorityKind = get.Required.Field "AuthorityKind" Decode.string
              ExpectedClosureKind = get.Required.Field "ExpectedClosureKind" Decode.string
              OwnerLogicalRunId = get.Optional.Field "OwnerLogicalRunId" Decode.string
              AgentOwnerLife = get.Optional.Field "AgentOwnerLife" Decode.string
              IdentitySeed = get.Required.Field "IdentitySeed" identitySeedDecoder })
        |> Decode.andThen (fun payload ->
            let validated =
                validateAuthorityIdentitySeed payload.AuthorityKind payload.IdentitySeed
                |> Result.bind (fun () -> validateRecordedClosureKind payload)

            match validated with
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

            let _ = get.Optional.Field "PeerAgent" Decode.string
            let _ = get.Optional.Field "EffectiveAgent" Decode.string

            let identity =
                { AuthorityKind = get.Required.Field "AuthorityKind" Decode.string
                  SelectedAgent = get.Required.Field "SelectedAgent" Decode.string
                  CanonicalRole = get.Required.Field "CanonicalRole" Decode.string }

            sessionId, logicalRunId, authorityRootUserMessageId, identity)

    let private legacyPayloadDecoder =
        legacyPayloadFieldsDecoder
        |> Decode.andThen (fun (sessionId, logicalRunId, authorityRootUserMessageId, legacyIdentity) ->
            let upgraded =
                ParticipantIdentity.upgradeLegacyV1Root legacyIdentity
                |> Result.map (fun identity -> PromptIdentitySeed.RootSelection identity)
                |> Result.mapError identityErrorMessage

            let validated =
                upgraded
                |> Result.bind (fun seed ->
                    legacyExpectedClosureKind legacyIdentity.AuthorityKind seed
                    |> Result.map (fun kind ->
                        { SchemaVersion = 2
                          SessionId = sessionId
                          LogicalRunId = logicalRunId
                          AuthorityRootUserMessageId = authorityRootUserMessageId
                          AuthorityKind = legacyIdentity.AuthorityKind
                          ExpectedClosureKind = kind
                          OwnerLogicalRunId = None
                          AgentOwnerLife = None
                          IdentitySeed = seed }))

            match validated with
            | Ok decoded -> Decode.succeed decoded
            | Error reason -> Decode.fail reason)

    let private authorityPayloadDecoder =
        Decode.object (fun get -> get.Optional.Field "SchemaVersion" Decode.int)
        |> Decode.andThen (function
            | None -> legacyPayloadDecoder
            | Some 2 -> currentPayloadDecoder
            | Some 3 -> currentPayloadDecoderV3
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
