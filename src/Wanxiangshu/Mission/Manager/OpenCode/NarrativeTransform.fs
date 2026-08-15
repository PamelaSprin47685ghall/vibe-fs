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
open FsToolkit.ErrorHandling
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

    let private activeProfile (journal: AgentJournal) (sessionId: SessionId) =
        PromptAuthorityLedger.activeProfile sessionId (AgentJournal.snapshot journal).AgentProjections

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
        traceState
        |> Option.bind (fun state ->
            state.Parts
            |> List.tryFind (fun part -> part.Turn = turnIndex && part.PartIndex = 0)
            |> Option.map (fun part -> part.Cursor.Sequence))

    /// The `PromptKey` a Host message carries in its metadata (PROMPT-011).
    /// Read via the Codec boundary (ProviderWireDecode.promptKeyOfMessage).
    let private promptKeyOfMessage (raw: obj) =
        ProviderWireDecode.promptKeyOfMessage raw

    let private promptKeyOrEmpty (raw: obj) =
        promptKeyOfMessage raw |> Option.defaultValue (PromptKey.create "")

    let private provenancesOf (parts: XTracePartRef list) =
        parts |> List.map (fun part -> part.Provenance) |> Set.ofList

    let private isMessageFromCompletedLife (traceState: XTraceProjectionState option) (messageId: string) =
        option {
            let! state = traceState
            let! _ = state.Terminal
            return provenancesOf state.Parts
        }
        |> Option.exists (Set.contains messageId)

    let private isEndingEvidence (text: string) =
        endingEvidenceFragments.Value
        |> List.exists (fun fragment -> text.Contains(fragment, StringComparison.OrdinalIgnoreCase))

    let private partIsEndingEvidence part =
        match part with
        | ProviderProjection.WireToolCall(_callId, name, _args) -> name = "suicide"
        | ProviderProjection.WireToolResult(_callId, result) -> isEndingEvidence result
        | ProviderProjection.WireText text -> isEndingEvidence text
        | _ -> false

    let private messageHasEndingEvidence (raw: obj) =
        ProviderWireCapture.decodeMessage raw
        |> Option.exists (fun message -> message.Parts |> List.exists partIsEndingEvidence)

    let private hasSuicideAfter (rawMessages: obj list) (messageIndex: int) =
        messageIndex < List.length rawMessages - 1
        && (rawMessages
            |> List.skip (messageIndex + 1)
            |> List.exists messageHasEndingEvidence)

    let private narrativePartObj (part: ManagerNarrative.NarrativePart) =
        if part.Synthetic then
            createObj [ "type", box "text"; "text", box part.Text; "synthetic", box true ]
        else
            createObj [ "type", box "text"; "text", box part.Text ]

    let private isNonTextPart (part: obj) =
        match ProviderWireDecode.decodePart part with
        | Some(ProviderProjection.WireText _) -> false
        | _ -> true

    let private rewriteRawMessage (raw: obj) (projection: ManagerNarrative.NarrativeProjection) =
        let parts = ProviderWireDecode.rawPartsOf raw

        let narrativeParts = projection.Parts |> List.map narrativePartObj |> List.toArray

        let nonText = parts |> List.filter isNonTextPart |> List.toArray
        let rewritten = Array.append narrativeParts nonText

        // Clone the message and replace only its parts: every other field
        // (info id/role/sessionID, metadata, timing) must survive verbatim,
        // whatever shape this Host version emits.
        let cloned = emitJsExpr raw "Object.assign({}, $0)"
        cloned?parts <- box rewritten
        cloned

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
                rewriteRawMessage raw projection)

    let private chooseBirthOrReawakening
        (sessionId: SessionId)
        (completedLives: LifeProjection list)
        (rawText: string)
        =
        if List.isEmpty completedLives then
            birthProjection sessionId rawText
        else
            reawakeningProjection sessionId rawText

    let private chooseBirthOrReawakeningByCount (sessionId: SessionId) (completedLifeCount: int) (rawText: string) =
        if completedLifeCount = 1 then
            birthProjection sessionId rawText
        else
            reawakeningProjection sessionId rawText

    let private narrativeWhenNonEmpty (choose: string -> ManagerNarrative.NarrativeProjection) (rawText: string) =
        if String.IsNullOrWhiteSpace rawText then
            None
        else
            Some(choose rawText)

    let private textPartsOf (message: ProviderProjection.WireMessage) =
        message.Parts
        |> List.choose (fun part ->
            match part with
            | ProviderProjection.WireText text -> Some text
            | _ -> None)
        |> String.concat "\n"

    let private fallbackMessageText (rawMessages: obj list) (messageIndex: int) =
        ProviderWireCapture.decodeMessage (List.item messageIndex rawMessages)
        |> Option.map textPartsOf
        |> Option.defaultValue ""

    let private readOpeningText (durable: AgentJournal) (rawMessages: obj list) (blobRef: BlobRef) (messageIndex: int) =
        task {
            match! durable.Writer.BlobWriter.Read blobRef with
            | Ok text -> return text
            | Error _ -> return fallbackMessageText rawMessages messageIndex
        }

    let private findMessageIndex (rawMessages: obj list) (messageId: string) =
        rawMessages
        |> List.tryFindIndex (fun raw -> ProviderWireDecode.hostMessageId raw = Some messageId)

    let private rewriteIndexedOpening
        (rawMessages: obj list)
        (messageIndex: int)
        (rawText: string)
        (choose: string -> ManagerNarrative.NarrativeProjection)
        =
        narrativeWhenNonEmpty choose rawText
        |> Option.map (fun narrative -> rewriteMessage rawMessages messageIndex narrative)

    let private rewriteOpenLifeOpening
        (durable: AgentJournal)
        (sid: SessionId)
        (lifecycle: ManagerLifeProjection)
        (life: LifeProjection)
        (rawMessages: obj list)
        : Task<obj list option> =
        task {
            match findMessageIndex rawMessages (PhysicalUserMessageId.value life.OpeningUserMessageId) with
            | None -> return None
            | Some messageIndex ->
                let! rawText = readOpeningText durable rawMessages life.OpeningTextRef messageIndex

                return
                    rewriteIndexedOpening
                        rawMessages
                        messageIndex
                        rawText
                        (chooseBirthOrReawakening sid lifecycle.CompletedLives)
        }

    let private isTitleRequestMessage (raw: obj) =
        let fromContent =
            ProviderWireDecode.topLevelString raw "content"
            |> Option.exists (fun text ->
                text.StartsWith("Generate a title for this conversation:", StringComparison.Ordinal))

        let fromParts =
            ProviderWireCapture.decodeMessage raw
            |> Option.exists (fun message ->
                message.Parts
                |> List.exists (fun part ->
                    match part with
                    | ProviderProjection.WireText text ->
                        text.StartsWith("Generate a title for this conversation:", StringComparison.Ordinal)
                    | _ -> false))

        fromContent || fromParts

    let private shouldSkipOpening (raw: obj) =
        isTitleRequestMessage raw || ProviderWireDecode.isCompactionMarker raw

    let private shouldPreserveCompletedOpening
        (durable: AgentJournal)
        (sid: SessionId)
        (messageId: string)
        (messageIndex: int)
        (rawMessages: obj list)
        (traceState: XTraceProjectionState option)
        =
        isAcceptedContinuation durable sid messageId
        || isMessageFromCompletedLife traceState messageId
        || hasSuicideAfter rawMessages messageIndex

    let private completedOpeningTarget (lifecycle: ManagerLifeProjection) (rawMessages: obj list) =
        lifecycle.CompletedLives
        |> List.tryHead
        |> Option.bind (fun completedLife ->
            findMessageIndex rawMessages (PhysicalUserMessageId.value completedLife.OpeningUserMessageId)
            |> Option.map (fun index -> completedLife, index))

    let private rewriteCompletedOpening
        (durable: AgentJournal)
        (sid: SessionId)
        (lifecycle: ManagerLifeProjection)
        (rawMessages: obj list)
        : Task<obj list option> =
        task {
            match completedOpeningTarget lifecycle rawMessages with
            | None -> return None
            | Some(completedLife, completedOpeningIndex) ->
                let! rawText = readOpeningText durable rawMessages completedLife.OpeningTextRef completedOpeningIndex

                return
                    rewriteIndexedOpening
                        rawMessages
                        completedOpeningIndex
                        rawText
                        (chooseBirthOrReawakeningByCount sid (List.length lifecycle.CompletedLives))
        }

    let private migrationCandidate (traceState: XTraceProjectionState option) (completedLives: LifeProjection list) =
        match
            traceState
            |> Option.bind (fun state -> state.Opening |> Option.map (fun opening -> state, opening))
        with
        | Some(state, opening) when
            List.isEmpty completedLives
            && state.Parts |> List.exists (fun part -> part.Turn <> 0)
            ->
            Some(opening, XTraceProjection.headSequence state + 1L)
        | _ -> None

    let private requireWorkflowUnit (result: Result<unit, string>) =
        match result with
        | Error error -> raise (InvalidOperationException error)
        | Ok() -> ()

    let private migrateExistingLife
        (durable: AgentJournal)
        (sid: SessionId)
        (messageIdValue: PhysicalUserMessageId)
        (lifecycle: ManagerLifeProjection)
        (traceState: XTraceProjectionState option)
        =
        task {
            match migrationCandidate traceState lifecycle.CompletedLives with
            | None -> return false
            | Some(opening, cursor) ->
                let lifeId = ManagerLifeId.create (Guid.NewGuid().ToString("N"))

                let! result =
                    ManagerLifeWorkflow.ensureMigrated durable sid lifeId messageIdValue opening.AssignmentText cursor

                requireWorkflowUnit result
                return true
        }

    let private openNewHumanRootLife
        (durable: AgentJournal)
        (sid: SessionId)
        (lifecycle: ManagerLifeProjection)
        (traceState: XTraceProjectionState option)
        (rawMessages: obj list)
        (messageIndex: int)
        (messageIdValue: PhysicalUserMessageId)
        : Task<obj list option> =
        task {
            let rawText = fallbackMessageText rawMessages messageIndex

            match narrativeWhenNonEmpty (chooseBirthOrReawakening sid lifecycle.CompletedLives) rawText with
            | None -> return None
            | Some narrative ->
                let lifeId = ManagerLifeId.create (Guid.NewGuid().ToString("N"))

                let cursor =
                    openingCursorOf traceState messageIndex
                    |> Option.defaultValue (
                        traceState |> Option.map XTraceProjection.headSequence |> Option.defaultValue 0L
                    )

                let! result = ManagerLifeWorkflow.ensureOpening durable sid lifeId messageIdValue rawText cursor

                requireWorkflowUnit result
                return Some(rewriteMessage rawMessages messageIndex narrative)
        }

    let private openAfterMigrationCheck
        (durable: AgentJournal)
        (sid: SessionId)
        (lifecycle: ManagerLifeProjection)
        (traceState: XTraceProjectionState option)
        (rawMessages: obj list)
        (messageIndex: int)
        (messageId: string)
        : Task<obj list option> =
        task {
            let messageIdValue = PhysicalUserMessageId.create messageId

            // GLORY-069: upgrade an already-active Manager with
            // historical XTrace into a migration Life, without
            // manufacturing a new Birth.
            let! migrated = migrateExistingLife durable sid messageIdValue lifecycle traceState

            if migrated then
                return None
            else
                return! openNewHumanRootLife durable sid lifecycle traceState rawMessages messageIndex messageIdValue
        }

    let private tryOpenHumanRootLife
        (durable: AgentJournal)
        (sid: SessionId)
        (lifecycle: ManagerLifeProjection)
        (traceState: XTraceProjectionState option)
        (rawMessages: obj list)
        : Task<obj list option> =
        task {
            match activeProfile durable sid with
            | None -> return None
            | Some profile ->
                let rootMessageId = AuthorityRootUserMessageId.value profile.AuthorityRootUserMessageId

                match findMessageIndex rawMessages rootMessageId with
                | None -> return None
                | Some messageIndex ->
                    let physicalMessageId = PhysicalUserMessageId.create rootMessageId

                    match ManagerLifeAdmission.tryHumanRootOpening lifecycle (Some profile) physicalMessageId with
                    | None -> return None
                    | Some evidence ->
                        let admittedMessageId = HumanRootOpeningEvidence.messageId evidence

                        return!
                            openAfterMigrationCheck
                                durable
                                sid
                                lifecycle
                                traceState
                                rawMessages
                                messageIndex
                                (PhysicalUserMessageId.value admittedMessageId)
        }

    let private transformWithJournal
        (durable: AgentJournal)
        (sessionIdValue: string)
        (traceState: XTraceProjectionState option)
        (rawMessages: obj list)
        : Task<obj list option> =
        task {
            let sid = SessionId.create sessionIdValue
            let snapshot = AgentJournal.snapshot durable

            let lifecycle =
                AgentProjection.tryFind sid snapshot.AgentProjections
                |> Option.bind (fun session -> session.ManagerLife)
                |> Option.defaultValue ManagerLifecycleProjection.empty

            match lifecycle.CurrentLife with
            // A Life is open: its Opening message is rewritten on EVERY provider
            // request. The durable opening remains authoritative; the current
            // wire text is only a best-effort fallback when the blob is unavailable.
            | Some life -> return! rewriteOpenLifeOpening durable sid lifecycle life rawMessages
            // No open Life: a new HumanRoot opens one (GLORY-012/063).
            | None -> return! tryOpenHumanRootLife durable sid lifecycle traceState rawMessages
        }

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
                return! transformWithJournal durable sessionIdValue traceState rawMessages
        }

    let private partIsWorkActivationAnchor part =
        match part with
        | ProviderProjection.WireText text ->
            workActivationAnchors.Value
            |> List.exists (fun anchor -> text.Contains(anchor, StringComparison.Ordinal))
        | _ -> false

    let private isWorkActivationMessage (raw: obj) =
        ProviderWireCapture.decodeMessage raw
        |> Option.exists (fun message ->
            message.Role = "user" && message.Parts |> List.exists partIsWorkActivationAnchor)

    let private lifeNeedingActivation (lifecycle: ManagerLifeProjection) =
        match lifecycle.CurrentLife with
        | Some life when life.ProtectedPrefixEnd.IsNone && not life.Completed -> Some life
        | _ -> None

    let private commitActivation
        (durable: AgentJournal)
        (sid: SessionId)
        (lifeId: ManagerLifeId)
        (promptKey: PromptKey)
        (protectedPrefixEnd: int64)
        =
        task {
            match! ManagerLifeWorkflow.acceptActivation durable sid lifeId promptKey protectedPrefixEnd with
            | Error error -> return raise (InvalidOperationException error)
            | Ok() -> ()
        }

    let private acceptActivationIfEligible
        (durable: AgentJournal)
        (sessionIdValue: string)
        (state: XTraceProjectionState)
        (rawMessages: obj list)
        =
        task {
            let sid = SessionId.create sessionIdValue
            let snapshot = AgentJournal.snapshot durable

            let lifecycle =
                AgentProjection.tryFind sid snapshot.AgentProjections
                |> Option.bind (fun session -> session.ManagerLife)
                |> Option.defaultValue ManagerLifecycleProjection.empty

            match
                lifeNeedingActivation lifecycle
                |> Option.bind (fun life ->
                    rawMessages
                    |> List.tryFind isWorkActivationMessage
                    |> Option.map (fun raw -> life, raw))
            with
            | None -> ()
            | Some(life, raw) ->
                let protectedPrefixEnd = XTraceProjection.headSequence state + 1L
                do! commitActivation durable sid life.LifeId (promptKeyOrEmpty raw) protectedPrefixEnd
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
            | Some durable, Some sessionIdValue, Some state ->
                do! acceptActivationIfEligible durable sessionIdValue state rawMessages
            | _ -> ()
        }
