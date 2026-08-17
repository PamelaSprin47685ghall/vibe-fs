namespace Wanxiangshu.Interaction.Authority

open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Persona

/// JS-native lifecycle surface for authority provenance and run state.
/// Typed profiles, claims, identities, and maps stay private to this boundary.
module RuntimeSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private arrayOf (value: obj) : obj array =
        if isNull value then [||] else unbox<obj array> value

    let private rootKindResult (value: obj) : Result<PromptAuthority.RootAuthorityKind, string> =
        match text value with
        | "HumanRoot" -> Ok PromptAuthority.RootAuthorityKind.HumanRoot
        | "AgentOwnerRoot" -> Ok PromptAuthority.RootAuthorityKind.AgentOwnerRoot
        | unknown -> Error(sprintf "unknown authority root kind: %s" unknown)

    let private rootKindOf (value: obj) =
        match rootKindResult value with
        | Ok kind -> kind
        | Error error -> invalidArg "authorityKind" error

    let private continuationKindResult (value: string) =
        match PromptAuthority.tryParseContinuationKind value with
        | Some kind -> Ok kind
        | None -> Error(sprintf "unknown ContinuationKind: %s" value)

    let private continuationKindOf (value: string) =
        match continuationKindResult value with
        | Ok kind -> kind
        | Error error -> invalidArg "kind" error

    let private roleResult (value: obj) : Result<Role, string> =
        match Roles.tryParseRole (text value) with
        | Some role -> Ok role
        | None -> Error(sprintf "unknown role: %s" (text value))

    let private tierResult (value: obj) : Result<AgentTier, string> =
        match Roles.tryParseTier (text value) with
        | Some tier -> Ok tier
        | None -> Error(sprintf "unknown tier: %s" (text value))

    let private profileResult (value: obj) : Result<PromptAuthority.AuthorityExecutionProfile, string> =
        match rootKindResult value?authorityKind, roleResult value?canonicalRole, tierResult value?selectedTier with
        | Ok authorityKind, Ok canonicalRole, Ok selectedTier ->
            Ok
                { SessionId = SessionId.create (text value?session)
                  LogicalRunId = LogicalRunId.create (text value?logicalRun)
                  AuthorityRootUserMessageId = AuthorityRootUserMessageId.create (text value?authorityRoot)
                  AuthorityKind = authorityKind
                  SelectedAgent = text value?selectedAgent
                  PeerAgent = text value?peerAgent
                  CanonicalRole = canonicalRole
                  SelectedTier = selectedTier }
        | Error error, _, _
        | _, Error error, _
        | _, _, Error error -> Error error

    let private profileOf (value: obj) : PromptAuthority.AuthorityExecutionProfile =
        match profileResult value with
        | Ok profile -> profile
        | Error error -> invalidArg "profile" error

    let private profileToJs (profile: PromptAuthority.AuthorityExecutionProfile) : obj =
        box
            {| session = SessionId.value profile.SessionId
               logicalRun = LogicalRunId.value profile.LogicalRunId
               authorityRoot = AuthorityRootUserMessageId.value profile.AuthorityRootUserMessageId
               authorityKind =
                match profile.AuthorityKind with
                | PromptAuthority.RootAuthorityKind.HumanRoot -> "HumanRoot"
                | PromptAuthority.RootAuthorityKind.AgentOwnerRoot -> "AgentOwnerRoot"
               selectedAgent = profile.SelectedAgent
               peerAgent = profile.PeerAgent
               canonicalRole = Roles.roleLabel profile.CanonicalRole
               selectedTier = Roles.wireTierLabel profile.SelectedTier |}

    let private optionalString (value: obj) : string option =
        if isNull value then None else Some(text value)

    let private originName (origin: PromptAuthority.PromptOrigin) : string =
        match origin with
        | PromptAuthority.PromptOrigin.AuthorityRoot _ -> "AuthorityRoot"
        | PromptAuthority.PromptOrigin.Continuation _ -> "Continuation"
        | PromptAuthority.PromptOrigin.HostInternal -> "HostInternal"
        | PromptAuthority.PromptOrigin.UnknownOrigin -> "UnknownOrigin"

    let private originOf (kind: string) (label: string) : PromptAuthority.PromptOrigin =
        match kind with
        | "Continuation" -> PromptAuthority.PromptOrigin.Continuation(continuationKindOf label)
        | "AuthorityRoot" -> PromptAuthority.PromptOrigin.AuthorityRoot(rootKindOf (box label))
        | "HostInternal" -> PromptAuthority.PromptOrigin.HostInternal
        | _ -> PromptAuthority.PromptOrigin.UnknownOrigin

    let private claimToJs (claim: PromptAuthority.PromptClaim) : obj =
        box
            {| promptKey = PromptKey.value claim.PromptKey
               session = SessionId.value claim.SessionId
               origin = originName claim.Origin
               originLabel = PromptAuthority.originLabel claim.Origin
               logicalRun = claim.LogicalRunId |> Option.map LogicalRunId.value |> Option.defaultValue null
               authorityRoot =
                claim.AuthorityRootUserMessageId
                |> Option.map AuthorityRootUserMessageId.value
                |> Option.defaultValue null
               effectiveAgent = claim.EffectiveAgent |> Option.defaultValue null
               payloadDigest = claim.PayloadDigest
               receipt = claim.Receipt |> Option.map TransportReceipt.value |> Option.defaultValue null
               claimedAtRuntimeStartCount = claim.ClaimedAtRuntimeStartCount |}

    let private claimOf (value: obj) : PromptAuthority.PromptClaim =
        let kind = text value?origin
        let label = text value?originLabel

        { PromptKey = PromptKey.create (text value?promptKey)
          SessionId = SessionId.create (text value?session)
          Origin = originOf kind label
          LogicalRunId = optionalString value?logicalRun |> Option.map LogicalRunId.create
          AuthorityRootUserMessageId =
            optionalString value?authorityRoot
            |> Option.map AuthorityRootUserMessageId.create
          EffectiveAgent = optionalString value?effectiveAgent
          PayloadDigest = text value?payloadDigest
          Receipt = optionalString value?receipt |> Option.map TransportReceipt.create
          ClaimedAtRuntimeStartCount = 0 }

    let private profileOption (value: obj) =
        if isNull value then None else Some(profileOf value)

    let private projectionValidation (value: obj) : Result<unit, string> =
        if isNull value then
            Ok()
        else
            let validateProfile value =
                if isNull value then
                    Ok()
                else
                    profileResult value |> Result.map (fun _ -> ())

            match validateProfile value?lastAuthorityProfile, validateProfile value?activeLogicalRun with
            | Ok(), Ok() -> Ok()
            | Error error, _
            | _, Error error -> Error error

    let private projectionOf (value: obj) : PromptAuthority.PromptAuthorityProjection =
        if isNull value then
            PromptAuthority.empty
        else
            let pending =
                arrayOf value?pendingClaims
                |> Array.fold
                    (fun current claim ->
                        let typed = claimOf claim
                        Map.add typed.PromptKey typed current)
                    Map.empty

            let accepted =
                arrayOf value?acceptedContinuations
                |> Array.fold
                    (fun current item ->
                        Map.add
                            (PhysicalUserMessageId.create (text item?physical))
                            (continuationKindOf (text item?kind))
                            current)
                    Map.empty

            let sequences =
                arrayOf value?claimSequences
                |> Array.fold (fun current item -> Map.add (text item?scope) (int (text item?count)) current) Map.empty

            { LastAuthorityProfile = profileOption value?lastAuthorityProfile
              ActiveLogicalRun = profileOption value?activeLogicalRun
              PendingClaims = pending
              AcceptedDispatches = Map.empty
              AcceptedContinuationIds = accepted
              ClaimSequences = sequences }

    let private projectionToJs (projection: PromptAuthority.PromptAuthorityProjection) : obj =
        box
            {| lastAuthorityProfile =
                projection.LastAuthorityProfile
                |> Option.map profileToJs
                |> Option.defaultValue null
               activeLogicalRun =
                projection.ActiveLogicalRun
                |> Option.map profileToJs
                |> Option.defaultValue null
               pendingClaims =
                projection.PendingClaims
                |> Map.toList
                |> List.map (snd >> claimToJs)
                |> List.toArray
               acceptedContinuations =
                projection.AcceptedContinuationIds
                |> Map.toList
                |> List.map (fun (physical, kind) ->
                    box
                        {| physical = PhysicalUserMessageId.value physical
                           kind = PromptAuthority.originLabel (PromptAuthority.PromptOrigin.Continuation kind) |})
                |> List.toArray
               claimSequences =
                projection.ClaimSequences
                |> Map.toList
                |> List.map (fun (scope, count) -> box {| scope = scope; count = count |})
                |> List.toArray |}

    let empty: obj = projectionToJs PromptAuthority.empty

    let promotePhysical (physical: string) : string =
        PhysicalUserMessageId.promoteToAuthorityRoot (PhysicalUserMessageId.create physical)
        |> AuthorityRootUserMessageId.value

    let transportReceiptShape (receipt: string) : bool =
        TransportReceipt.isAdmissionShaped (TransportReceipt.create receipt)

    let createAuthorityRoot
        (hash: string -> string)
        (runtime: string)
        (session: string)
        (kind: string)
        (physical: string)
        (agent: string)
        : obj =
        match rootKindResult (box kind) with
        | Error error ->
            box
                {| ok = false
                   value = null
                   error = error |}
        | Ok authorityKind ->
            match
                PromptAuthorityRun.createAuthorityRoot
                    hash
                    (RuntimeId.create runtime)
                    (SessionId.create session)
                    authorityKind
                    (PhysicalUserMessageId.create physical)
                    agent
            with
            | Ok profile ->
                box
                    {| ok = true
                       value = profileToJs profile
                       error = "" |}
            | Error error ->
                box
                    {| ok = false
                       value = null
                       error = error |}

    let parseAgentName (agent: string) : obj =
        match PromptAuthority.parseAgentNameTyped agent with
        | Ok parsed ->
            box
                {| ok = true
                   value =
                    box
                        {| name = parsed.Name
                           role = Roles.roleLabel parsed.Role
                           tier = Roles.wireTierLabel parsed.Tier
                           peer = parsed.PeerName |}
                   error = null |}
        | Error rejection ->
            let kind, message =
                match rejection with
                | PromptAuthority.AgentNameRejection.LegacyAgentName name ->
                    "LegacyAgentName", ManagedAgentCatalog.formatLegacyNameNotSupported name
                | PromptAuthority.AgentNameRejection.UnknownManagedAgent _ ->
                    "UnknownManagedAgent", "Unknown tier or role. Use fast-* or deep-* ."
                | PromptAuthority.AgentNameRejection.Malformed _ -> "Malformed", "Expected fast-ROLE or deep-ROLE."

            box
                {| ok = false
                   value = null
                   error = box {| kind = kind; message = message |} |}

    let registerAuthority (profile: obj) (projection: obj) : obj =
        match profileResult profile, projectionValidation projection with
        | Ok profile, Ok() ->
            PromptAuthorityRun.registerAuthority profile (projectionOf projection)
            |> projectionToJs
        | Error error, _
        | _, Error error -> box {| ok = false; error = error |}

    let claimContinuation
        (promptKey: string)
        (session: string)
        (kind: string)
        (profile: obj)
        (effectiveAgent: string)
        (payloadDigest: string)
        : obj =
        match continuationKindResult kind, profileResult profile with
        | Ok continuationKind, Ok profile ->
            PromptAuthorityRun.claimContinuation
                (PromptKey.create promptKey)
                (SessionId.create session)
                continuationKind
                profile
                effectiveAgent
                payloadDigest
            |> claimToJs
        | Error error, _
        | _, Error error -> box {| ok = false; error = error |}

    let claimAgentOwnerRoot (promptKey: string) (session: string) (payloadDigest: string) (agent: string) : obj =
        match
            PromptAuthorityRun.claimAgentOwnerRoot
                (PromptKey.create promptKey)
                (SessionId.create session)
                payloadDigest
                agent
        with
        | Ok claim ->
            box
                {| ok = true
                   value = claimToJs claim
                   error = "" |}
        | Error error ->
            box
                {| ok = false
                   value = null
                   error = error |}

    let registerClaim (claim: obj) (projection: obj) : obj =
        match projectionValidation projection with
        | Error error -> box {| ok = false; error = error |}
        | Ok() ->
            PromptAuthorityRun.registerClaim (claimOf claim) (projectionOf projection)
            |> projectionToJs

    let acceptClaim (promptKey: string) (physical: string) (projection: obj) : obj =
        match projectionValidation projection with
        | Error error -> box {| ok = false; error = error |}
        | Ok() ->
            PromptAuthorityRun.acceptClaim
                (PromptKey.create promptKey)
                (PhysicalUserMessageId.create physical)
                (projectionOf projection)
            |> projectionToJs

    let abandonClaim (promptKey: string) (projection: obj) : obj =
        match projectionValidation projection with
        | Error error -> box {| ok = false; error = error |}
        | Ok() ->
            PromptAuthorityRun.abandonClaim (PromptKey.create promptKey) (projectionOf projection)
            |> projectionToJs

    let nextClaimSequence (scope: string) (projection: obj) : int =
        match projectionValidation projection with
        | Error _ -> 0
        | Ok() -> PromptAuthority.nextClaimSequence scope (projectionOf projection)

    let submitClaim (promptKey: string) (receipt: string) (projection: obj) : obj =
        match projectionValidation projection with
        | Error error -> box {| ok = false; error = error |}
        | Ok() ->
            PromptAuthorityRun.submitClaim
                (PromptKey.create promptKey)
                (TransportReceipt.create receipt)
                (projectionOf projection)
            |> projectionToJs

    let claimScopeDigest (session: string) (logicalRun: obj) (origin: obj) (payloadDigest: string) : string =
        PromptAuthority.claimScopeDigest
            (SessionId.create session)
            (optionalString logicalRun |> Option.map LogicalRunId.create)
            (originOf (text origin?kind) (text origin?label))
            payloadDigest

    let derivePromptKey
        (hash: string -> string)
        (session: string)
        (logicalRun: obj)
        (authorityRoot: obj)
        (origin: obj)
        (effectiveAgent: obj)
        (payloadDigest: string)
        (claimSequence: int)
        : string =
        PromptAuthority.derivePromptKey
            hash
            (SessionId.create session)
            (optionalString logicalRun |> Option.map LogicalRunId.create)
            (optionalString authorityRoot |> Option.map AuthorityRootUserMessageId.create)
            (originOf (text origin?kind) (text origin?label))
            (optionalString effectiveAgent)
            payloadDigest
            claimSequence
        |> PromptKey.value

    let closeAuthority (logicalRun: string) (authorityRoot: string) (projection: obj) : obj =
        match projectionValidation projection with
        | Error error ->
            box
                {| ok = false
                   value = null
                   error = error |}
        | Ok() ->
            match
                PromptAuthorityRun.closeAuthority
                    (LogicalRunId.create logicalRun)
                    (AuthorityRootUserMessageId.create authorityRoot)
                    (projectionOf projection)
            with
            | Ok closed ->
                box
                    {| ok = true
                       value = projectionToJs closed
                       error = "" |}
            | Error error ->
                box
                    {| ok = false
                       value = null
                       error = error |}

    let closeCompletedHumanRootManager (projection: obj) : obj =
        match projectionValidation projection with
        | Error error -> box {| ok = false; error = error |}
        | Ok() ->
            let typed = projectionOf projection

            match typed.ActiveLogicalRun with
            | Some profile when profile.AuthorityKind = PromptAuthority.RootAuthorityKind.HumanRoot ->
                match
                    PromptAuthorityRun.closeAuthority profile.LogicalRunId profile.AuthorityRootUserMessageId typed
                with
                | Ok closed -> projectionToJs closed
                | Error _ -> projectionToJs typed
            | _ -> projectionToJs typed

    let resolveKnownOrigin (physical: string) (promptKey: string) (hostCompaction: bool) (projection: obj) : string =
        match projectionValidation projection with
        | Error _ -> "UnknownOrigin"
        | Ok() ->
            PromptAuthorityRun.resolveKnownOrigin
                (PhysicalUserMessageId.create physical)
                (if System.String.IsNullOrWhiteSpace promptKey then
                     None
                 else
                     Some(PromptKey.create promptKey))
                hostCompaction
                (projectionOf projection)
            |> originName

    let stableLogicalRunId (hash: string -> string) (runtime: string) (session: string) (physical: string) : string =
        PromptAuthority.stableLogicalRunId
            hash
            (RuntimeId.create runtime)
            (SessionId.create session)
            (PhysicalUserMessageId.promoteToAuthorityRoot (PhysicalUserMessageId.create physical))
        |> LogicalRunId.value

    let originForContinuation (kind: string) : obj =
        let value = PromptAuthority.PromptOrigin.Continuation(continuationKindOf kind)

        box
            {| kind = originName value
               label = PromptAuthority.originLabel value |}

    let tryParseContinuationKind (kind: string) : obj =
        PromptAuthority.tryParseContinuationKind kind
        |> Option.map (fun value ->
            box {| kind = PromptAuthority.originLabel (PromptAuthority.PromptOrigin.Continuation value) |})
        |> Option.defaultValue null

    let repairPayloadDigest (request: string) (terminal: string) (kind: string) : string =
        PromptAuthority.repairPayloadDigest (BloggerRequestId.create request) (ProviderRunIdentity.create terminal) kind

    let repairAlreadyClaimed
        (session: string)
        (logicalRun: string)
        (request: string)
        (terminal: string)
        (kind: string)
        (projection: obj)
        : bool =
        PromptAuthority.repairAlreadyClaimed
            (SessionId.create session)
            (LogicalRunId.create logicalRun)
            (BloggerRequestId.create request)
            (ProviderRunIdentity.create terminal)
            kind
            (projectionOf projection)

    let repairFamilyAlreadyClaimed (session: string) (logicalRun: string) (kind: string) (projection: obj) : bool =
        PromptAuthority.repairFamilyAlreadyClaimed
            (SessionId.create session)
            (LogicalRunId.create logicalRun)
            kind
            (projectionOf projection)

    let idleAlreadyClaimed
        (session: string)
        (logicalRun: string)
        (life: string)
        (condition: string)
        (providerRun: string)
        (projection: obj)
        : bool =
        PromptAuthority.idleAlreadyClaimed
            (SessionId.create session)
            (LogicalRunId.create logicalRun)
            (ManagerLifeId.create life)
            condition
            (ProviderRunIdentity.create providerRun)
            (projectionOf projection)
