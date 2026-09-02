namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode

/// Durable localization for one V1 Host tool callback.
///
/// The callback exposes only `(sessionID, callID)`. This resolver proves its
/// assistant provider run and physical ToolPart from the persisted snapshot,
/// then joins that exact triple to the captured XTrace part. Any missing or
/// ambiguous link is fail-closed; neither message-array position nor a guessed
/// provider run is admissible evidence.
module MagicTodoLocality =

    type LocalizedToolCall =
        { ProviderRun: ProviderRunIdentity
          HostToolPartId: HostToolPartId
          ToolCallId: ToolCallId
          ToolName: string
          InputCanonical: string
          State: SnapshotToolPartState
          TodowriteCallIdsInMessage: ToolCallId list
          ToolPartOrdinal: int
          ReviewFrontier: XTraceCursor
          Range: XTraceRange }

    [<RequireQualifiedAccess>]
    type LocalityRejection =
        | Snapshot of SessionSnapshotPort.ToolCallLocationError
        | XTraceUnavailable
        | XTraceMissing of providerRun: ProviderRunIdentity * toolCallId: ToolCallId * hostToolPartId: HostToolPartId
        | XTraceAmbiguous of providerRun: ProviderRunIdentity * toolCallId: ToolCallId * hostToolPartId: HostToolPartId

    [<RequireQualifiedAccess>]
    type InputMaterializationRejection =
        | SnapshotUnavailable of reason: string
        | Snapshot of SessionSnapshotPort.ToolCallLocationError
        | CarrierChanged
        | InputMismatch

    val resolve:
        sessionId: SessionId ->
        messages: SessionMessage list ->
        projection: ProjectionSet ->
        toolCallId: ToolCallId ->
            Result<LocalizedToolCall, LocalityRejection>

    val materializeInput:
        localized: LocalizedToolCall ->
        expectedInputCanonical: string ->
            Result<LocalizedToolCall, InputMaterializationRejection>
