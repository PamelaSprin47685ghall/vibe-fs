namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal

module TransactionSurface =

    let private requiredText fieldName (value: obj) =
        if isNull value then
            invalidArg fieldName $"missing {fieldName}"

        let parsed: string = unbox value

        if String.IsNullOrWhiteSpace parsed then
            invalidArg fieldName $"missing {fieldName}"

        parsed

    let private profileOf (value: obj) : PromptAuthority.AttemptExecutionProfile =
        let sessionId = SessionId.create (requiredText "sessionId" value?sessionId)

        let physicalId =
            PhysicalUserMessageId.create (requiredText "physicalUserMessageId" value?physicalUserMessageId)

        let logicalRunId =
            LogicalRunId.create (requiredText "logicalRunId" value?logicalRunId)

        let authorityRoot =
            AuthorityRootUserMessageId.create (
                requiredText "authorityRootUserMessageId" value?authorityRootUserMessageId
            )

        let selectedAgent =
            requiredText
                "identitySeed.participantIdentity.selectedAgent"
                value?identitySeed?participantIdentity?selectedAgent

        let identity =
            ParticipantIdentity.resolveAtRoot selectedAgent
            |> Result.defaultWith (fun error -> invalidArg "selectedAgent" $"{error}")

        let authority =
            PromptAuthority.createAuthorityExecutionProfile
                sessionId
                logicalRunId
                authorityRoot
                PromptAuthority.RootAuthorityKind.HumanRoot
                identity
            |> Result.defaultWith (fun error -> invalidArg "authority" error)

        PromptAuthority.buildAttemptExecutionProfile
            authority
            AgentPairCursor.initial
            physicalId
            (ProviderRunIdentity.create (requiredText "providerRun" value?providerRun))
            (PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.HumanRoot)
            ProviderRequestKind.WorkMain
            XProjectionChoice.UseCommittedEpoch

    let private initialState
        (stateLabel: string)
        (evidence: AcceptedChatExecutionEvidence)
        : ChatExecutionState option =
        let lifecycle =
            match stateLabel with
            | "None" -> None
            | "Accepted" -> Some ChatExecutionLifecycle.Accepted
            | "ProviderStarted" -> Some ChatExecutionLifecycle.ProviderStarted
            | "Terminal" -> Some(ChatExecutionLifecycle.Terminal ChatExecutionTerminalDisposition.Completed)
            | value -> invalidArg "state" $"unknown transaction state '{value}'"

        lifecycle
        |> Option.map (fun current ->
            let providerStarted =
                match current with
                | ChatExecutionLifecycle.ProviderStarted
                | ChatExecutionLifecycle.Terminal _ ->
                    Some
                        { Accepted = evidence
                          ProviderRun = ProviderRunIdentity.create "provider-transaction"
                          RequestKind = ProviderRequestKind.WorkMain
                          ProjectionChoice = XProjectionChoice.UseCommittedEpoch }
                | ChatExecutionLifecycle.Accepted -> None

            let terminalEvidence =
                match current, providerStarted with
                | ChatExecutionLifecycle.Terminal _, Some started ->
                    Some(ChatExecutionTerminalEvidence.AfterProviderStart started)
                | _ -> None

            { Key =
                { SessionId = evidence.SessionId
                  PhysicalUserMessageId = evidence.PhysicalUserMessageId }
              Evidence = evidence
              ProviderStarted = providerStarted
              TerminalEvidence = terminalEvidence
              Lifecycle = current })

    let private stepLabel =
        function
        | ChatAdmissionTransactionStep.ResolveState -> "ResolveState"
        | ChatAdmissionTransactionStep.Accept -> "Accept"
        | ChatAdmissionTransactionStep.AcceptedWitness -> "AcceptedWitness"
        | ChatAdmissionTransactionStep.AcquireLease -> "AcquireLease"
        | ChatAdmissionTransactionStep.LeaseTarget -> "LeaseTarget"
        | ChatAdmissionTransactionStep.BindExecution -> "BindExecution"
        | ChatAdmissionTransactionStep.ProjectHost -> "ProjectHost"
        | ChatAdmissionTransactionStep.CommitLease -> "CommitLease"
        | ChatAdmissionTransactionStep.TerminalizeAccepted -> "TerminalizeAccepted"
        | ChatAdmissionTransactionStep.UnbindExecution -> "UnbindExecution"
        | ChatAdmissionTransactionStep.ReleaseBeforeProvider -> "ReleaseBeforeProvider"
        | ChatAdmissionTransactionStep.Settled -> "Settled"

    let private acceptanceErrorKind =
        function
        | ManagedChatAcceptanceError.IntentRejected _ -> "IntentRejected"
        | ManagedChatAcceptanceError.AuthorityRegistrationRejected _ -> "AuthorityRegistrationRejected"
        | ManagedChatAcceptanceError.NotAttempted _ -> "NotAttempted"
        | ManagedChatAcceptanceError.CommitUnknown _ -> "CommitUnknown"
        | ManagedChatAcceptanceError.AttemptEvidenceInvalid _ -> "AttemptEvidenceInvalid"
        | ManagedChatAcceptanceError.AttemptKeyMismatch _ -> "AttemptKeyMismatch"
        | ManagedChatAcceptanceError.EstablishedEvidenceConflict _ -> "EstablishedEvidenceConflict"
        | ManagedChatAcceptanceError.ProjectionMissingAfterCommit _ -> "ProjectionMissingAfterCommit"
        | ManagedChatAcceptanceError.ProjectionConflictAfterCommit _ -> "ProjectionConflictAfterCommit"
        | ManagedChatAcceptanceError.FactRejected _ -> "FactRejected"

    let private releaseLabel =
        function
        | ChatAdmissionReleaseOutcome.Settled CapacityTransitionOutcome.Applied -> "Applied"
        | ChatAdmissionReleaseOutcome.Settled CapacityTransitionOutcome.AlreadyApplied -> "AlreadyApplied"
        | ChatAdmissionReleaseOutcome.Settled CapacityTransitionOutcome.StaleFence -> "StaleFence"
        | ChatAdmissionReleaseOutcome.Settled CapacityTransitionOutcome.Conflict -> "Conflict"
        | ChatAdmissionReleaseOutcome.BoundaryFailed _ -> "BoundaryFailed"

    let private errorToJs =
        function
        | ChatAdmissionTransactionError.AdmissionRejected _ -> box {| kind = "AdmissionRejected" |}
        | ChatAdmissionTransactionError.AcceptanceFailed error -> box {| kind = acceptanceErrorKind error |}
        | ChatAdmissionTransactionError.AcceptanceBoundaryFailed _ -> box {| kind = "AcceptanceBoundaryFailed" |}
        | ChatAdmissionTransactionError.PreProviderSettlementFailed _ -> box {| kind = "PreProviderSettlementFailed" |}
        | ChatAdmissionTransactionError.PreProviderSettlementBoundaryFailed _ ->
            box {| kind = "PreProviderSettlementBoundaryFailed" |}
        | ChatAdmissionTransactionError.PreProviderUnbindBoundaryFailed(_, release) ->
            box
                {| kind = "PreProviderUnbindBoundaryFailed"
                   release = releaseLabel release |}
        | ChatAdmissionTransactionError.LeaseAcquisitionFailed _ -> box {| kind = "LeaseAcquisitionFailed" |}
        | ChatAdmissionTransactionError.LeaseTargetFailed(_, release) ->
            box
                {| kind = "LeaseTargetFailed"
                   release = releaseLabel release |}
        | ChatAdmissionTransactionError.LeaseTargetBoundaryFailed(_, release) ->
            box
                {| kind = "LeaseTargetBoundaryFailed"
                   release = releaseLabel release |}
        | ChatAdmissionTransactionError.LeaseTargetProjectionFailed(_, release) ->
            box
                {| kind = "LeaseTargetProjectionFailed"
                   release = releaseLabel release |}
        | ChatAdmissionTransactionError.BindingFailed(_, release) ->
            box
                {| kind = "BindingFailed"
                   release = releaseLabel release |}
        | ChatAdmissionTransactionError.HostProjectionFailed(_, release) ->
            box
                {| kind = "HostProjectionFailed"
                   release = releaseLabel release |}
        | ChatAdmissionTransactionError.LeaseCommitFailed(_, release) ->
            box
                {| kind = "LeaseCommitFailed"
                   release = releaseLabel release |}
        | ChatAdmissionTransactionError.LeaseCommitBoundaryFailed(_, release) ->
            box
                {| kind = "LeaseCommitBoundaryFailed"
                   release = releaseLabel release |}

    let private outcomeLabel =
        function
        | ChatAdmissionTransactionOutcome.Settled _ -> "Settled"
        | ChatAdmissionTransactionOutcome.Superseded _ -> "Superseded"
        | ChatAdmissionTransactionOutcome.CapacityQueueFull _ -> "CapacityQueueFull"
        | ChatAdmissionTransactionOutcome.Cancelled _ -> "Cancelled"
        | ChatAdmissionTransactionOutcome.AlreadyStarted _ -> "AlreadyStarted"
        | ChatAdmissionTransactionOutcome.AlreadyTerminal _ -> "AlreadyTerminal"

    let transactionScenario (evidenceValue: obj) (failurePoint: string) (stateLabel: string) : Task<obj> =
        task {
            let profile: PromptAuthority.AttemptExecutionProfile = profileOf evidenceValue

            let evidence: AcceptedChatExecutionEvidence =
                ManagedChatAcceptance.evidenceFromIntent
                    profile.Authority
                    profile.PhysicalUserMessageId
                    profile.Origin
                    profile.EffectiveAgent
            // DSL-MUTABLE: algorithm-scratch
            let mutable state: ChatExecutionState option = initialState stateLabel evidence
            let trace = ResizeArray<string>()
            // DSL-MUTABLE: algorithm-scratch
            let mutable acceptCount = 0
            let mutable appendCount = 0
            // DSL-MUTABLE: algorithm-scratch
            let mutable acquireCount = 0
            let mutable bindCount = 0
            // DSL-MUTABLE: algorithm-scratch
            let mutable hostCount = 0
            let mutable commitCount = 0
            // DSL-MUTABLE: algorithm-scratch
            let mutable releaseCount = 0
            let mutable unbindCount = 0
            // DSL-MUTABLE: algorithm-scratch
            let mutable activeCapacity = 0
            let mutable providerBinding = 0
            // DSL-MUTABLE: algorithm-scratch
            let mutable hostProjected = false
            let mutable crashed = false

            let persistence: ManagedChatAcceptancePersistence =
                { ReadExact = fun _ -> state
                  AppendAccepted =
                    fun key acceptedEvidence ->
                        task {
                            appendCount <- appendCount + 1

                            match failurePoint with
                            | "CrashA" ->
                                crashed <- true
                                return raise (InvalidOperationException "crash A before Accepted append")
                            | "AcceptNotAttempted" ->
                                return
                                    Error(
                                        JournalAppendFailure.WriterUnavailable(
                                            EventId.create "transaction-not-attempted",
                                            JournalUnavailable.WriterDisposed
                                        )
                                    )
                            | "AcceptCommitUnknown" ->
                                return
                                    Error(
                                        JournalAppendFailure.WriteUnknown(
                                            EventId.create "transaction-commit-unknown",
                                            JournalFailure.WriteFailed "recording surface"
                                        )
                                    )
                            | _ ->
                                state <-
                                    Some
                                        { Key = key
                                          Evidence = acceptedEvidence
                                          ProviderStarted = None
                                          TerminalEvidence = None
                                          Lifecycle = ChatExecutionLifecycle.Accepted }

                                return Ok()
                        } }

            let target =
                { Model = "openai/gpt-5"
                  Reasoning = "high" }

            let exactIdentity: ExecutionAdmissionExactIdentity =
                { SessionId = SessionId.value profile.SessionId
                  PhysicalUserMessageId = PhysicalUserMessageId.value profile.PhysicalUserMessageId
                  EffectiveAgent = profile.EffectiveAgent
                  Target = target }

            let lease =
                ExecutionAdmissionLease.Create(
                    obj (),
                    CapacityCreditId.first,
                    CapacityLeaseId.first,
                    CapacityFence.first,
                    exactIdentity
                )

            let ports: ChatAdmissionTransactionPorts =
                { Accept =
                    fun _ ->
                        task {
                            acceptCount <- acceptCount + 1

                            let key =
                                { SessionId = evidence.SessionId
                                  PhysicalUserMessageId = evidence.PhysicalUserMessageId }

                            return! ManagedChatAcceptance.acceptWith persistence key evidence
                        }
                  Acquire =
                    fun _ ->
                        task {
                            acquireCount <- acquireCount + 1

                            if failurePoint = "AcquireLease" then
                                return Error(InvalidOperationException "injected acquisition failure")
                            elif failurePoint = "AcquireSuperseded" then
                                return Ok ExecutionAdmissionAcquisition.Superseded
                            elif failurePoint = "AcquireQueueFull" then
                                return Ok ExecutionAdmissionAcquisition.QueueFull
                            elif failurePoint = "AcquireCancelled" then
                                return Ok ExecutionAdmissionAcquisition.Cancelled
                            else
                                activeCapacity <- 1
                                return Ok(ExecutionAdmissionAcquisition.Admitted lease)
                        }
                  LeaseTarget =
                    fun _ ->
                        if failurePoint = "LeaseTarget" then
                            Error ExecutionAdmissionRejection.WrongTarget
                        else
                            Ok target
                  Bind =
                    fun _ _ _ ->
                        bindCount <- bindCount + 1
                        providerBinding <- 1

                        if failurePoint = "BindExecution" || failurePoint = "ReleaseBeforeProvider" then
                            Error(InvalidOperationException "injected binding failure")
                        else
                            Ok()
                  ProjectHost =
                    fun _ ->
                        hostCount <- hostCount + 1

                        if failurePoint = "ProjectHost" then
                            Error(InvalidOperationException "injected Host projection failure")
                        else
                            hostProjected <- true
                            Ok()
                  Commit =
                    fun _ _ ->
                        commitCount <- commitCount + 1

                        if failurePoint = "CommitLease" then
                            CapacityTransitionOutcome.StaleFence
                        else
                            CapacityTransitionOutcome.Applied
                  ReleaseBeforeProvider =
                    fun _ ->
                        releaseCount <- releaseCount + 1

                        if failurePoint = "ReleaseBeforeProvider" then
                            raise (InvalidOperationException "injected release failure")
                        else
                            activeCapacity <- 0
                            CapacityTransitionOutcome.Applied
                  SettlePreProvider =
                    fun key acceptedEvidence disposition ->
                        let settlementPersistence: PreProviderSettlementPersistence =
                            { ReadExact = fun _ -> state
                              AppendTerminal =
                                fun _ _ _ ->
                                    task {
                                        match
                                            state
                                            |> Option.map (fun current ->
                                                ChatExecutionFactFold.applyTerminal
                                                    key
                                                    (ChatExecutionTerminalEvidence.PreProvider acceptedEvidence)
                                                    disposition
                                                    { ByKey = Map.ofList [ key, current ] })
                                        with
                                        | Some(Ok updated) ->
                                            state <- ChatExecutionProjection.byKey key updated
                                            return Ok()
                                        | Some(Error rejection) ->
                                            return
                                                Error(
                                                    JournalAppendFailure.FactRejected(
                                                        EventId.create "transaction-terminal-rejected",
                                                        rejection
                                                    )
                                                )
                                        | None ->
                                            return
                                                Error(
                                                    JournalAppendFailure.WriterUnavailable(
                                                        EventId.create "transaction-terminal-missing",
                                                        JournalUnavailable.WriterDisposed
                                                    )
                                                )
                                    } }

                        PreProviderSettlement.settleWith settlementPersistence key acceptedEvidence disposition
                  Unbind =
                    fun _ ->
                        unbindCount <- unbindCount + 1
                        providerBinding <- 0 }

            let key: ChatAdmissionIntent.ExecutionKey =
                { SessionId = profile.SessionId
                  PhysicalUserMessageId = profile.PhysicalUserMessageId }

            let intent =
                ChatAdmissionIntent.Decision.ExternalRootIntent
                    { Key = key
                      ExplicitAgent = ParticipantIdentity.selectedAgent profile.Authority.ParticipantIdentity
                      EffectiveAgent = profile.EffectiveAgent
                      Origin = profile.Origin
                      IdentitySeed = profile.Authority.IdentitySeed }

            let input =
                { Intent = intent
                  CurrentState = state }

            let observe step =
                trace.Add(stepLabel step)

                match failurePoint, step with
                | "CrashB", ChatAdmissionTransactionStep.AcquireLease
                | "CrashC", ChatAdmissionTransactionStep.BindExecution
                | "CrashD", ChatAdmissionTransactionStep.ProjectHost ->
                    crashed <- true
                    raise (InvalidOperationException $"crash {failurePoint}")
                | _ -> ()

            let! result =
                task {
                    try
                        return! ChatAdmissionTransaction.executeWith observe ports input
                    with error ->
                        return Error(ChatAdmissionTransactionError.AcceptanceBoundaryFailed error)
                }

            if failurePoint = "CrashE" then
                crashed <- true

            let outcome, targetValue, error =
                match result with
                | Ok(ChatAdmissionTransactionOutcome.Settled(_, settledTarget, _, _)) ->
                    "Settled",
                    box
                        {| model = settledTarget.Model
                           reasoning = settledTarget.Reasoning |},
                    null
                | Ok value -> outcomeLabel value, null, null
                | Error transactionError -> null, null, errorToJs transactionError

            return
                box
                    {| ok = Result.isOk result
                       outcome = outcome
                       target = targetValue
                       error = error
                       trace = trace.ToArray()
                       acceptCount = acceptCount
                       appendCount = appendCount
                       acquireCount = acquireCount
                       bindCount = bindCount
                       hostCount = hostCount
                       commitCount = commitCount
                       releaseCount = releaseCount
                       unbindCount = unbindCount
                       providerCount = 0
                       crashed = crashed
                       durableLifecycle =
                        state
                        |> Option.map (fun current ->
                            match current.Lifecycle with
                            | ChatExecutionLifecycle.Accepted -> "Accepted"
                            | ChatExecutionLifecycle.ProviderStarted -> "ProviderStarted"
                            | ChatExecutionLifecycle.Terminal _ -> "Terminal")
                        |> Option.defaultValue "None"
                       admission =
                        {| activeCapacity = activeCapacity
                           providerBinding = providerBinding
                           hostProjected = hostProjected |} |}
        }

    let preProviderSettlementScenario (evidenceValue: obj) (failureKind: string) (releaseMode: string) : Task<obj> =
        task {
            let profile = profileOf evidenceValue

            let acceptedEvidence =
                ManagedChatAcceptance.evidenceFromIntent
                    profile.Authority
                    profile.PhysicalUserMessageId
                    profile.Origin
                    profile.EffectiveAgent

            let key: ChatExecutionKey =
                { SessionId = acceptedEvidence.SessionId
                  PhysicalUserMessageId = acceptedEvidence.PhysicalUserMessageId }

            let facts = ResizeArray<ChatExecutionFactCases>()
            // DSL-MUTABLE: algorithm-scratch
            let mutable projection = ChatExecutionProjection.empty
            let mutable activeCapacity = 0
            // DSL-MUTABLE: algorithm-scratch
            let mutable providerEffectCount = 0
            let trace = ResizeArray<string>()

            let apply fact =
                match ChatExecutionFactFold.fold projection fact with
                | Ok updated ->
                    projection <- updated
                    facts.Add fact
                    Ok()
                | Error rejection ->
                    Error(
                        JournalAppendFailure.FactRejected(EventId.create "pre-provider-settlement-rejected", rejection)
                    )

            let acceptancePersistence: ManagedChatAcceptancePersistence =
                { ReadExact = fun requested -> ChatExecutionProjection.byKey requested projection
                  AppendAccepted =
                    fun requested evidence ->
                        ChatExecutionFactCases.Accepted
                            {| SchemaVersion = 1
                               Key = requested
                               Evidence = evidence |}
                        |> apply
                        |> Task.FromResult }

            let settlementPersistence: PreProviderSettlementPersistence =
                { ReadExact = fun requested -> ChatExecutionProjection.byKey requested projection
                  AppendTerminal =
                    fun requested evidence disposition ->
                        ChatExecutionFactCases.Terminal
                            {| SchemaVersion = 1
                               Key = requested
                               Evidence = ChatExecutionTerminalEvidence.PreProvider evidence
                               Disposition = disposition |}
                        |> apply
                        |> Task.FromResult }

            let rawMembraneInput =
                not (isNull evidenceValue?effectiveAgent)
                && string evidenceValue?effectiveAgent = "AGENT-028"

            let acceptanceConflict =
                failureKind = "IdentityConflict"
                || failureKind = "PluginReplay"
                || rawMembraneInput

            if acceptanceConflict then
                ChatExecutionFactCases.Accepted
                    {| SchemaVersion = 1
                       Key = key
                       Evidence = acceptedEvidence |}
                |> apply
                |> Result.defaultWith (fun error -> invalidOp (JournalAppendFailure.describe error))

            let attemptedEvidence =
                if acceptanceConflict then
                    { acceptedEvidence with
                        EffectiveAgent = ParticipantIdentity.peerAgent profile.Authority.ParticipantIdentity }
                else
                    acceptedEvidence

            let accept _ =
                ManagedChatAcceptance.acceptWith acceptancePersistence key attemptedEvidence

            let target =
                { Model = "openai/gpt-5"
                  Reasoning = "high" }

            let exactIdentity: ExecutionAdmissionExactIdentity =
                { SessionId = SessionId.value key.SessionId
                  PhysicalUserMessageId = PhysicalUserMessageId.value key.PhysicalUserMessageId
                  EffectiveAgent = acceptedEvidence.EffectiveAgent
                  Target = target }

            let lease =
                ExecutionAdmissionLease.Create(
                    obj (),
                    CapacityCreditId.first,
                    CapacityLeaseId.first,
                    CapacityFence.first,
                    exactIdentity
                )

            let bindingIntent: ChatAdmissionIntent.Decision =
                if failureKind = "PluginReplay" then
                    let promptKey = PromptKey.create "prompt-plugin-replay"

                    ChatAdmissionIntent.Decision.PendingPromptIntent
                        { Key =
                            { SessionId = key.SessionId
                              PhysicalUserMessageId = key.PhysicalUserMessageId }
                          PromptKey = promptKey
                          Claim =
                            { PromptKey = promptKey
                              SessionId = key.SessionId
                              Origin = acceptedEvidence.Origin
                              LogicalRunId = Some acceptedEvidence.LogicalRunId
                              AuthorityRootUserMessageId = Some acceptedEvidence.AuthorityRootUserMessageId
                              EffectiveAgent = Some acceptedEvidence.EffectiveAgent
                              IdentitySeed = acceptedEvidence.IdentitySeed
                              PayloadDigest = "plugin-replay"
                              Receipt = None
                              ClaimedAtRuntimeStartCount = 0 }
                          EffectiveAgent = acceptedEvidence.EffectiveAgent
                          Origin = acceptedEvidence.Origin
                          IdentitySeed = acceptedEvidence.IdentitySeed }
                else
                    ChatAdmissionIntent.Decision.ExternalRootIntent
                        { Key =
                            { SessionId = key.SessionId
                              PhysicalUserMessageId = key.PhysicalUserMessageId }
                          ExplicitAgent = acceptedEvidence.EffectiveAgent
                          EffectiveAgent = acceptedEvidence.EffectiveAgent
                          Origin = acceptedEvidence.Origin
                          IdentitySeed = acceptedEvidence.IdentitySeed }

            let installBinding model =
                match bindingIntent with
                | ChatAdmissionIntent.Decision.PendingPromptIntent intent ->
                    SessionExecutionBinding.acceptPromptExecution
                        key.SessionId
                        intent.PromptKey
                        key.PhysicalUserMessageId
                        acceptedEvidence.EffectiveAgent
                        model
                | _ ->
                    SessionExecutionBinding.acceptExternalExecution
                        key.SessionId
                        key.PhysicalUserMessageId
                        acceptedEvidence.EffectiveAgent
                        model

            let ports: ChatAdmissionTransactionPorts =
                { Accept = accept
                  Acquire =
                    fun _ ->
                        if failureKind = "Supersession" then
                            Task.FromResult(Ok ExecutionAdmissionAcquisition.Superseded)
                        else
                            activeCapacity <- 1
                            Task.FromResult(Ok(ExecutionAdmissionAcquisition.Admitted lease))
                  LeaseTarget = fun _ -> Ok target
                  Bind =
                    fun _ _ model ->
                        installBinding model

                        if failureKind = "ExecutionBindingError" || failureKind = "FatalMembraneInput" then
                            Error(InvalidOperationException "injected pre-provider binding failure")
                        else
                            Ok()
                  ProjectHost =
                    fun _ ->
                        if failureKind = "ProjectionError" then
                            Error(InvalidOperationException "injected pre-provider projection failure")
                        else
                            Ok()
                  Commit = fun _ _ -> CapacityTransitionOutcome.Applied
                  ReleaseBeforeProvider =
                    fun _ ->
                        if releaseMode = "Exact" then
                            activeCapacity <- 0

                        CapacityTransitionOutcome.Applied
                  SettlePreProvider = PreProviderSettlement.settleWith settlementPersistence
                  Unbind =
                    fun requested ->
                        SessionExecutionBinding.releaseAcceptedExecution
                            requested.SessionId
                            requested.PhysicalUserMessageId }

            let current = ChatExecutionProjection.byKey key projection

            let! transactionResult =
                ChatAdmissionTransaction.executeWith
                    (stepLabel >> trace.Add)
                    ports
                    { Intent = bindingIntent
                      CurrentState = current }

            let state = ChatExecutionProjection.byKey key projection

            let disposition =
                state
                |> Option.bind (fun execution ->
                    match execution.Lifecycle with
                    | ChatExecutionLifecycle.Terminal value -> Some value
                    | _ -> None)
                |> Option.map string
                |> Option.toObj

            let classification =
                match transactionResult with
                | Ok(ChatAdmissionTransactionOutcome.Superseded _) -> "Recoverable"
                | _ -> "Permanent"

            let serializedFacts =
                facts
                |> Seq.map (AgentFact.ChatExecution >> Fact.Agent >> FactCodec.serializeFact)
                |> Seq.toArray

            return
                box
                    {| key =
                        {| sessionId = SessionId.value key.SessionId
                           physicalUserMessageId = PhysicalUserMessageId.value key.PhysicalUserMessageId |}
                       facts = serializedFacts
                       admission =
                        {| activeCapacity = activeCapacity
                           providerBinding =
                            SessionExecutionBinding.exactExecutionBindingCount key.SessionId key.PhysicalUserMessageId |}
                       acceptedFactCount =
                        facts
                        |> Seq.filter (function
                            | ChatExecutionFactCases.Accepted _ -> true
                            | _ -> false)
                        |> Seq.length
                       providerEffectCount = providerEffectCount
                       trace = trace.ToArray()
                       failure =
                        {| kind = failureKind
                           classification = classification
                           disposition = disposition |}
                       transactionOk = Result.isOk transactionResult |}
        }
