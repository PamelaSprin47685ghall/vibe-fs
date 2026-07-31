namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Domain.ProviderProjection
open Wanxiangshu.Next.Host
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

module XWire =

    let private sessionIdOfOutput (output: obj) : SessionId option =
        projectionSessionIdFromMessages output |> Option.map SessionId.create

    let private sessionProjection (journal: AgentJournal) (sessionId: SessionId) =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections

    let private isCompanionSession (journal: AgentJournal) (sessionId: SessionId) =
        SessionAssociationProjection.isCompanion
            sessionId
            (AgentJournal.snapshot journal).AgentProjections.Associations

    let private readFrames (journal: AgentJournal) (frames: BlogFrame list) : Result<string, string> =
        let rec loop remaining collected =
            match remaining with
            | [] -> Ok(String.concat "\n\n" (List.rev collected))
            | frame :: tail ->
                journal.Writer.BlobWriter.Read frame.TextRef
                |> Result.bind (fun text ->
                    if HostDigest.sha256Hex text = BlobDigest.value frame.Digest then
                        loop tail (text :: collected)
                    else
                        Error(sprintf "Companion blob digest mismatch: %s" (BlobDigest.value frame.Digest)))

        loop frames []

    let private requestStartCutoff (physical: PhysicalUserMessageId) (rawMessages: obj list) =
        rawMessages
        |> List.tryFindIndex (fun raw -> Projection.hostMessageId raw = Some physical)
        |> Option.defaultWith (fun () ->
            raise (InvalidOperationException "X-wire cannot bind the physical user message to the transform snapshot"))

    let private rawWithPrefix (rawMessages: obj list) (plan: XPrefixPlan) =
        match plan.CompanionMemory with
        | None -> rawMessages
        | Some(syntheticId, memory) ->
            Projection.prependCompanionMemory rawMessages syntheticId memory plan.DropLeading

    let private candidate
        (journal: AgentJournal)
        (sessionId: SessionId)
        (current: ProviderSemanticProjection)
        (state: SessionAgentProjection)
        (requestCutoff: int)
        : Result<PrefixProbe, NoCandidateReason> =
        let prefix = state.PrefixEpoch |> Option.defaultValue PrefixEpochProjection.empty
        let blog = state.Blog |> Option.defaultValue BlogProjection.empty
        let frames = BlogProjection.coverableFrames blog

        match readFrames journal frames with
        | Error reason -> raise (InvalidOperationException reason)
        | Ok frozenB ->
            match journal.WriteBlob frozenB with
            | Error reason -> raise (InvalidOperationException reason)
            | Ok blob ->
                PrefixProbeSelection.select
                    HostDigest.sha256Hex
                    sessionId
                    prefix.EpochId
                    (prefix.Snapshot)
                    blog.Coverage.CoverableTurnCutoffExclusive
                    blog.Coverage.CoveredPrefixDigest
                    requestCutoff
                    blob.BlobRef
                    blob.BlobDigest
                    (fun cutoff ->
                        current.Messages
                        |> List.truncate cutoff
                        |> fun messages -> ProviderProjection.renderSemantic { current with Messages = messages }
                        |> HostDigest.sha256Hex)

    let applyTransform
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (scope: PluginRuntimeScope)
        (output: obj)
        : Task<unit> =
        task {
            match journal, sessionIdOfOutput output with
            | Some durable, Some sessionId when not (isCompanionSession durable sessionId) ->
                match scope.TryRecoveryArming sessionId with
                | None -> return ()
                | Some _ ->
                    let rawMessages = Projection.messagesFromTransformOutput output
                    let physical = Projection.lastUserMessageId rawMessages

                    match physical, snapshot with
                    | None, _ ->
                        raise (InvalidOperationException "X-wire cannot plan a retry without a physical user message")
                    | _, None ->
                        raise (InvalidOperationException "X-wire cannot plan a retry without the public session snapshot")
                    | Some physical, Some snapshotPort ->
                        let! snapshotResult = snapshotPort.GetMessages sessionId

                        match snapshotResult with
                        | Error reason -> raise (InvalidOperationException reason)
                        | Ok messages ->
                            match ReviewSeal.bindableRun (PhysicalUserMessageId.value physical) messages with
                            | Error rejection -> raise (InvalidOperationException(sprintf "X-wire run binding failed: %A" rejection))
                            | Ok assistant ->
                                let providerRun = ProviderRunIdentity.create assistant.Id
                                let projections = AgentJournal.snapshot durable

                                match
                                    PromptAuthorityLedger.activeProfile sessionId projections.AgentProjections,
                                    DurableFallback.tryCurrentState sessionId projections,
                                    sessionProjection durable sessionId
                                with
                                | Some authority, Some fallback, Some state ->
                                    let current =
                                        Projection.decodeMessageView rawMessages
                                        |> ProviderProjection.toSemantic

                                    let cutoff = requestStartCutoff physical rawMessages
                                    let blog = state.Blog |> Option.defaultValue BlogProjection.empty
                                    let arming = scope.TryRecoveryArming sessionId |> Option.get
                                    let mayRecover =
                                        RecoverySlot.mayRecover
                                            arming
                                            fallback.Cursor.Offset
                                            (BlogProjection.hasCoverage blog)
                                    let selectedProbe = ref None

                                    let selectProbe () =
                                        let selected = candidate durable sessionId current state cutoff
                                        match selected with
                                        | Ok probe ->
                                            selectedProbe.Value <- Some probe
                                            Ok probe
                                        | Error reason -> Error reason

                                    let plan =
                                        AttemptPlanner.plan
                                            authority
                                            fallback.Cursor
                                            physical
                                            providerRun
                                            (PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.ProviderRetryAttempt)
                                            ProviderRequestKind.WorkMain
                                            mayRecover
                                            selectProbe

                                    let prefix =
                                        state.PrefixEpoch
                                        |> Option.defaultValue PrefixEpochProjection.empty

                                    let frozenBody =
                                        match plan.Profile.ProjectionChoice with
                                        | XProjectionChoice.UseCommittedEpoch ->
                                            match prefix.Snapshot with
                                            | None -> ""
                                            | Some committed ->
                                                match durable.Writer.BlobWriter.Read committed.FrozenBRef with
                                                | Ok body -> body
                                                | Error reason -> raise (InvalidOperationException reason)
                                        | XProjectionChoice.UsePrefixProbe _ ->
                                            match selectedProbe.Value with
                                            | Some probe ->
                                                match durable.Writer.BlobWriter.Read probe.Candidate.FrozenBRef with
                                                | Ok body -> body
                                                | Error reason -> raise (InvalidOperationException reason)
                                            | None ->
                                                raise (InvalidOperationException "X-wire planner selected a probe without a materialised candidate")

                                    let prefixPlan =
                                        XPrefixProjection.forChoice
                                            plan.Profile.ProjectionChoice
                                            prefix.Snapshot
                                            frozenBody

                                    let transformed = rawWithPrefix rawMessages prefixPlan
                                    Wanxiangshu.Next.Session.CompanionProjection.replaceMessagesInPlace output transformed
                                    scope.RecordAttemptPlan sessionId providerRun plan
                                    scope.ClearRecovery sessionId
                                | _ ->
                                    raise (InvalidOperationException "X-wire cannot plan a retry without authority, fallback, and session projections")
            | _ -> return ()
        }

    let reconcileAttempt
        (journal: AgentJournal option)
        (scope: PluginRuntimeScope)
        (turn: ReconciledTurn)
        : unit =
        match journal, scope.TryAttemptPlan turn.SessionId turn.ProviderRun with
        | Some durable, Some plan ->
            let outcome =
                match turn.Outcome with
                | TurnCompleted -> AttemptOutcome.Completed
                | TurnFailed _ -> AttemptOutcome.Failed
                | TurnAborted _ -> AttemptOutcome.Aborted
                | TurnNeedsContinuation _
                | TurnInProgress
                | TurnUnknown -> AttemptOutcome.CompletedInvalid

            match AttemptPlanner.promotableProbe plan outcome with
            | Some probe ->
                let projections = AgentJournal.snapshot durable
                let epoch =
                    AgentProjection.tryFind turn.SessionId projections.AgentProjections
                    |> Option.bind (fun state -> state.PrefixEpoch)
                    |> Option.defaultValue PrefixEpochProjection.empty

                let fact =
                    AgentFact.PrefixRebaseCommitted
                        {| SessionId = turn.SessionId
                           PreviousEpochId = probe.BasedOnEpochId
                           NextEpochId = PrefixEpochId.next probe.BasedOnEpochId
                           FrozenBRef = probe.Candidate.FrozenBRef
                           FrozenBDigest = probe.Candidate.FrozenBDigest
                           CutoffExclusive = probe.Candidate.CutoffExclusive
                           CoveredPrefixDigest = probe.Candidate.CoveredPrefixDigest
                           SealRoot = probe.Candidate.SealRoot
                           SyntheticMessageId = probe.Candidate.SyntheticMessageId
                           ProbeId = probe.ProbeId
                           SolvingProviderRun = turn.ProviderRun |}

                if epoch.EpochId = probe.BasedOnEpochId then
                    AgentJournal.appendAgent
                        (StreamId.Session turn.SessionId)
                        (Some turn.ProviderRun)
                        fact
                        durable
                    |> ignore
            | None -> ()

            scope.ClearAttemptPlan turn.SessionId turn.ProviderRun
        | _ -> ()
