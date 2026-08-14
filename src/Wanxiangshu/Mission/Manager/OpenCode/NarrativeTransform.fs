namespace Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity

/// Provider-facing Birth/Reawakening rewrite from durable Life projection + raw messages.
module ManagerNarrativeTransform =

    let private planningTableDocument (sessionId: SessionId) =
        ProviderProse.documentFor sessionId ManagerNarrative.Path.PlanningTable Map.empty

    let private reawakeningDocument (sessionId: SessionId) =
        ProviderProse.documentFor sessionId ManagerNarrative.Path.Reawakening Map.empty

    let private birthProjection (sessionId: SessionId) (rawText: string) =
        ManagerNarrative.firstBirth rawText (planningTableDocument sessionId)

    let private reawakeningProjection (sessionId: SessionId) (rawText: string) =
        ManagerNarrative.reawakening rawText (reawakeningDocument sessionId) (planningTableDocument sessionId)

    /// Ending-evidence anchors from Finality prose in every ProviderLanguage.
    let private endingEvidenceFragments =
        lazy
            ([ ProviderLanguage.English; ProviderLanguage.SimplifiedChinese ]
             |> List.collect (fun lang ->
                 [ FinalityPrompt.Path.Rest
                   FinalityPrompt.Path.Rejected
                   FinalityPrompt.Path.Blessed ]
                 |> List.collect (fun path ->
                     ProviderProse.instructionLines lang path Map.empty
                     |> List.filter (fun line -> not (String.IsNullOrWhiteSpace line))))
             |> List.distinct)

    let private workActivationAnchors =
        lazy
            ([ ProviderLanguage.English; ProviderLanguage.SimplifiedChinese ]
             |> List.collect (fun lang ->
                 ProviderProse.instructionLines lang ManagerLifecyclePrompt.Path.WorkActivation Map.empty
                 |> List.filter (fun line -> not (String.IsNullOrWhiteSpace line))
                 |> List.truncate 1)
             |> List.distinct)

    /// GLORY-012: only a HumanRoot-managed Manager opens Lives. An
    /// AgentOwnerRoot Manager (an Orchestrator's forked ManagerJob) receives
    /// assignments from the Host, not from a user, and must not be rewritten.
    let private isHumanRootManager (journal: AgentJournal) (sessionId: SessionId) =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.PromptAuthority)
        |> Option.bind (fun authority -> authority.ActiveLogicalRun)
        |> Option.exists (fun profile ->
            profile.CanonicalRole = Role.Manager
            && profile.AuthorityKind = PromptAuthority.RootAuthorityKind.HumanRoot)

    /// PROMPT-009: a message the Dispatcher proved to be a continuation. Such a
    /// message never opens a Life (GLORY-012).
    let private isAcceptedContinuation (journal: AgentJournal) (sessionId: SessionId) (messageId: string) =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.PromptAuthority)
        |> Option.exists (fun authority ->
            authority.AcceptedContinuationIds
            |> Map.exists (fun id _ -> PhysicalUserMessageId.value id = messageId))

    /// The last `role=user` message and its raw object, or `None`.
    let private lastUserMessage (rawMessages: obj list) =
        rawMessages
        |> List.mapi (fun index raw -> index, raw)
        |> List.choose (fun (index, raw) ->
            match ProviderWireCapture.decodeMessage raw, ProviderWireDecode.hostMessageId raw with
            | Some message, Some id when message.Role = "user" -> Some(index, id, raw)
            | _ -> None)
        |> List.tryLast

    /// The first XTrace cursor of the user message at semantic turn `turnIndex`.
    let private openingCursorOf (traceState: XTraceProjectionState option) (turnIndex: int) =
        match traceState with
        | None -> None
        | Some state ->
            state.Parts
            |> List.tryFind (fun part -> part.Turn = turnIndex && part.PartIndex = 0)
            |> Option.map (fun part -> part.Cursor.Sequence)

    /// The `PromptKey` a Host message carries in its metadata (PROMPT-011).
    /// Read via the Codec boundary (ProviderWireDecode.promptKeyOfMessage).
    let private promptKeyOfMessage (raw: obj) =
        ProviderWireDecode.promptKeyOfMessage raw

    let private isMessageFromCompletedLife (traceState: XTraceProjectionState option) (messageId: string) =
        match traceState with
        | None -> false
        | Some state ->
            match state.Terminal with
            | None -> false
            | Some _ ->
                let provenances = HashSet<string>()

                for part in state.Parts do
                    ignore (provenances.Add part.Provenance)

                provenances.Contains messageId

    let private hasSuicideAfter (rawMessages: obj list) (messageIndex: int) =
        if messageIndex >= List.length rawMessages - 1 then
            false
        else
            // GLORY-062: rest-in-peace / blessing tool results also prove the
            // ending already ran. Without them, the next provider step after
            // LifeCompleted re-opens a Life on the same HumanRoot (measured:
            // reawakening rewrite on the terminal text step).
            let isEndingEvidence (text: string) =
                endingEvidenceFragments.Value
                |> List.exists (fun fragment -> text.Contains(fragment, StringComparison.OrdinalIgnoreCase))

            rawMessages
            |> List.skip (messageIndex + 1)
            |> List.exists (fun raw ->
                match ProviderWireCapture.decodeMessage raw with
                | Some message ->
                    message.Parts
                    |> List.exists (function
                        | WireToolCall(_callId, name, _args) -> name = "suicide"
                        | WireToolResult(_callId, result) -> isEndingEvidence result
                        | WireText text -> isEndingEvidence text
                        | _ -> false)
                | None -> false)

    /// Replace the user message at `messageIndex` with multi-part narrative
    /// projection: human text part(s) + synthetic guidance parts (synthetic=true).
    /// Non-text parts (reasoning/tool/activity) pass through unchanged.
    let private rewriteMessage
        (rawMessages: obj list)
        (messageIndex: int)
        (projection: ManagerNarrative.NarrativeProjection)
        : obj list =
        rawMessages
        |> List.mapi (fun index raw ->
            if index <> messageIndex then
                raw
            else
                let parts = ProviderWireDecode.rawPartsOf raw

                let narrativeParts =
                    projection.Parts
                    |> List.map (fun part ->
                        if part.Synthetic then
                            createObj [ "type", box "text"; "text", box part.Text; "synthetic", box true ]
                        else
                            createObj [ "type", box "text"; "text", box part.Text ])
                    |> List.toArray

                let nonText =
                    parts
                    |> List.filter (fun part ->
                        match ProviderWireDecode.decodePart part with
                        | Some(WireText _) -> false
                        | _ -> true)
                    |> List.toArray

                let rewritten = Array.append narrativeParts nonText

                // Clone the message and replace only its parts: every other field
                // (info id/role/sessionID, metadata, timing) must survive verbatim,
                // whatever shape this Host version emits.
                let cloned = emitJsExpr raw "Object.assign({}, $0)"
                cloned?parts <- box rewritten
                cloned)

    /// GLORY-013 order (after X capture, before seal): open the Life and rewrite.
    ///
    /// Returns the rewritten message list when a Life was opened; `None` when
    /// nothing applies (non-Manager, no legal HumanRoot, Life already open,
    /// already injected, no journal).
    let tryTransform
        (journal: AgentJournal option)
        (sessionId: string option)
        (traceState: XTraceProjectionState option)
        (rawMessages: obj list)
        : Task<obj list option> =
        task {
            match journal, sessionId with
            | None, _
            | _, None -> return None
            | Some durable, Some sessionIdValue ->
                let sid = SessionId.create sessionIdValue
                let snapshot = AgentJournal.snapshot durable

                let lifecycle =
                    AgentProjection.tryFind sid snapshot.AgentProjections
                    |> Option.bind (fun session -> session.ManagerLife)
                    |> Option.defaultValue ManagerLifecycleProjection.empty

                let fallbackMessageText messageIndex =
                    match ProviderWireCapture.decodeMessage (List.item messageIndex rawMessages) with
                    | Some message ->
                        message.Parts
                        |> List.choose (function
                            | WireText text -> Some text
                            | _ -> None)
                        |> String.concat "\n"
                    | None -> ""

                let readOpeningText blobRef messageIndex =
                    task {
                        match! durable.Writer.BlobWriter.Read blobRef with
                        | Ok text -> return text
                        | Error _ -> return fallbackMessageText messageIndex
                    }

                match lifecycle.CurrentLife with
                // A Life is open: its Opening message is rewritten on EVERY provider
                // request. The durable opening remains authoritative; the current
                // wire text is only a best-effort fallback when the blob is unavailable.
                | Some life ->
                    let openingId = PhysicalUserMessageId.value life.OpeningUserMessageId

                    match
                        rawMessages
                        |> List.tryFindIndex (fun raw -> ProviderWireDecode.hostMessageId raw = Some openingId)
                    with
                    | None -> return None
                    | Some messageIndex ->
                        let! rawText = readOpeningText life.OpeningTextRef messageIndex

                        if String.IsNullOrWhiteSpace rawText then
                            return None
                        else
                            let narrative =
                                if List.isEmpty lifecycle.CompletedLives then
                                    birthProjection sid rawText
                                else
                                    reawakeningProjection sid rawText

                            return Some(rewriteMessage rawMessages messageIndex narrative)
                // No open Life: a new HumanRoot opens one (GLORY-012/063).
                | None ->
                    if not (isHumanRootManager durable sid) then
                        return None
                    else
                        match lastUserMessage rawMessages with
                        | None -> return None
                        | Some(messageIndex, messageId, raw) ->
                            let isTitleRequest =
                                let fromContent =
                                    ProviderWireDecode.topLevelString raw "content"
                                    |> Option.exists (fun text ->
                                        text.StartsWith(
                                            "Generate a title for this conversation:",
                                            StringComparison.Ordinal
                                        ))

                                fromContent
                                || (match ProviderWireCapture.decodeMessage raw with
                                    | Some message ->
                                        message.Parts
                                        |> List.exists (function
                                            | WireText text ->
                                                text.StartsWith(
                                                    "Generate a title for this conversation:",
                                                    StringComparison.Ordinal
                                                )
                                            | _ -> false)
                                    | None -> false)

                            if isTitleRequest || ProviderWireDecode.isCompactionMarker raw then
                                return None
                            elif
                                isAcceptedContinuation durable sid messageId
                                || isMessageFromCompletedLife traceState messageId
                                || hasSuicideAfter rawMessages messageIndex
                            then
                                // A post-completion continuation must keep the completed
                                // Life's opening rewrite byte-stable (ARCH-004).
                                match List.tryHead lifecycle.CompletedLives with
                                | None -> return None
                                | Some completedLife ->
                                    let openingId = PhysicalUserMessageId.value completedLife.OpeningUserMessageId

                                    match
                                        rawMessages
                                        |> List.tryFindIndex (fun item ->
                                            ProviderWireDecode.hostMessageId item = Some openingId)
                                    with
                                    | None -> return None
                                    | Some completedOpeningIndex ->
                                        let! rawText =
                                            readOpeningText completedLife.OpeningTextRef completedOpeningIndex

                                        if String.IsNullOrWhiteSpace rawText then
                                            return None
                                        else
                                            let narrative =
                                                if List.length lifecycle.CompletedLives = 1 then
                                                    birthProjection sid rawText
                                                else
                                                    reawakeningProjection sid rawText

                                            return Some(rewriteMessage rawMessages completedOpeningIndex narrative)
                            else
                                let messageIdValue = PhysicalUserMessageId.create messageId

                                // GLORY-069: upgrade an already-active Manager with
                                // historical XTrace into a migration Life, without
                                // manufacturing a new Birth.
                                let migrateExistingLife () =
                                    task {
                                        match traceState with
                                        | Some state ->
                                            let hasHistory = state.Parts |> List.exists (fun p -> p.Turn <> 0)

                                            match state.Opening, hasHistory with
                                            | Some opening, true when List.isEmpty lifecycle.CompletedLives ->
                                                let lifeId = ManagerLifeId.create (Guid.NewGuid().ToString("N"))

                                                match!
                                                    ManagerLifeWorkflow.ensureMigrated
                                                        durable
                                                        sid
                                                        lifeId
                                                        messageIdValue
                                                        opening.AssignmentText
                                                        (XTraceProjection.headSequence state + 1L)
                                                with
                                                | Error error -> return raise (InvalidOperationException error)
                                                | Ok() -> return true
                                            | _ -> return false
                                        | None -> return false
                                    }

                                let! migrated = migrateExistingLife ()

                                if migrated then
                                    return None
                                else
                                    let rawText = fallbackMessageText messageIndex

                                    if String.IsNullOrWhiteSpace rawText then
                                        return None
                                    else
                                        let narrative =
                                            if List.isEmpty lifecycle.CompletedLives then
                                                birthProjection sid rawText
                                            else
                                                reawakeningProjection sid rawText

                                        let lifeId = ManagerLifeId.create (Guid.NewGuid().ToString("N"))

                                        let cursor =
                                            openingCursorOf traceState messageIndex
                                            |> Option.defaultValue (
                                                traceState
                                                |> Option.map XTraceProjection.headSequence
                                                |> Option.defaultValue 0L
                                            )

                                        match!
                                            ManagerLifeWorkflow.ensureOpening
                                                durable
                                                sid
                                                lifeId
                                                messageIdValue
                                                rawText
                                                cursor
                                        with
                                        | Error error -> return raise (InvalidOperationException error)
                                        | Ok() -> return Some(rewriteMessage rawMessages messageIndex narrative)
        }

    /// GLORY-021 legacy: if a historical Activation message is still in the wire,
    /// append inert WorkActivated. Production BlindPlan never sends Activation;
    /// Opening floor is effectiveOpeningFloor / T1 (TODO-001).
    let applyAcceptedActivation
        (journal: AgentJournal option)
        (sessionId: string option)
        (traceState: XTraceProjectionState option)
        (rawMessages: obj list)
        : Task =
        task {
            match journal, sessionId, traceState with
            | None, _, _
            | _, None, _
            | _, _, None -> ()
            | Some durable, Some sessionIdValue, Some state ->
                let sid = SessionId.create sessionIdValue
                let snapshot = AgentJournal.snapshot durable

                let lifecycle =
                    AgentProjection.tryFind sid snapshot.AgentProjections
                    |> Option.bind (fun session -> session.ManagerLife)
                    |> Option.defaultValue ManagerLifecycleProjection.empty

                match lifecycle.CurrentLife with
                | Some life when life.ProtectedPrefixEnd.IsNone && not life.Completed ->
                    let activationMessage =
                        rawMessages
                        |> List.tryFind (fun raw ->
                            match ProviderWireCapture.decodeMessage raw with
                            | Some message when message.Role = "user" ->
                                message.Parts
                                |> List.exists (function
                                    | WireText text ->
                                        workActivationAnchors.Value
                                        |> List.exists (fun anchor -> text.Contains(anchor, StringComparison.Ordinal))
                                    | _ -> false)
                            | _ -> false)

                    match activationMessage with
                    | None -> ()
                    | Some raw ->
                        let protectedPrefixEnd = XTraceProjection.headSequence state + 1L

                        let promptKey =
                            match promptKeyOfMessage raw with
                            | Some key -> key
                            | None -> PromptKey.create ""

                        match!
                            ManagerLifeWorkflow.acceptActivation durable sid life.LifeId promptKey protectedPrefixEnd
                        with
                        | Error error -> return raise (InvalidOperationException error)
                        | Ok() -> ()
                | _ -> ()
        }
