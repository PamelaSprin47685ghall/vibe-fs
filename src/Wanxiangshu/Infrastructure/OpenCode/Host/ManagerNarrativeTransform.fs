namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

/// GLORY-013/014/015/063/064: the provider-facing Birth / Reawakening rewrite.
///
/// Runs at the transform boundary AFTER `XTraceCapture.captureProjection`
/// (durable X keeps the raw HumanRoot) and BEFORE `ReviewSeal` (the seal
/// digests the bytes the provider actually receives). Only a Manager session
/// with a legal new HumanRoot is rewritten; every other message passes through
/// untouched (GLORY-007/026).
module ManagerNarrativeTransform =

    let private readField (value: obj) (name: string) : obj =
        if isNull value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

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

    /// The Host compaction pseudo-run marker (SessionSnapshotPort): any of
    /// `agent`/`mode` = "compaction" or `summary` = true.
    let private isCompactionMarker (raw: obj) =
        let info =
            if isNull raw then null
            elif not (isNull raw?info) then raw?info
            else raw

        if isNull info then
            false
        else
            let label (name: string) =
                let v = info?name

                if isNull v then "" else string v

            (label "agent").ToLowerInvariant() = "compaction"
            || (label "mode").ToLowerInvariant() = "compaction"
            || (label "summary").ToLowerInvariant() = "true"

    /// The last `role=user` message and its raw object, or `None`.
    let private lastUserMessage (rawMessages: obj list) =
        rawMessages
        |> List.mapi (fun index raw -> index, raw)
        |> List.choose (fun (index, raw) ->
            match Projection.decodeMessage raw, Projection.hostMessageId raw with
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
    let private promptKeyOfMessage (raw: obj) =
        let fromMetadata (source: obj) =
            if isNull source || isNull source?metadata then
                None
            else
                let value = source?metadata?(PromptMetadataCodec.PromptKeyField)

                if isNull value then None else Some(unbox<string> value)

        let info =
            if isNull raw then null
            elif not (isNull raw?info) then raw?info
            else raw

        let fromParts () =
            if isNull raw || isNull raw?parts then
                None
            else
                unbox<obj array> raw?parts
                |> Array.tryPick (fun part -> if isNull part then None else fromMetadata part)

        [ fromMetadata info; fromMetadata raw; fromParts () ]
        |> List.tryPick id
        |> Option.map PromptKey.create

    let private isMessageFromCompletedLife (traceState: XTraceProjectionState option) (messageId: string) =
        match traceState with
        | None -> false
        | Some state ->
            match state.Terminal with
            | None -> false
            | Some _ -> state.Parts |> List.exists (fun part -> part.Provenance = messageId)

    let private hasSuicideAfter (rawMessages: obj list) (messageIndex: int) =
        if messageIndex >= List.length rawMessages - 1 then
            false
        else
            rawMessages
            |> List.skip (messageIndex + 1)
            |> List.exists (fun raw ->
                match Projection.decodeMessage raw with
                | Some message ->
                    message.Parts
                    |> List.exists (function
                        | WireToolCall(_callId, name, _args) -> name = "suicide"
                        | WireToolResult(_callId, result) -> result.Contains("Your final words have been received")
                        | WireText text -> text.Contains("Your final words have been received")
                        | _ -> false)
                | None -> false)

    /// Replace the text of the user message at `messageIndex` with the narrative
    /// text. Only text parts are touched; reasoning/tool/activity parts pass
    /// through unchanged.
    let private rewriteMessage (rawMessages: obj list) (messageIndex: int) (narrative: string) : obj list =
        rawMessages
        |> List.mapi (fun index raw ->
            if index <> messageIndex then
                raw
            else
                let parts =
                    if isNull raw || isNull raw?parts then
                        [||]
                    else
                        unbox<obj array> raw?parts

                let rewritten =
                    parts
                    |> Array.map (fun part ->
                        let kind =
                            if isNull part then
                                ""
                            else
                                let value = readField part "type"
                                if isNull value then "" else unbox<string> value

                        if kind = "text" then
                            createObj [ "type", box "text"; "text", box narrative ]
                        else
                            part)

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
        : obj list option =
        match journal, sessionId with
        | None, _
        | _, None -> None
        | Some durable, Some sessionIdValue ->
            let sid = SessionId.create sessionIdValue
            let snapshot = AgentJournal.snapshot durable

            let lifecycle =
                AgentProjection.tryFind sid snapshot.AgentProjections
                |> Option.bind (fun session -> session.ManagerLife)
                |> Option.defaultValue ManagerLifecycleProjection.empty

            match lifecycle.CurrentLife with
            // A Life is open: its Opening message is rewritten on EVERY provider
            // request — the transform is a request-level view layer and the Host
            // persists the raw conversation, so each request must re-apply the
            // narrative (GLORY-015 idempotence is by message identity, never by
            // text matching). This runs regardless of who the last user message
            // is: a later continuation (e.g. the Activation) or work-time message
            // must not suppress the opening rewrite, or the next request breaks
            // the ARCH-004 seal. The narrative derives from the DURABLE opening
            // blob, never from the message's current text, so a persisted rewrite
            // never stacks a second tail. Migration Lives (AgentOwnerRoot
            // managers) are never rewritten — the Host's assignment is not a
            // Birth (GLORY-012/068).
            | Some life when isHumanRootManager durable sid ->
                let openingId = PhysicalUserMessageId.value life.OpeningUserMessageId

                rawMessages
                |> List.tryFindIndex (fun raw -> Projection.hostMessageId raw = Some openingId)
                |> Option.bind (fun messageIndex ->
                    let rawText =
                        match durable.Writer.BlobWriter.Read life.OpeningTextRef with
                        | Ok text -> text
                        | Error _ ->
                            // Blob unavailable: fall back to the current message
                            // text (best effort; a persisted rewrite would stack,
                            // so prefer the blob).
                            match Projection.decodeMessage (List.item messageIndex rawMessages) with
                            | Some message ->
                                message.Parts
                                |> List.choose (function
                                    | WireText text -> Some text
                                    | _ -> None)
                                |> String.concat "\n"
                            | None -> ""

                    if String.IsNullOrWhiteSpace rawText then
                        None
                    else
                        let narrative =
                            if List.isEmpty lifecycle.CompletedLives then
                                ManagerNarrative.firstBirth rawText
                            else
                                ManagerNarrative.reawakening rawText

                        Some(rewriteMessage rawMessages messageIndex narrative))
            | Some _ -> None
            // No open Life: a new HumanRoot opens one (GLORY-012/063).
            | None ->
                if not (isHumanRootManager durable sid) then
                    None
                else
                    match lastUserMessage rawMessages with
                    | None -> None
                    | Some(messageIndex, messageId, raw) ->
                        // The Host's title request carries its own marker user
                        // message in the preamble (measured: "Generate a title for
                        // this conversation:"); it is Host-synthesized, never a
                        // HumanRoot, and must not open a Life (GLORY-012). The
                        // marker lives on the message's top-level `content` field
                        // (Host 1.18 assembly), not in `parts`.
                        let isTitleRequest =
                            let fromContent =
                                let value = readField raw "content"

                                if isNull value then
                                    false
                                else
                                    unbox<string> value
                                    |> fun text ->
                                        text.StartsWith(
                                            "Generate a title for this conversation:",
                                            StringComparison.Ordinal
                                        )

                            fromContent
                            || (match Projection.decodeMessage raw with
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

                        // GLORY-012: not a title request, not a continuation, not
                        // a compaction replay.
                        if isTitleRequest || isCompactionMarker raw then
                            None
                        elif
                            isAcceptedContinuation durable sid messageId
                            || isMessageFromCompletedLife traceState messageId
                            || hasSuicideAfter rawMessages messageIndex
                        then
                            // GLORY-0xx: after a Life completed, a continuation
                            // (e.g. the join guard) or a post-completion step of the completed Life
                            // must still see the same opening rewrite as the previous request, or the
                            // ARCH-004 seal breaks (measured: msg[1] reverted to
                            // the raw assignment on the second join-guard turn
                            // once CurrentLife became None).
                            match List.tryHead lifecycle.CompletedLives with
                            | None -> None
                            | Some completedLife ->
                                let openingId = PhysicalUserMessageId.value completedLife.OpeningUserMessageId

                                rawMessages
                                |> List.tryFindIndex (fun raw -> Projection.hostMessageId raw = Some openingId)
                                |> Option.bind (fun messageIndex ->
                                    let rawText =
                                        match durable.Writer.BlobWriter.Read completedLife.OpeningTextRef with
                                        | Ok text -> text
                                        | Error _ ->
                                            match Projection.decodeMessage (List.item messageIndex rawMessages) with
                                            | Some message ->
                                                message.Parts
                                                |> List.choose (function
                                                    | WireText text -> Some text
                                                    | _ -> None)
                                                |> String.concat "\n"
                                            | None -> ""

                                    if String.IsNullOrWhiteSpace rawText then
                                        None
                                    else
                                        // Keep the rewrite byte-identical to the
                                        // completed Life's own opening rewrite:
                                        // the seal compares the previous wire
                                        // verbatim, and the first rewrite was the
                                        // Birth narrative (measured: the join
                                        // guard's second delivery broke the seal
                                        // when this branch switched to the
                                        // reawakening narrative).
                                        let narrative =
                                            if List.length lifecycle.CompletedLives = 1 then
                                                ManagerNarrative.firstBirth rawText
                                            else
                                                ManagerNarrative.reawakening rawText

                                        Some(rewriteMessage rawMessages messageIndex narrative))
                        else
                            let messageIdValue = PhysicalUserMessageId.create messageId

                            // GLORY-069: an already-active Manager (upgrade path)
                            // has an XTrace Opening but no Life. Build one
                            // migration Life, treat it as already WorkActivated,
                            // and never re-manufacture a Birth for it.
                            //
                            // The Opening of THIS round's first HumanRoot is also
                            // present (captured at chat.message), so history — a
                            // part from an earlier turn — is what distinguishes a
                            // migrated session from a brand-new one. A session
                            // whose whole XTrace is this round's opening is a new
                            // Life and takes the normal Birth path (GLORY-071).
                            let migrateExistingLife () =
                                match traceState with
                                | Some state ->
                                    let hasHistory = state.Parts |> List.exists (fun p -> p.Turn <> 0)

                                    match state.Opening, hasHistory with
                                    | Some opening, true when List.isEmpty lifecycle.CompletedLives ->
                                        let lifeId = ManagerLifeId.create (Guid.NewGuid().ToString("N"))

                                        match durable.WriteBlob opening.AssignmentText with
                                        | Error error ->
                                            raise (
                                                InvalidOperationException(
                                                    sprintf "Life migration blob write failed: %s" error
                                                )
                                            )
                                        | Ok blob ->
                                            AgentJournal.appendManagerLifecycle
                                                (StreamId.Session sid)
                                                (ManagerLifecycleFact.LifeOpened
                                                    {| SessionId = sid
                                                       LifeId = lifeId
                                                       OpeningUserMessageId = messageIdValue
                                                       OpeningTextRef = blob.BlobRef
                                                       OpeningTextDigest = blob.BlobDigest
                                                       OpeningCursorSequence = 0L |})
                                                durable
                                            |> Result.mapError (fun failure ->
                                                raise (
                                                    InvalidOperationException(
                                                        sprintf
                                                            "Life migration append failed: %s"
                                                            (JournalAppendFailure.describe failure)
                                                    )
                                                ))
                                            |> ignore

                                            // GLORY-069: migration Life is already
                                            // activated; the protected prefix covers
                                            // the pre-migration history.
                                            AgentJournal.appendManagerLifecycle
                                                (StreamId.Session sid)
                                                (ManagerLifecycleFact.WorkActivated
                                                    {| SessionId = sid
                                                       LifeId = lifeId
                                                       ActivationPromptKey = PromptKey.create ""
                                                       ProtectedPrefixEndSequence =
                                                        XTraceProjection.headSequence state + 1L |})
                                                durable
                                            |> Result.mapError (fun failure ->
                                                raise (
                                                    InvalidOperationException(
                                                        sprintf
                                                            "Life migration activation failed: %s"
                                                            (JournalAppendFailure.describe failure)
                                                    )
                                                ))
                                            |> ignore

                                        true
                                    | _ -> false
                                | None -> false

                            if migrateExistingLife () then
                                // GLORY-069: the migrated Manager keeps working;
                                // this HumanRoot is not re-Birthed.
                                None
                            else
                                // The raw user text: the durable Opening (GLORY-013).
                                let rawText =
                                    match Projection.decodeMessage raw with
                                    | Some message ->
                                        message.Parts
                                        |> List.choose (function
                                            | WireText text -> Some text
                                            | _ -> None)
                                        |> String.concat "\n"
                                    | None -> ""

                                if String.IsNullOrWhiteSpace rawText then
                                    None
                                else
                                    let narrative =
                                        if List.isEmpty lifecycle.CompletedLives then
                                            ManagerNarrative.firstBirth rawText
                                        else
                                            ManagerNarrative.reawakening rawText

                                    let lifeId = ManagerLifeId.create (Guid.NewGuid().ToString("N"))

                                    let openLife () =
                                        match durable.WriteBlob rawText with
                                        | Error error ->
                                            raise (
                                                InvalidOperationException(
                                                    sprintf "Life opening blob write failed: %s" error
                                                )
                                            )
                                        | Ok blob ->
                                            let cursor =
                                                openingCursorOf traceState messageIndex
                                                |> Option.defaultValue (
                                                    traceState
                                                    |> Option.map XTraceProjection.headSequence
                                                    |> Option.defaultValue 0L
                                                )

                                            AgentJournal.appendManagerLifecycle
                                                (StreamId.Session sid)
                                                (ManagerLifecycleFact.LifeOpened
                                                    {| SessionId = sid
                                                       LifeId = lifeId
                                                       OpeningUserMessageId = messageIdValue
                                                       OpeningTextRef = blob.BlobRef
                                                       OpeningTextDigest = blob.BlobDigest
                                                       OpeningCursorSequence = cursor |})
                                                durable
                                            |> Result.mapError (fun failure ->
                                                raise (
                                                    InvalidOperationException(
                                                        sprintf
                                                            "Life opening append failed: %s"
                                                            (JournalAppendFailure.describe failure)
                                                    )
                                                ))
                                            |> ignore

                                    openLife ()

                                    Some(rewriteMessage rawMessages messageIndex narrative)

    /// GLORY-021: after the Activation continuation's physical acceptance, write
    /// `WorkActivated` with the protected prefix end.
    ///
    /// Runs at the transform boundary: the Activation message has already entered
    /// the XTrace (captureProjection ran first), so the protected prefix end is
    /// the XTrace head — the cursor just after the Activation prompt. Idempotent:
    /// a Life with `ProtectedPrefixEnd` set is left alone. The activation prompt
    /// key is read from the message's PROMPT-011 metadata when present; a message
    /// without it is skipped (the next transform re-checks).

    let applyAcceptedActivation
        (journal: AgentJournal option)
        (sessionId: string option)
        (traceState: XTraceProjectionState option)
        (rawMessages: obj list)
        : unit =
        match journal, sessionId, traceState with
        | None, _, _
        | _, None, _
        | _, _, None -> ()
        | Some durable, Some sessionIdValue, Some state ->
            let sid = SessionId.create sessionIdValue

            // GLORY-021: the floor fix is keyed on the Life alone — an accepted
            // Activation for a Life that was opened by a HumanRoot. The
            // `isHumanRootManager` run check is deliberately not repeated here:
            // the ActiveLogicalRun on a continuation request describes the
            // continuation, and a Life can only have been opened by a HumanRoot
            // (GLORY-012), so the Life itself is the authority.
            let snapshot = AgentJournal.snapshot durable

            let lifecycle =
                AgentProjection.tryFind sid snapshot.AgentProjections
                |> Option.bind (fun session -> session.ManagerLife)
                |> Option.defaultValue ManagerLifecycleProjection.empty

            match lifecycle.CurrentLife with
            | Some life when life.ProtectedPrefixEnd.IsNone && not life.Completed ->
                // GLORY-021: find the Activation message by its canonical text
                // rather than the AcceptedContinuationIds ledger. The ledger is
                // updated by the accept path which races the first transform of
                // the Activation request (measured: the first Activation
                // transform sees no ledger entry on some runs, and the floor is
                // only fixed by a later transform). The Activation message is the
                // user message whose text begins with the canonical Activation
                // prompt; a Life can only have been opened by a HumanRoot
                // (GLORY-012), so within a Life the match is unambiguous.
                let activationMessage =
                    rawMessages
                    |> List.tryFind (fun raw ->
                        match Projection.decodeMessage raw with
                        | Some message when message.Role = "user" ->
                            message.Parts
                            |> List.exists (function
                                | WireText text ->
                                    text.StartsWith("Now complete it yourself.", StringComparison.Ordinal)
                                | _ -> false)
                        | _ -> false)

                match activationMessage with
                | None -> ()
                | Some raw ->
                    match promptKeyOfMessage raw with
                    | Some promptKey ->
                        let protectedPrefixEnd = XTraceProjection.headSequence state + 1L

                        AgentJournal.appendManagerLifecycle
                            (StreamId.Session sid)
                            (ManagerLifecycleFact.WorkActivated
                                {| SessionId = sid
                                   LifeId = life.LifeId
                                   ActivationPromptKey = promptKey
                                   ProtectedPrefixEndSequence = protectedPrefixEnd |})
                            durable
                        |> Result.mapError (fun failure ->
                            raise (
                                InvalidOperationException(
                                    sprintf "WorkActivated append failed: %s" (JournalAppendFailure.describe failure)
                                )
                            ))
                        |> ignore
                    | None ->
                        // GLORY-021: the compression floor must be fixed once
                        // the Activation landed, even when the Host did not
                        // preserve the PROMPT-011 metadata on the persisted
                        // message (the key is audit-only; the floor is the
                        // contract). A later transform re-checks and is
                        // idempotent either way.
                        let protectedPrefixEnd = XTraceProjection.headSequence state + 1L

                        AgentJournal.appendManagerLifecycle
                            (StreamId.Session sid)
                            (ManagerLifecycleFact.WorkActivated
                                {| SessionId = sid
                                   LifeId = life.LifeId
                                   ActivationPromptKey = PromptKey.create ""
                                   ProtectedPrefixEndSequence = protectedPrefixEnd |})
                            durable
                        |> Result.mapError (fun failure ->
                            raise (
                                InvalidOperationException(
                                    sprintf "WorkActivated append failed: %s" (JournalAppendFailure.describe failure)
                                )
                            ))
                        |> ignore
            | _ -> ()
