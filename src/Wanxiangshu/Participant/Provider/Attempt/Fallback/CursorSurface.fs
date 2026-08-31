namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal

/// JSON/opaque owner boundary for the pure fallback cursor and its durable fold.
/// Cursor/projection identities and journal facts never cross as Fable records,
/// maps, lists or union cases; projection handles remain opaque between calls.
[<RequireQualifiedAccess>]
module CursorSurface =

    let defaultAutoRecoveryBudget = AgentPairCursor.DefaultAutoRecoveryBudget

    type private ProjectionHandle(projection: FallbackProjection) =
        member _.Value = projection

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    [<Emit("new Date(0)")>]
    let private foldObservation () : ObservedAt = jsNative

    let private field (value: obj) (name: string) : obj =
        if isNullish value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

    let private text (value: obj) : string =
        if isNullish value then "" else string value

    let private intValue (value: obj) : int =
        if isNullish value then 0 else int (text value)

    let private optionalText (value: obj) : string option =
        if isNullish value then None else Some(text value)

    let private requiredText argument value =
        let parsed = text value

        if String.IsNullOrWhiteSpace parsed then
            invalidArg argument $"missing {argument}"

        parsed

    let private optionObj (value: 'a option) : obj =
        match value with
        | None -> null
        | Some item -> box item

    let private firstField (value: obj) (names: string list) : obj =
        names
        |> List.tryPick (fun name ->
            let item = field value name
            if isNullish item then None else Some item)
        |> Option.defaultValue null

    let private participantIdentityOf (value: obj) : ParticipantIdentityEvidence =
        if isNullish value then
            invalidArg "identitySeed.participantIdentity" "missing identitySeed.participantIdentity"

        let roleLabel =
            requiredText "identitySeed.participantIdentity.canonicalRole" (field value "canonicalRole")

        let role =
            Roles.tryParseRole roleLabel
            |> Option.defaultWith (fun () -> invalidArg "participantIdentity" $"unknown role '{roleLabel}'")
            |> Some

        let tierLabel =
            requiredText "identitySeed.participantIdentity.selectedTier" (field value "selectedTier")

        let initialTier =
            Roles.tryParseTier tierLabel
            |> Option.defaultWith (fun () -> invalidArg "participantIdentity" $"unknown tier '{tierLabel}'")

        let originLabel =
            requiredText "identitySeed.participantIdentity.origin" (field value "origin")

        let origin =
            match originLabel with
            | "ResolvedAtRoot" -> PersonaOrigin.ResolvedAtRoot
            | "InheritedFromOwner" -> PersonaOrigin.InheritedFromOwner
            | _ -> invalidArg "participantIdentity" $"unknown persona origin '{originLabel}'"

        { SelectedAgent = requiredText "identitySeed.participantIdentity.selectedAgent" (field value "selectedAgent")
          PeerAgent = requiredText "identitySeed.participantIdentity.peerAgent" (field value "peerAgent")
          Role = role
          InitialTier = initialTier
          Persona = requiredText "identitySeed.participantIdentity.persona" (field value "persona")
          PersonaCatalogVersion = intValue (field value "personaCatalogVersion")
          Origin = origin }
        |> ParticipantIdentity.fromInput
        |> Result.defaultWith (fun error -> invalidArg "participantIdentity" $"invalid participant identity: {error}")

    let private identitySeedOf (value: obj) : PromptAuthority.IdentitySeed =
        if isNullish value then
            invalidArg "identitySeed" "missing identitySeed"

        let participantIdentity = participantIdentityOf (field value "participantIdentity")
        let identityInput = ParticipantIdentity.toInput participantIdentity
        let ownerSession = field value "ownerSession"
        let ownerLogicalRun = field value "ownerLogicalRun"
        let ownerAuthorityRoot = field value "ownerAuthorityRoot"

        let seedInput =
            match requiredText "identitySeed.kind" (field value "kind") with
            | "RootSelection" ->
                if
                    not (isNullish ownerSession)
                    || not (isNullish ownerLogicalRun)
                    || not (isNullish ownerAuthorityRoot)
                then
                    invalidArg "identitySeed" "RootSelection cannot carry inherited owner evidence"

                PromptAuthority.IdentitySeedInput.RootSelectionInput identityInput
            | "InheritedFromOwner" ->
                PromptAuthority.IdentitySeedInput.InheritedFromOwnerInput
                    { OwnerSessionId = SessionId.create (requiredText "identitySeed.ownerSession" ownerSession)
                      OwnerLogicalRunId =
                        LogicalRunId.create (requiredText "identitySeed.ownerLogicalRun" ownerLogicalRun)
                      OwnerAuthorityRootUserMessageId =
                        AuthorityRootUserMessageId.create (
                            requiredText "identitySeed.ownerAuthorityRoot" ownerAuthorityRoot
                        )
                      ParticipantIdentity = identityInput }
            | kind -> invalidArg "identitySeed" $"unknown identity seed kind '{kind}'"

        PromptAuthority.rehydrateIdentitySeed seedInput
        |> Result.defaultWith (fun error -> invalidArg "identitySeed" $"invalid identity seed: {error}")

    let private participantIdentityView (identity: ParticipantIdentityEvidence) : obj =
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

    let private identitySeedView (seed: PromptAuthority.IdentitySeed) : obj =
        let participantIdentity =
            PromptAuthority.identitySeedParticipantIdentity seed |> participantIdentityView

        match PromptAuthority.identitySeedOwner seed with
        | None ->
            box
                {| kind = "RootSelection"
                   ownerSession = null
                   ownerLogicalRun = null
                   ownerAuthorityRoot = null
                   participantIdentity = participantIdentity |}
        | Some(ownerSession, ownerLogicalRun, ownerAuthorityRoot) ->
            box
                {| kind = "InheritedFromOwner"
                   ownerSession = SessionId.value ownerSession
                   ownerLogicalRun = LogicalRunId.value ownerLogicalRun
                   ownerAuthorityRoot = AuthorityRootUserMessageId.value ownerAuthorityRoot
                   participantIdentity = participantIdentity |}

    let private offsetOf (value: obj) : AgentPairCursor.FallbackOffset =
        match intValue value with
        | 0 -> AgentPairCursor.FallbackOffset.Fork0
        | 1 -> AgentPairCursor.FallbackOffset.Fork1
        | 2 -> AgentPairCursor.FallbackOffset.Fork2
        | 3 -> AgentPairCursor.FallbackOffset.Fork3
        | _ -> invalidArg "offset" "fallback offset must be in 0..3"

    let private offsetValue (offset: AgentPairCursor.FallbackOffset) : int =
        int (AgentPairCursor.FallbackOffsetCodec.toByte offset)

    let private cursorOf (value: obj) : AgentPairCursor.FallbackCursor =
        if isNullish value then
            AgentPairCursor.initial
        else
            { Offset = offsetOf (firstField value [ "offset"; "Offset" ])
              ConsecutiveFailureCount = intValue (firstField value [ "failures"; "ConsecutiveFailureCount" ]) }

    let private cursorView (cursor: AgentPairCursor.FallbackCursor) : obj =
        box
            {| offset = offsetValue cursor.Offset
               failures = cursor.ConsecutiveFailureCount |}

    let private pairOf (value: obj) : AgentPairCursor.AuthorityAgentPair =
        { SelectedAgent = text (firstField value [ "selectedAgent"; "SelectedAgent" ])
          PeerAgent = text (firstField value [ "peerAgent"; "PeerAgent" ]) }

    let private identityOf (value: obj) : FallbackAttemptIdentity =
        { SessionId = SessionId.create (text (firstField value [ "session"; "SessionId" ]))
          LogicalRunId = LogicalRunId.create (text (firstField value [ "run"; "logicalRun"; "LogicalRunId" ]))
          AuthorityRootUserMessageId =
            AuthorityRootUserMessageId.create (
                text (firstField value [ "root"; "authorityRoot"; "AuthorityRootUserMessageId" ])
            )
          ProviderRun = ProviderRunIdentity.create (text (firstField value [ "attempt"; "ProviderRun" ])) }

    let private identityView (identity: FallbackAttemptIdentity) : obj =
        box
            {| session = SessionId.value identity.SessionId
               run = LogicalRunId.value identity.LogicalRunId
               root = AuthorityRootUserMessageId.value identity.AuthorityRootUserMessageId
               attempt = ProviderRunIdentity.value identity.ProviderRun |}

    let private projectionView (projection: FallbackProjection) : obj =
        box
            {| logicalRun = LogicalRunId.value projection.LogicalRunId
               authorityRoot = AuthorityRootUserMessageId.value projection.AuthorityRootUserMessageId
               offset = offsetValue projection.Cursor.Offset
               failures = projection.Cursor.ConsecutiveFailureCount
               dedupeKeys = List.length projection.RecentFailureKeys
               exhausted = projection.Exhausted |}

    let private projectionHandleView (projection: FallbackProjection) : obj =
        box
            {| logicalRun = LogicalRunId.value projection.LogicalRunId
               authorityRoot = AuthorityRootUserMessageId.value projection.AuthorityRootUserMessageId
               offset = offsetValue projection.Cursor.Offset
               failures = projection.Cursor.ConsecutiveFailureCount
               dedupeKeys = List.length projection.RecentFailureKeys
               exhausted = projection.Exhausted
               handle = box (ProjectionHandle projection) |}

    let private outcomeName outcome =
        match outcome with
        | ConfirmedFailureOutcome.RecoveryAdvanced _ -> "Advanced"
        | ConfirmedFailureOutcome.RecoveryExhausted -> "Exhausted"
        | ConfirmedFailureOutcome.AlreadyRecorded -> "AlreadyRecorded"
        | ConfirmedFailureOutcome.NoActiveRun -> "NoActiveRun"

    let recordConfirmedFailure
        (handle: Wanxiangshu.Persistence.Journal.JournalHandle)
        (budget: int)
        (session: string)
        (providerRun: string)
        (reason: string)
        : Task<obj> =
        task {
            let sessionId = SessionId.create session
            let providerRunId = ProviderRunIdentity.create providerRun

            let! result =
                match FallbackEvidence.tryCurrentState sessionId (AgentJournal.snapshot handle.Journal) with
                | None -> Task.FromResult(Ok ConfirmedFailureOutcome.NoActiveRun)
                | Some _ when budget <> defaultAutoRecoveryBudget ->
                    Task.FromResult(Error "provider recovery budget must equal the declared default")
                | Some current ->
                    let available =
                        if FallbackProjection.mayContinue defaultAutoRecoveryBudget current then
                            ProviderRecoveryBudget.Available
                        else
                            ProviderRecoveryBudget.Exhausted

                    let decision =
                        ExecutionFailurePolicy.decide
                            { Failure = ExecutionFailure.ProviderTransient
                              Lifecycle = DurableExecutionLifecycle.ProviderStarted
                              ExecutionKey =
                                { SessionId = sessionId
                                  PhysicalUserMessageId = PhysicalUserMessageId.create ("proof-" + providerRun) }
                              Capacity = CapacityOwnership.NoCapacityFence
                              Provider =
                                { LogicalRun = current.LogicalRunId
                                  ProviderRun = providerRunId
                                  RequestKind = ProviderRequestKind.WorkMain
                                  RetryBudget = ProviderRecoveryBudget.Exhausted
                                  FallbackBudget = available
                                  Breaker = ProviderBreakerState.Closed } }

                    match decision.Fallback with
                    | FallbackDecision.AdvanceFallback authorization ->
                        FallbackLedger.recordAuthorizedFailure handle.Journal sessionId authorization reason
                    | FallbackDecision.NoFallback -> Task.FromResult(Ok ConfirmedFailureOutcome.AlreadyRecorded)

            return
                match result with
                | Ok outcome ->
                    box
                        {| ok = true
                           outcome = outcomeName outcome |}
                | Error error -> box {| ok = false; error = error |}
        }

    let snapshot (handle: Wanxiangshu.Persistence.Journal.JournalHandle) (session: string) : obj =
        match FallbackEvidence.tryCurrentState (SessionId.create session) (AgentJournal.snapshot handle.Journal) with
        | None -> null
        | Some current ->
            box
                {| offset = AgentPairCursor.FallbackOffsetCodec.toByte current.Cursor.Offset
                   failures = current.Cursor.ConsecutiveFailureCount
                   exhausted = current.Exhausted |}

    let private projectionOf (value: obj) : FallbackProjection =
        let handle = field value "handle"

        if not (isNullish handle) then
            (unbox<ProjectionHandle> handle).Value
        else
            { LogicalRunId = LogicalRunId.create (text (field value "logicalRun"))
              AuthorityRootUserMessageId = AuthorityRootUserMessageId.create (text (field value "authorityRoot"))
              Cursor =
                { Offset = offsetOf (field value "offset")
                  ConsecutiveFailureCount = intValue (field value "failures") }
              RecentFailureKeys = []
              LastTransitionWasSuccess = false
              Exhausted =
                match field value "exhausted" with
                | value when isNullish value -> false
                | value -> unbox<bool> value }

    let private rejectionName (rejection: FallbackAdvanceRejection) : string =
        match rejection with
        | FallbackAdvanceRejection.AlreadyObserved -> "AlreadyObserved"
        | FallbackAdvanceRejection.AlreadyExhausted -> "AlreadyExhausted"
        | FallbackAdvanceRejection.DifferentRun -> "DifferentRun"
        | FallbackAdvanceRejection.NoCursor -> "NoCursor"
        | FallbackAdvanceRejection.InvalidTransition -> "InvalidTransition"
        | FallbackAdvanceRejection.InvalidFallbackOffset _ -> "InvalidFallbackOffset"

    let private applyAdvance (identity: obj) (previousOffset: int) (nextOffset: int) (count: int) (current: obj) : obj =
        match
            FallbackProjection.applyAdvance
                (identityOf identity)
                (offsetOf (box previousOffset))
                (offsetOf (box nextOffset))
                count
                (projectionOf current)
        with
        | Ok projection ->
            box
                {| ok = true
                   value = projectionHandleView projection |}
        | Error rejection ->
            box
                {| ok = false
                   error = rejectionName rejection |}

    /// Pure cursor API. Every method accepts/returns JSON values; only the
    /// identity key helper intentionally consumes an opaque semantic identity.
    let cursor =
        box
            {| initial = cursorView AgentPairCursor.initial
               atOffset = (fun offset -> cursorView (AgentPairCursor.atOffset (offsetOf (box offset))))
               advance = (fun offset -> offsetValue (AgentPairCursor.advance (offsetOf (box offset))))
               recordFailure = (fun value -> cursorOf value |> AgentPairCursor.recordFailure |> cursorView)
               recordSuccess = (fun value -> cursorOf value |> AgentPairCursor.recordSuccess |> cursorView)
               side = (fun offset -> AgentPairCursor.side (offsetOf (box offset)) |> string)
               sideSequence = (fun count -> AgentPairCursor.sideSequence count |> List.map string |> List.toArray)
               effectiveAgent = (fun pair value -> AgentPairCursor.effectiveAgent (pairOf pair) (cursorOf value))
               isValidAdvance =
                (fun previousOffset nextOffset previousCount nextCount ->
                    AgentPairCursor.isValidAdvance
                        (offsetOf (box previousOffset))
                        (offsetOf (box nextOffset))
                        previousCount
                        nextCount)
               isRecoverySlot = (fun offset -> AgentPairCursor.isRecoverySlot (offsetOf (box offset)))
               recoveryVerdict =
                (fun budget value ->
                    match AgentPairCursor.recoveryVerdict budget (cursorOf value) with
                    | AgentPairCursor.MayContinue _ -> "MayContinue"
                    | AgentPairCursor.Exhausted _ -> "Exhausted")
               defaultBudget = AgentPairCursor.DefaultAutoRecoveryBudget
               attemptIdentity =
                (fun session logicalRun authorityRoot providerRun ->
                    identityView
                        { SessionId = SessionId.create session
                          LogicalRunId = LogicalRunId.create logicalRun
                          AuthorityRootUserMessageId = AuthorityRootUserMessageId.create authorityRoot
                          ProviderRun = ProviderRunIdentity.create providerRun })
               dedupeKey = (fun value -> identityOf value |> FallbackAttemptIdentity.dedupeKey)
               read = (fun value -> cursorOf value |> cursorView) |}

    /// Durable fallback projection API. The projection state itself is carried
    /// by an opaque handle so its bounded dedupe keys never become public JSON.
    let fallbackProjection =
        box
            {| forAuthority =
                (fun logicalRun authorityRoot ->
                    FallbackProjection.forAuthority
                        (LogicalRunId.create logicalRun)
                        (AuthorityRootUserMessageId.create authorityRoot)
                    |> projectionHandleView)
               applyAdvance =
                (fun identity previous next count current -> applyAdvance identity previous next count current)
               applyExhausted =
                (fun current ->
                    projectionOf current
                    |> FallbackProjection.applyExhausted
                    |> projectionHandleView)
               recordSuccess =
                (fun current -> projectionOf current |> FallbackProjection.recordSuccess |> projectionHandleView)
               mayContinue = (fun budget current -> FallbackProjection.mayContinue budget (projectionOf current))
               read = (fun current -> projectionOf current |> projectionView) |}

    let authorityRootAccepted (value: obj) : obj =
        let authorityKind = requiredText "authorityKind" (field value "authorityKind")
        let identitySeed = identitySeedOf (field value "identitySeed")

        match authorityKind, identitySeed with
        | "HumanRoot", PromptAuthority.IdentitySeed.RootSelection _
        | "AgentOwnerRoot", PromptAuthority.IdentitySeed.InheritedFromOwner _ -> ()
        | "HumanRoot", _ -> invalidArg "identitySeed" "HumanRoot requires a RootSelection identity seed"
        | "AgentOwnerRoot", _ -> invalidArg "identitySeed" "AgentOwnerRoot requires an inherited owner identity seed"
        | kind, _ -> invalidArg "authorityKind" $"unknown authority kind '{kind}'"

        box
            {| kind = "AuthorityRootAccepted"
               schemaVersion = 2
               session = text (field value "session")
               logicalRun = text (field value "logicalRun")
               authorityRoot = text (field value "authorityRoot")
               authorityKind = authorityKind
               identitySeed = identitySeedView identitySeed |}

    let fallbackCursorAdvanced (value: obj) : obj =
        box
            {| kind = "FallbackCursorAdvanced"
               session = text (field value "session")
               logicalRun = text (field value "logicalRun")
               authorityRoot = text (field value "authorityRoot")
               providerRun = text (field value "providerRun")
               previousOffset = intValue (field value "previousOffset")
               nextOffset = intValue (field value "nextOffset")
               consecutiveFailureCount = intValue (field value "consecutiveFailureCount")
               reason =
                match text (field value "reason") with
                | "" -> "provider_error"
                | value -> value |}

    let fallbackExhausted (value: obj) : obj =
        box
            {| kind = "FallbackExhausted"
               session = text (field value "session")
               logicalRun = text (field value "logicalRun")
               authorityRoot = text (field value "authorityRoot")
               finalConsecutiveFailureCount = intValue (field value "finalConsecutiveFailureCount")
               finalOffset = intValue (field value "finalOffset") |}

    let fallbackSucceeded (value: obj) : obj =
        box
            {| kind = "FallbackSucceeded"
               session = text (field value "session")
               logicalRun = text (field value "logicalRun")
               authorityRoot = text (field value "authorityRoot")
               providerRun = text (field value "providerRun") |}

    let envelope (value: obj) : obj =
        box
            {| session = text (field value "session")
               seq = intValue (field value "seq")
               fact = field value "fact"
               providerRun = field value "providerRun" |}

    let private factOf (value: obj) : AgentFact =
        match text (field value "kind") with
        | "AuthorityRootAccepted" ->
            PromptFact.AuthorityRootAccepted
                { SchemaVersion = 2
                  SessionId = SessionId.create (text (field value "session"))
                  LogicalRunId = LogicalRunId.create (text (field value "logicalRun"))
                  AuthorityRootUserMessageId = AuthorityRootUserMessageId.create (text (field value "authorityRoot"))
                  AuthorityKind = text (field value "authorityKind")
                  IdentitySeed = identitySeedOf (field value "identitySeed") }
        | "FallbackCursorAdvanced" ->
            FallbackFact.FallbackCursorAdvanced
                {| SessionId = SessionId.create (text (field value "session"))
                   LogicalRunId = LogicalRunId.create (text (field value "logicalRun"))
                   AuthorityRootUserMessageId = AuthorityRootUserMessageId.create (text (field value "authorityRoot"))
                   ProviderRun = ProviderRunIdentity.create (text (field value "providerRun"))
                   PreviousOffset = byte (intValue (field value "previousOffset"))
                   NextOffset = byte (intValue (field value "nextOffset"))
                   ConsecutiveFailureCount = intValue (field value "consecutiveFailureCount")
                   Reason = text (field value "reason") |}
        | "FallbackExhausted" ->
            FallbackFact.FallbackExhausted
                {| SessionId = SessionId.create (text (field value "session"))
                   LogicalRunId = LogicalRunId.create (text (field value "logicalRun"))
                   AuthorityRootUserMessageId = AuthorityRootUserMessageId.create (text (field value "authorityRoot"))
                   FinalConsecutiveFailureCount = intValue (field value "finalConsecutiveFailureCount")
                   FinalOffset = byte (intValue (field value "finalOffset")) |}
        | "FallbackSucceeded" ->
            FallbackFact.FallbackSucceeded
                {| SessionId = SessionId.create (text (field value "session"))
                   LogicalRunId = LogicalRunId.create (text (field value "logicalRun"))
                   AuthorityRootUserMessageId = AuthorityRootUserMessageId.create (text (field value "authorityRoot"))
                   ProviderRun = ProviderRunIdentity.create (text (field value "providerRun")) |}
        | other -> failwith $"CursorSurface: unsupported fallback fact '{other}'"

    let private envelopeOf (value: obj) : Envelope =
        let session = SessionId.create (text (field value "session"))
        let sequence = int64 (intValue (field value "seq"))

        let providerRun =
            optionalText (field value "providerRun")
            |> Option.map ProviderRunIdentity.create

        { RuntimeId = RuntimeId.create "rt-fallback-surface"
          LocalSeq = LocalSeq.create sequence
          ObservedAt = foldObservation ()
          EventId = EventId.create ($"fallback-{sequence}")
          Stream = StreamId.Session session
          ProviderRun = providerRun
          Fact = Fact.Agent(factOf (field value "fact")) }

    let private fallbackIn (projection: ProjectionSet) : obj =
        projection.AgentProjections.Sessions
        |> Map.toList
        |> List.tryPick (fun (_, session) -> session.Fallback |> Option.map projectionHandleView)
        |> optionObj

    let private foldTyped (values: Envelope list) : Result<ProjectionSet, FoldRejection> =
        let rec loop current remaining =
            match remaining with
            | [] -> Ok current
            | envelope :: tail ->
                match Fold.foldEnvelope current envelope with
                | Ok next -> loop next tail
                | Error failure -> Error failure

        loop Fold.empty values

    let private foldResult (result: Result<ProjectionSet, FoldRejection>) : obj =
        match result with
        | Ok projection ->
            box
                {| ok = true
                   value = fallbackIn projection |}
        | Error failure ->
            box
                {| ok = false
                   error =
                    box
                        {| Fact = failure.Fact
                           Reason = failure.Reason |} |}

    /// Fold fallback owner envelopes through the production durable fold.
    let fold (values: obj array) : obj =
        values |> Array.toList |> List.map envelopeOf |> foldTyped |> foldResult

    let fallbackFactCaseNames: string array =
        [| "FallbackCursorAdvanced"; "FallbackExhausted"; "FallbackSucceeded" |]

    /// Open the first logical run through the existing PromptDispatcher owner.
    /// The JournalHandle is opaque to callers; only this fallback boundary unwraps
    /// it for the production dispatcher.
    let acceptHumanRoot
        (handle: Wanxiangshu.Persistence.Journal.JournalHandle)
        (session: string)
        (physicalMessage: string)
        (agent: string)
        : Task<obj> =
        task {
            let runtime = PromptDispatcher.forJournal handle.Journal

            let identitySeed =
                ParticipantIdentity.resolveAtRoot agent
                |> Result.map PromptAuthority.IdentitySeed.RootSelection
                |> Result.mapError (sprintf "invalid participant identity: %A")

            match identitySeed with
            | Error error -> return box {| ok = false; error = error |}
            | Ok seed ->
                let! result =
                    runtime.AcceptHumanRoot
                        (SessionId.create session)
                        (PhysicalUserMessageId.create physicalMessage)
                        (Some seed)

                return
                    match result with
                    | Ok _ -> box {| ok = true; error = "" |}
                    | Error error ->
                        box
                            {| ok = false
                               error = PromptDispatcher.describeHumanRootAcceptanceFailure error |}
        }
