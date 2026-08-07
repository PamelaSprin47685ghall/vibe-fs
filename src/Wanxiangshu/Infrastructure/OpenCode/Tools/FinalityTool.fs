namespace Wanxiangshu.OpenCode

open System
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
        match scope.RuntimeFor context with
        | Ok runtime ->
            // TEMP DIAG: outstanding work (removed before merge).
            emitJsExpr
                (sprintf "ow run=%d comp=%d" runtime.PendingRunCount runtime.PendingCompletionCount)
                "require('node:fs').appendFileSync('/tmp/fc.log', $0 + '\\n')"

            runtime.PendingRunCount > 0
            || runtime.PendingCompletionCount > 0
            || scope.HasLivePty context.SessionId
        | Error _ -> scope.HasLivePty context.SessionId

    /// GLORY-068/069: an AgentOwnerRoot Manager (an Orchestrator's ManagerJob)
    /// has no HumanRoot and therefore no Life. Its ending still goes through
    /// suicide; build the migration Life on acceptance so the Finality workflow
    /// has an identity. Idempotent: an existing Life is returned as-is.
    let private ensureMigrationLife (journal: AgentJournal) (sessionId: string) : ManagerLifeId option =
        let sid = SessionId.create sessionId
        let snapshot = AgentJournal.snapshot journal

        match AgentProjection.tryFind sid snapshot.AgentProjections with
        | None -> None
        | Some session ->
            match session.ManagerLife with
            | Some existing -> existing.CurrentLife |> Option.map (fun life -> life.LifeId)
            | None ->
                match session.XTrace with
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
                                        sprintf
                                            "Life migration append failed: %s"
                                            (JournalAppendFailure.describe failure)
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

    let private execute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            let lastWords = args.Text "last_words"

            match scope.RoleFor context with
            | Some Role.Manager ->
                if String.IsNullOrWhiteSpace context.SessionId then
                    return
                        ToolHostCodec.tomlObject [ "error", tString "The ending cannot be entered without a session." ]
                else
                    let sessionId = context.SessionId
                    let sid = SessionId.create sessionId

                    match scope.Journal with
                    | None ->
                        return
                            ToolHostCodec.tomlObject
                                [ "error", tString "The ending cannot be entered because the journal is unavailable." ]
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
                                // TEMP DIAG: finality request (removed before merge).
                                | Some request when not request.Rejected && not request.Confirmed ->
                                    emitJsExpr
                                        (sprintf
                                            "af-open req=%s rev=%b call=%A"
                                            (FinalityRequestId.value request.RequestId)
                                            (Option.isSome request.ReviewerSessionId)
                                            context.ToolCallId)
                                        "require('node:fs').appendFileSync('/tmp/fc.log', $0 + '\\n')"

                                    match context.ToolCallId with
                                    | Some callId when callId = request.ToolCallId ->
                                        return
                                            ToolHostCodec.tomlObject
                                                [ "status", tString "already_received"
                                                  "message", tString "Your final words have already been received." ]
                                    | _ ->
                                        // Crash recovery (docs/how/glory.md matrix):
                                        // a request with no `FinalityReviewStarted`
                                        // died before the Reviewer fork. Restart the
                                        // Finality workflow on the SAME request; the
                                        // fold keeps the request open until the
                                        // restart lands a terminal fact.
                                        if request.ReviewerSessionId.IsNone then
                                            FinalityController.start
                                                scope
                                                sid
                                                life.LifeId
                                                request.RequestId
                                                request.GitTreeHash
                                                request.LastWordsRef
                                                request.LastWordsDigest
                                                request.ProviderRun
                                            |> ignore

                                        return
                                            ToolHostCodec.tomlObject
                                                [ "error", tString "Your ending is already in motion." ]
                                | other ->
                                    emitJsExpr
                                        (sprintf "af-other=%b" (Option.isSome other))
                                        "require('node:fs').appendFileSync('/tmp/fc.log', $0 + '\\n')"

                                    if String.IsNullOrWhiteSpace lastWords then
                                        return ToolHostCodec.tomlObject [ "error", tString "Final words are required." ]
                                    elif context.ToolCallId.IsNone then
                                        return
                                            ToolHostCodec.tomlObject
                                                [ "error",
                                                  tString "The ending cannot be entered without a tool call identity." ]
                                    elif context.ProviderRunId.IsNone then
                                        return
                                            ToolHostCodec.tomlObject
                                                [ "error",
                                                  tString "The ending cannot be entered without a run identity." ]
                                    elif outstandingWork scope context then
                                        // TEMP DIAG: suicide rejection (removed before merge).
                                        emitJsExpr
                                            (sprintf
                                                "outstanding sid=%s call=%A run=%A"
                                                context.SessionId
                                                context.ToolCallId
                                                context.ProviderRunId)
                                            "require('node:fs').appendFileSync('/tmp/fc.log', $0 + '\\n')"
                                        // GLORY-038: background work still walks the world.
                                        return
                                            ToolHostCodec.tomlObject
                                                [ "error",
                                                  tString
                                                      "Your work still walks the world.\nGather what remains before seeking your end." ]
                                    else
                                        // GLORY-037.14/15: the tree must be readable.
                                        match treeOf scope sessionId with
                                        | None ->
                                            // TEMP DIAG: finality tree (removed before merge).
                                            emitJsExpr
                                                ()
                                                "require('node:fs').appendFileSync('/tmp/fc.log', 'no-tree\\n')"

                                            return
                                                ToolHostCodec.tomlObject
                                                    [ "error", tString "Your ending could not be entered.\nContinue." ]
                                        | Some tree ->
                                            match journal.WriteBlob lastWords with
                                            | Error err ->
                                                // TEMP DIAG: finality blob (removed before merge).
                                                emitJsExpr
                                                    (sprintf "blob-ERR=%s" (string err))
                                                    "require('node:fs').appendFileSync('/tmp/fc.log', $0 + '\\n')"

                                                return
                                                    ToolHostCodec.tomlObject
                                                        [ "error",
                                                          tString "Your ending could not be entered.\nContinue." ]
                                            | Ok blob ->
                                                // GLORY-040: accept in order. The Finality
                                                // workflow is fire-and-forget; every outcome
                                                // lands on the journal before any side effect.
                                                let requestId = FinalityRequestId.create (Guid.NewGuid().ToString("N"))

                                                // TEMP DIAG: finality append (removed before merge).
                                                emitJsExpr
                                                    (sprintf "append-ok=%b" true)
                                                    "require('node:fs').appendFileSync('/tmp/fc.log', $0 + '\\n')"

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
                                                    // TEMP DIAG: finality append (removed before merge).
                                                    emitJsExpr
                                                        (sprintf
                                                            "append-ERR=%s"
                                                            (JournalAppendFailure.describe failure))
                                                        "require('node:fs').appendFileSync('/tmp/fc.log', $0 + '\\n')"

                                                    raise (
                                                        InvalidOperationException(
                                                            sprintf
                                                                "FinalityRequested append failed: %s"
                                                                (JournalAppendFailure.describe failure)
                                                        )
                                                    ))
                                                |> ignore

                                                FinalityController.start
                                                    scope
                                                    sid
                                                    life.LifeId
                                                    requestId
                                                    tree
                                                    blob.BlobRef
                                                    blob.BlobDigest
                                                    context.ProviderRunId.Value
                                                |> ignore

                                                // GLORY-041: the Manager sees only the
                                                // narrative; the physical run stops here.
                                                return
                                                    ToolHostCodec.tomlObjectWithInstructions
                                                        [ "Your final words have been received." ]
                                                        []
                            }

                        match effectiveLife with
                        // GLORY-039: still no Life — the HumanRoot Manager is planning.
                        | None ->
                            // TEMP DIAG: suicide rejection (removed before merge).
                            emitJsExpr () "require('node:fs').appendFileSync('/tmp/fc.log', 'no-life\\n')"

                            return
                                ToolHostCodec.tomlObject [ "error", tString "Your work has not yet begun.\nContinue." ]
                        | Some life when life.ProtectedPrefixEnd.IsNone ->
                            // TEMP DIAG: suicide rejection (removed before merge).
                            emitJsExpr
                                (sprintf "not-activated")
                                "require('node:fs').appendFileSync('/tmp/fc.log', $0 + '\\n')"

                            return
                                ToolHostCodec.tomlObject [ "error", tString "Your work has not yet begun.\nContinue." ]
                        | Some life when life.Completed ->
                            return ToolHostCodec.tomlObject [ "status", tString "already_completed" ]
                        | Some life ->
                            // TEMP DIAG: suicide rejection (removed before merge).
                            emitJsExpr
                                (sprintf
                                    "accepting sid=%s call=%A run=%A"
                                    context.SessionId
                                    context.ToolCallId
                                    context.ProviderRunId)
                                "require('node:fs').appendFileSync('/tmp/fc.log', $0 + '\\n')"

                            return! acceptSuicide life
            | Some _ -> return ToolHostCodec.tomlObject [ "error", tString "The ending is not yours to call." ]
            | None ->
                return
                    ToolHostCodec.tomlObject
                        [ "error", tString "The ending cannot be entered without an accepted role." ]
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "suicide"
          Description = "End your life when your task is complete."
          Arguments = [ "last_words", ToolHostCodec.stringSchema factory ]
          Execute = execute scope }
