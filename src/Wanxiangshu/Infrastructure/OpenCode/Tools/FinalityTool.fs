namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Fable.Core.JsInterop
open Wanxiangshu.Session
open Wanxiangshu.Tools

/// GLORY-034/035/037/041: the Manager's end-of-life tool.
///
/// The tool is deliberately opaque to the Manager: the description never
/// mentions review, the reviewer, PERFECT, or the barrier (SURFACE-005). A
/// legal call is accepted in GLORY-040 order — validate, read tree, write
/// last_words blob, append `FinalityRequested`, park the Manager completion,
/// start the Host-owned `FinalityController`. Every precondition failure
/// returns a narrative refusal and never writes `FinalityRequested`
/// (GLORY-038/039).
module FinalityTool =

    /// GLORY-062 + SURFACE-004: the frozen second-suicide tool result. The
    /// Manager's next accepted ending is final.
    let RestInPeaceInstructions =
        [ "rest in peace"
          "Terminate the conversation now."
          "Do not call any more tools or continue working." ]

    let private tString = ToolHostCodec.TString

    let private treeOf (scope: ToolRuntimeScope) (sessionId: string) =
        match scope.TreePortFor sessionId with
        | None -> None
        | Some port ->
            try
                let hash = port.GetTreeHash().Trim()

                if String.IsNullOrWhiteSpace hash then
                    None
                else
                    Some(GitTreeHash.create hash)
            with _ ->
                None

    /// GLORY-037.11-13: any outstanding child work blocks the ending.
    let private outstandingWork (scope: ToolRuntimeScope) (context: HostToolContext) =
        let sid = SessionId.create context.SessionId

        let durableOutstanding =
            TerminalPolicy.outstandingBackground scope.Journal scope.HasLivePty (Some Role.Manager) sid

        let runtimeOutstanding =
            match scope.RuntimeFor context with
            | Ok runtime -> runtime.PendingRunCount > 0
            | Error _ -> false

        durableOutstanding || runtimeOutstanding

    /// GLORY-068/069: an AgentOwnerRoot Manager (an Orchestrator's ManagerJob)
    /// has no HumanRoot and therefore no Life. Its ending still goes through
    /// suicide; build the migration Life on acceptance so the Finality workflow
    /// has an identity. Idempotent: a current Life is returned as-is; a
    /// completed (archived) Life is a closed chapter, and ORCH-007 recovery may
    /// resume the Manager — a new migration Life is opened for the new ending.
    let private ensureMigrationLife (journal: AgentJournal) (sessionId: string) : ManagerLifeId option =
        let sid = SessionId.create sessionId
        let snapshot = AgentJournal.snapshot journal

        let openLife (xTrace: XTraceProjectionState option) : ManagerLifeId option =
            match xTrace with
            | None -> None
            | Some xTrace ->
                match xTrace.Opening with
                | None -> None
                | Some opening ->
                    match journal.WriteBlob opening.AssignmentText with
                    | Error _ -> None
                    | Ok blob ->
                        let lifeId = ManagerLifeId.create (Guid.NewGuid().ToString("N"))

                        AgentJournal.appendManagerLifecycle
                            (StreamId.Session sid)
                            (ManagerLifecycleFact.LifeOpened
                                {| SessionId = sid
                                   LifeId = lifeId
                                   OpeningUserMessageId = PhysicalUserMessageId.create sessionId
                                   OpeningTextRef = blob.BlobRef
                                   OpeningTextDigest = blob.BlobDigest
                                   OpeningCursorSequence = 0L |})
                            journal
                        |> Result.mapError (fun failure ->
                            raise (
                                InvalidOperationException(
                                    sprintf "Life migration append failed: %s" (JournalAppendFailure.describe failure)
                                )
                            ))
                        |> ignore

                        AgentJournal.appendManagerLifecycle
                            (StreamId.Session sid)
                            (ManagerLifecycleFact.WorkActivated
                                {| SessionId = sid
                                   LifeId = lifeId
                                   ActivationPromptKey = PromptKey.create ""
                                   ProtectedPrefixEndSequence = XTraceProjection.headSequence xTrace + 1L |})
                            journal
                        |> Result.mapError (fun failure ->
                            raise (
                                InvalidOperationException(
                                    sprintf
                                        "Life migration activation failed: %s"
                                        (JournalAppendFailure.describe failure)
                                )
                            ))
                        |> ignore

                        Some lifeId

        match AgentProjection.tryFind sid snapshot.AgentProjections with
        | None -> None
        | Some session ->
            match session.ManagerLife with
            | Some existing ->
                match existing.CurrentLife with
                | Some life -> Some life.LifeId
                | None -> openLife session.XTrace
            | None -> openLife session.XTrace

    /// GLORY-062: the second suicide of a blessed Life. Resource safety first
    /// (GLORY-037); then NO tree read, NO Reviewer/barrier/witness. The NEW
    /// last_words becomes the terminal; the tool result is the frozen
    /// rest-in-peace instruction.
    let private completeBlessedLife
        (scope: ToolRuntimeScope)
        (journal: AgentJournal)
        (context: HostToolContext)
        (sid: SessionId)
        (life: LifeProjection)
        (blessing: BlessingEvidence)
        (lastWords: string)
        : Task<string> =
        task {
            match journal.WriteBlob lastWords with
            | Error _ -> return ToolHostCodec.tomlObjectWithInstructions [ "Continue working and try again later." ] []
            | Ok blob ->
                let providerRun = context.ProviderRunId.Value

                let alreadyCompleted =
                    AgentProjection.tryFind sid (AgentJournal.snapshot journal).AgentProjections
                    |> Option.bind (fun session -> session.ManagerLife)
                    |> Option.exists (fun lifecycle ->
                        (lifecycle.CurrentLife
                         |> Option.exists (fun current -> current.LifeId = life.LifeId && current.Completed))
                        || lifecycle.CompletedLives
                           |> List.exists (fun completed -> completed.LifeId = life.LifeId))

                let terminalRecorded =
                    AgentProjection.tryFind sid (AgentJournal.snapshot journal).AgentProjections
                    |> Option.bind (fun session -> session.XTrace)
                    |> Option.exists (fun state -> state.Terminal.IsSome)

                if not alreadyCompleted then
                    AgentJournal.appendManagerLifecycle
                        (StreamId.Session sid)
                        (ManagerLifecycleFact.LifeCompleted
                            {| SessionId = sid
                               LifeId = life.LifeId
                               RequestId = blessing.RequestId
                               TerminalRef = blob.BlobRef
                               TerminalDigest = blob.BlobDigest |})
                        journal
                    |> Result.mapError (fun failure ->
                        raise (
                            InvalidOperationException(
                                sprintf "LifeCompleted append failed: %s" (JournalAppendFailure.describe failure)
                            )
                        ))
                    |> ignore

                    // GLORY-062: LifeCompleted BEFORE the terminal is published.
                    // Only the FIRST Life may occupy the single XTrace terminal
                    // slot; later Lives' terminals live in LifeCompleted only.
                    if not terminalRecorded then
                        AgentJournal.appendAgent
                            (StreamId.Session sid)
                            (Some providerRun)
                            (CompanionFact.TerminalOutputCaptured
                                {| SessionId = sid
                                   TextRef = blob.BlobRef
                                   TextDigest = blob.BlobDigest
                                   ProviderRun = providerRun |})
                            journal
                        |> ignore

                    match scope.EventPort with
                    | Some eventPort ->
                        let runResult: AgentRunResult =
                            { SessionId = sid
                              AuthorityRootUserMessageId =
                                PromptAuthorityLedger.activeProfile sid (AgentJournal.snapshot journal).AgentProjections
                                |> Option.map (fun profile -> profile.AuthorityRootUserMessageId)
                                |> Option.defaultValue (AuthorityRootUserMessageId.create "")
                              ProviderRun = providerRun
                              Role = Role.Manager
                              Directory = scope.DirectoryFor(SessionId.value sid)
                              TerminalText = lastWords
                              TurnFormalText = lastWords }

                        eventPort.NotifyTerminal sid (TerminalOutcome.Completed runResult) |> ignore
                    | None -> ()

                return ToolHostCodec.tomlObjectWithInstructions RestInPeaceInstructions []
        }


    let private execute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            let lastWords = args.Text "last_words"

            match scope.RoleFor context with
            | Some Role.Manager ->
                if String.IsNullOrWhiteSpace context.SessionId then
                    return ToolHostCodec.tomlObjectWithInstructions [ "Continue working and try again later." ] []
                else
                    let sessionId = context.SessionId
                    let sid = SessionId.create sessionId

                    match scope.Journal with
                    | None ->
                        return ToolHostCodec.tomlObjectWithInstructions [ "Continue working and try again later." ] []
                    | Some journal ->
                        let snapshot = AgentJournal.snapshot journal

                        let lifecycle =
                            AgentProjection.tryFind sid snapshot.AgentProjections
                            |> Option.bind (fun session -> session.ManagerLife)
                            |> Option.defaultValue ManagerLifecycleProjection.empty

                        // GLORY-039: an AgentOwnerRoot Manager (Orchestrator
                        // ManagerJob, GLORY-068) has no Life; migrate on its first
                        // ending. A HumanRoot Manager without a Life is still
                        // planning and must keep working.
                        // DSL-MUTABLE: algorithm-scratch — effective life after optional migration ensure
                        let mutable effectiveLife: LifeProjection option = lifecycle.CurrentLife

                        if effectiveLife.IsNone then
                            let authorityKind =
                                AgentProjection.tryFind sid snapshot.AgentProjections
                                |> Option.bind (fun session -> session.PromptAuthority)
                                |> Option.bind (fun authority -> authority.ActiveLogicalRun)
                                |> Option.map (fun profile -> profile.AuthorityKind)

                            match authorityKind with
                            | Some kind when kind <> PromptAuthority.RootAuthorityKind.HumanRoot ->
                                ensureMigrationLife journal sessionId |> ignore

                                effectiveLife <-
                                    AgentProjection.tryFind sid (AgentJournal.snapshot journal).AgentProjections
                                    |> Option.bind (fun session -> session.ManagerLife)
                                    |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)
                            | _ -> ()

                        let acceptSuicide (life: LifeProjection) =
                            task {
                                // GLORY-037.7: an open request. The same ToolCallId is a
                                // replay (idempotent); a different one is still in motion.
                                match life.ActiveFinality with
                                | Some request when ManagerLifecycleProjection.isOpen request ->

                                    match context.ToolCallId with
                                    | Some callId when callId = request.ToolCallId ->
                                        return ToolHostCodec.tomlObject [ "status", tString "already_received" ]
                                    | _ ->
                                        // Crash recovery (docs/how/glory.md matrix):
                                        // a request with no enlisted Reviewer member
                                        // died before the first enlistment. Restart the
                                        // Finality workflow on the SAME request; the
                                        // fold keeps the request open until the
                                        // restart lands a terminal fact.
                                        if Map.isEmpty request.Members then
                                            let! outcome =
                                                FinalityController.start
                                                    scope
                                                    sid
                                                    life.LifeId
                                                    request.RequestId
                                                    request.GitTreeHash
                                                    request.LastWordsRef
                                                    request.LastWordsDigest
                                                    request.ProviderRun
                                                    (defaultArg
                                                        scope.FinalityReviewerTimeoutMs
                                                        ExecutorSummarize.AwaitAgentTimeoutMs)

                                            match outcome with
                                            | FinalityController.FinalityOutcome.Rejected prompt
                                            | FinalityController.FinalityOutcome.Blessed prompt
                                            | FinalityController.FinalityOutcome.Undecided prompt -> return prompt
                                        else
                                            return
                                                ToolHostCodec.tomlObjectWithInstructions
                                                    [ "Wait for the current ending to resolve." ]
                                                    []
                                | _ ->
                                    if String.IsNullOrWhiteSpace lastWords then
                                        return
                                            ToolHostCodec.tomlObjectWithInstructions
                                                [ "Call suicide again with non-empty last_words." ]
                                                []
                                    elif context.ToolCallId.IsNone then
                                        return
                                            ToolHostCodec.tomlObjectWithInstructions
                                                [ "Continue working and try again later." ]
                                                []
                                    elif context.ProviderRunId.IsNone then
                                        return
                                            ToolHostCodec.tomlObjectWithInstructions
                                                [ "Continue working and try again later." ]
                                                []
                                    elif outstandingWork scope context then
                                        // GLORY-038: background work still walks the world.
                                        return
                                            ToolHostCodec.tomlObjectWithInstructions
                                                [ "Call join before seeking your end." ]
                                                []
                                    else
                                        // GLORY-062: a blessed Life ends without a
                                        // second review — resource safety only.
                                        match life.LastBlessing with
                                        | Some blessing ->
                                            return!
                                                completeBlessedLife scope journal context sid life blessing lastWords
                                        | None ->
                                            // GLORY-037.14/15: the tree must be readable.
                                            match treeOf scope sessionId with
                                            | None ->
                                                return
                                                    ToolHostCodec.tomlObjectWithInstructions
                                                        [ "Continue working and seek your end again when you are ready." ]
                                                        []
                                            | Some tree ->
                                                match journal.WriteBlob lastWords with
                                                | Error _ ->
                                                    return
                                                        ToolHostCodec.tomlObjectWithInstructions
                                                            [ "Continue working and seek your end again when you are ready." ]
                                                            []
                                                | Ok blob ->
                                                    // GLORY-040: accept in order. Synchronously wait for the Finality
                                                    // workflow to complete; every outcome lands on the journal before any side effect.
                                                    let requestId =
                                                        FinalityRequestId.create (Guid.NewGuid().ToString("N"))

                                                    AgentJournal.appendManagerLifecycle
                                                        (StreamId.Session sid)
                                                        (ManagerLifecycleFact.FinalityRequested
                                                            {| SessionId = sid
                                                               LifeId = life.LifeId
                                                               RequestId = requestId
                                                               GitTreeHash = tree
                                                               LastWordsRef = blob.BlobRef
                                                               LastWordsDigest = blob.BlobDigest
                                                               ProviderRun = context.ProviderRunId.Value
                                                               ToolCallId = context.ToolCallId.Value |})
                                                        journal
                                                    |> Result.mapError (fun failure ->
                                                        raise (
                                                            InvalidOperationException(
                                                                sprintf
                                                                    "FinalityRequested append failed: %s"
                                                                    (JournalAppendFailure.describe failure)
                                                            )
                                                        ))
                                                    |> ignore

                                                    let! outcome =
                                                        FinalityController.start
                                                            scope
                                                            sid
                                                            life.LifeId
                                                            requestId
                                                            tree
                                                            blob.BlobRef
                                                            blob.BlobDigest
                                                            context.ProviderRunId.Value
                                                            (defaultArg
                                                                scope.FinalityReviewerTimeoutMs
                                                                ExecutorSummarize.AwaitAgentTimeoutMs)

                                                    match outcome with
                                                    | FinalityController.FinalityOutcome.Rejected prompt
                                                    | FinalityController.FinalityOutcome.Blessed prompt
                                                    | FinalityController.FinalityOutcome.Undecided prompt ->
                                                        return prompt
                            }

                        match effectiveLife with
                        // GLORY-039: still no Life — the HumanRoot Manager is planning.
                        | None -> return ToolHostCodec.tomlObjectWithInstructions [ "Continue working." ] []
                        | Some life when life.ProtectedPrefixEnd.IsNone ->
                            return ToolHostCodec.tomlObjectWithInstructions [ "Continue working." ] []
                        | Some life when life.Completed ->
                            return ToolHostCodec.tomlObject [ "status", tString "already_completed" ]
                        | Some life -> return! acceptSuicide life
            | Some _ -> return ToolHostCodec.tomlObjectWithInstructions [ "Do not call suicide from this role." ] []
            | None -> return ToolHostCodec.tomlObjectWithInstructions [ "Continue working and try again later." ] []
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "suicide"
          Description = "End your life when your task is complete."
          Arguments = [ "last_words", ToolHostCodec.stringSchema factory ]
          Execute = execute scope }
