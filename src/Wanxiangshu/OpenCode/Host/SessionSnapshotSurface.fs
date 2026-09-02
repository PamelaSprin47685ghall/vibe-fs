namespace Wanxiangshu.OpenCode

open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity

/// JS-native boundary for HOST-BOUNDARY-009/012/020 snapshot laws.
///
/// `SessionSnapshotPort.projectMessages` / `locateToolCall` return Fable
/// records and unions (`SessionMessage`, `ToolCallLocation`,
/// `ToolCallLocationError`, `SnapshotToolPartState`). This surface delegates to
/// those production functions and translates the results into JSON-shaped
/// objects so semantic tests observe the real laws without touching Fable
/// representation (`.tag` / `.fields` / DU ordinals).
///
/// The projected message list is an opaque capability: tests pass it back to
/// `locateToolCall` / `toolPartStateAt` through this same surface and never
/// inspect its internal shape. `ProviderRunBindingSurface.bindableRun` accepts
/// the same handle (same-assembly access to the internal `Messages` field).
module SessionSnapshotSurface =

    /// Opaque handle wrapping the projected `SessionMessage list`.
    /// Tests never inspect this; they pass it back to surface functions.
    type ProjectedMessages internal (messages: SessionMessage list) =
        member internal _.Messages = messages

    /// Translate `SnapshotToolPartState` DU to a plain state string.
    /// Tests receive `"pending" | "completed" | "failed"` — never `.tag` /
    /// `.fields` / DU ordinals.
    let private toolPartStateToJs (state: SnapshotToolPartState) : string =
        match state with
        | SnapshotToolPartState.Pending -> "pending"
        | SnapshotToolPartState.Completed _ -> "completed"
        | SnapshotToolPartState.Failed _ -> "failed"

    /// Extract the canonical payload string from a `SnapshotToolPartState`,
    /// if any. `pending` has no canonical value.
    let private toolPartStateCanonical (state: SnapshotToolPartState) : obj =
        match state with
        | SnapshotToolPartState.Pending -> null
        | SnapshotToolPartState.Completed output -> box output
        | SnapshotToolPartState.Failed error -> box error

    /// Project raw Host message objects into an opaque `ProjectedMessages`
    /// handle. Delegates to `SessionSnapshotPort.projectMessages` — no
    /// algorithm duplication.
    let projectMessages (rawMessages: obj) : ProjectedMessages =
        let messages =
            (rawMessages :?> obj array)
            |> Array.toList
            |> List.choose SessionSnapshotPort.projectMessage

        ProjectedMessages(messages)

    /// HOST-BOUNDARY-012: resolve one tool callback through the Host's
    /// persisted assistant message + ToolPart. Delegates to
    /// `SessionSnapshot.locateToolCall` and translates the
    /// `Result<ToolCallLocation, ToolCallLocationError>` into a plain object.
    ///
    /// Returns `{ ok: true, providerRun, hostToolPartId, toolCallId, toolName,
    /// inputCanonical, state }` when exactly one ToolPart matches; `{ ok: false,
    /// error, toolCallId? }` when 0 (`Missing`) or ≥2 (`Ambiguous`) candidates
    /// are observed (fail closed).
    let locateToolCall (callId: string) (handle: ProjectedMessages) : obj =
        let typed = ToolCallId.create callId

        match SessionSnapshot.locateToolCall typed handle.Messages with
        | Ok location ->
            box
                {| ok = true
                   providerRun = ProviderRunIdentity.value location.ProviderRun
                   hostToolPartId = HostToolPartId.value location.HostToolPartId
                   toolCallId = ToolCallId.value location.ToolCallId
                   toolName = location.ToolName
                   inputCanonical = location.InputCanonical
                   state = toolPartStateToJs location.State |}
        | Error error ->
            match error with
            | SessionSnapshot.ToolCallLocationError.Missing id ->
                box
                    {| ok = false
                       error = "Missing"
                       toolCallId = ToolCallId.value id |}
            | SessionSnapshot.ToolCallLocationError.Ambiguous id ->
                box
                    {| ok = false
                       error = "Ambiguous"
                       toolCallId = ToolCallId.value id |}

    /// HOST-BOUNDARY-006: classify one persisted ToolPart's state as a plain
    /// string. Delegates to the already-projected `SessionMessage.ToolParts`
    /// array — no re-parsing.
    ///
    /// Returns `{ ok: true, state, canonical? }` where `state` is
    /// `"pending" | "completed" | "failed"`; `{ ok: false, error }` when the
    /// index is out of range.
    let toolPartStateAt (handle: ProjectedMessages) (messageIndex: int) (partIndex: int) : obj =
        let messages = handle.Messages

        if messageIndex < 0 || messageIndex >= List.length messages then
            box
                {| ok = false
                   error = "MessageIndexOutOfRange" |}
        else
            let message = List.item messageIndex messages
            let toolParts = message.ToolParts

            if partIndex < 0 || partIndex >= Array.length toolParts then
                box
                    {| ok = false
                       error = "PartIndexOutOfRange" |}
            else
                let part = toolParts[partIndex]

                box
                    {| ok = true
                       state = toolPartStateToJs part.State
                       canonical = toolPartStateCanonical part.State |}
