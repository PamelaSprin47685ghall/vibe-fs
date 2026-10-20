namespace Wanxiangshu.Participant.Persona

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type PersonaOrigin =
    | ResolvedAtRoot
    | InheritedFromOwner

[<RequireQualifiedAccess>]
type ParticipantIdentityError =
    | BlankParticipantName
    | LegacyParticipantName of string
    | MalformedParticipantName of string
    | UnknownParticipantName of string
    | UnsupportedPersonaCatalogVersion of int
    | RoleMismatch of Expected: Role option * Actual: Role option
    | BlankPersona
    | PersonaMismatch of Expected: string * Actual: string
    | OriginMismatch of Expected: PersonaOrigin * Actual: PersonaOrigin
    | OwnerRequired
    | OwnerPersonaMismatch of Expected: string * Actual: string
    | OwnerCatalogVersionMismatch of Expected: int * Actual: int
    | LegacyRoleMismatch of Expected: string * Actual: string
    | UnsupportedLegacyAuthorityKind of string
    | UnprovableLegacyAuthorityIdentity of string

type ParticipantIdentityInput =
    { SelectedAgent: string
      Role: Role option
      Persona: string
      PersonaCatalogVersion: int
      Origin: PersonaOrigin }

type LegacyAuthorityRootIdentityV1Input =
    { AuthorityKind: string
      SelectedAgent: string
      CanonicalRole: string }

type private PersonaName = PersonaName of string

type private PersonaCatalogVersion = PersonaCatalogVersion of int

type private ParticipantKind =
    | ManagedRole of Role
    | Bookkeeper

type ParticipantIdentity =
    private
        { SelectedAgent: PersonaName
          Kind: ParticipantKind
          Persona: string
          PersonaCatalogVersion: PersonaCatalogVersion
          Origin: PersonaOrigin }

type ParticipantIdentityEvidence =
    private
        { Identity: ParticipantIdentity }

/// Typed F# projection of the participant identity owner in
/// `wanxiangli/identity` (CUT-07). Name parsing, legacy rejection, persona
/// resolution and every rehydration rule are decided by the library; evidence
/// is constructed here only from a library success view.
[<RequireQualifiedAccess>]
module ParticipantIdentity =

    let private library = IdentityLibrary.instance

    let private versionNumber (PersonaCatalogVersion version) = version

    let private nameValue (PersonaName name) = name

    let private roleOfKind kind =
        match kind with
        | ManagedRole role -> Some role
        | Bookkeeper -> None

    let private roleLabelOfKind kind =
        match kind with
        | ManagedRole role -> Roles.roleLabel role
        | Bookkeeper -> "bookkeeper"

    let private originLabel origin =
        match origin with
        | PersonaOrigin.ResolvedAtRoot -> "ResolvedAtRoot"
        | PersonaOrigin.InheritedFromOwner -> "InheritedFromOwner"

    let private originOfLabel (label: obj) =
        match unbox<string> label with
        | "InheritedFromOwner" -> PersonaOrigin.InheritedFromOwner
        | _ -> PersonaOrigin.ResolvedAtRoot

    let private roleOfLabel (label: obj) : Role option =
        if isNull label then
            None
        else
            Roles.tryParseHistoricalRole (unbox<string> label)

    let private roleView (role: Role option) : obj =
        match role with
        | Some value -> box (Roles.roleLabel value)
        | None -> null

    let private identity (evidence: ParticipantIdentityEvidence) = evidence.Identity

    let selectedAgent (evidence: ParticipantIdentityEvidence) : string =
        (identity evidence).SelectedAgent |> nameValue

    let role (evidence: ParticipantIdentityEvidence) : Role option = (identity evidence).Kind |> roleOfKind

    let roleLabel (evidence: ParticipantIdentityEvidence) : string =
        (identity evidence).Kind |> roleLabelOfKind

    let persona (evidence: ParticipantIdentityEvidence) : string = (identity evidence).Persona

    let personaCatalogVersion (evidence: ParticipantIdentityEvidence) : int =
        (identity evidence).PersonaCatalogVersion |> versionNumber

    let origin (evidence: ParticipantIdentityEvidence) : PersonaOrigin = (identity evidence).Origin

    let toInput (evidence: ParticipantIdentityEvidence) : ParticipantIdentityInput =
        { SelectedAgent = selectedAgent evidence
          Role = role evidence
          Persona = persona evidence
          PersonaCatalogVersion = personaCatalogVersion evidence
          Origin = origin evidence }

    let private inputView (input: ParticipantIdentityInput) : obj =
        createObj
            [ "name" ==> input.SelectedAgent
              "role" ==> roleView input.Role
              "persona" ==> input.Persona
              "catalogVersion" ==> input.PersonaCatalogVersion
              "origin" ==> originLabel input.Origin ]

    let private evidenceView (evidence: ParticipantIdentityEvidence) : obj = evidence |> toInput |> inputView

    let private kindOfLabel (label: obj) : ParticipantKind =
        if isNull label then
            Bookkeeper
        else
            roleOfLabel label
            |> Option.map ManagedRole
            |> Option.defaultWith (fun () ->
                failwithf "wanxiangli/identity returned an undecodable role %s" (unbox<string> label))

    let private evidenceOfView (view: obj) : ParticipantIdentityEvidence =
        { Identity =
            { SelectedAgent = PersonaName(unbox<string> view?name)
              Kind = kindOfLabel view?role
              Persona = unbox<string> view?persona
              PersonaCatalogVersion = PersonaCatalogVersion(unbox<int> view?catalogVersion)
              Origin = originOfLabel view?origin } }

    let private errorOfView (view: obj) : ParticipantIdentityError =
        let text (value: obj) = unbox<string> value
        let number (value: obj) = unbox<int> value

        match unbox<string> view?error with
        | "BlankParticipantName" -> ParticipantIdentityError.BlankParticipantName
        | "LegacyParticipantName" -> ParticipantIdentityError.LegacyParticipantName(text view?detail)
        | "MalformedParticipantName" -> ParticipantIdentityError.MalformedParticipantName(text view?detail)
        | "UnknownParticipantName" -> ParticipantIdentityError.UnknownParticipantName(text view?detail)
        | "UnsupportedPersonaCatalogVersion" ->
            ParticipantIdentityError.UnsupportedPersonaCatalogVersion(number view?detail)
        | "RoleMismatch" -> ParticipantIdentityError.RoleMismatch(roleOfLabel view?expected, roleOfLabel view?actual)
        | "BlankPersona" -> ParticipantIdentityError.BlankPersona
        | "PersonaMismatch" -> ParticipantIdentityError.PersonaMismatch(text view?expected, text view?actual)
        | "OriginMismatch" ->
            ParticipantIdentityError.OriginMismatch(originOfLabel view?expected, originOfLabel view?actual)
        | "OwnerRequired" -> ParticipantIdentityError.OwnerRequired
        | "OwnerPersonaMismatch" -> ParticipantIdentityError.OwnerPersonaMismatch(text view?expected, text view?actual)
        | "OwnerCatalogVersionMismatch" ->
            ParticipantIdentityError.OwnerCatalogVersionMismatch(number view?expected, number view?actual)
        | other -> failwithf "wanxiangli/identity returned an unexpected identity error %s" other

    let private resultOf (view: obj) : Result<ParticipantIdentityEvidence, ParticipantIdentityError> =
        if unbox<bool> view?ok = true then
            Ok(evidenceOfView view?evidence)
        else
            Error(errorOfView view)

    let resolveAtRoot (canonicalManagedName: string) : Result<ParticipantIdentityEvidence, ParticipantIdentityError> =
        library?resolveAtRoot canonicalManagedName |> resultOf

    let inheritFromOwner
        (canonicalManagedName: string)
        (owner: ParticipantIdentityEvidence)
        : Result<ParticipantIdentityEvidence, ParticipantIdentityError> =
        library?inheritFromOwner (canonicalManagedName, evidenceView owner) |> resultOf

    let rehydrate
        (ownerOption: ParticipantIdentityEvidence option)
        (input: ParticipantIdentityInput)
        : Result<ParticipantIdentityEvidence, ParticipantIdentityError> =
        let owner =
            match ownerOption with
            | Some evidence -> evidenceView evidence
            | None -> null

        library?rehydrate (owner, inputView input) |> resultOf

    let fromInput (input: ParticipantIdentityInput) : Result<ParticipantIdentityEvidence, ParticipantIdentityError> =
        library?restoreRecorded (inputView input) |> resultOf

    let legacyAgentOwnerRootUnprovableMessage =
        "legacy AuthorityRootAccepted v1 AgentOwnerRoot cannot prove participant identity"

    /// Host-side adapter for the legacy AuthorityRootAccepted v1 journal shape.
    /// A v1 HumanRoot is exactly a V1 root selection, so it is resolved by the
    /// library; v1 AgentOwnerRoot never carried provable identity evidence.
    let upgradeLegacyV1Root
        (input: LegacyAuthorityRootIdentityV1Input)
        : Result<ParticipantIdentityEvidence, ParticipantIdentityError> =
        match input.AuthorityKind with
        | "AgentOwnerRoot" ->
            Error(ParticipantIdentityError.UnprovableLegacyAuthorityIdentity legacyAgentOwnerRootUnprovableMessage)
        | "HumanRoot" -> resolveAtRoot input.SelectedAgent
        | authorityKind -> Error(ParticipantIdentityError.UnsupportedLegacyAuthorityKind authorityKind)
