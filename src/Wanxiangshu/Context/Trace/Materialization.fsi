namespace Wanxiangshu.Context.Trace

open System.Threading.Tasks
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module XTraceMaterialization =
    val empty: ProviderProjection.ProviderSemanticProjection

    val tryHostToolResult:
        messageId: TranscriptMessageAddress ->
        partId: HostToolPartId option ->
        callId: ToolCallId ->
        xTrace: XTraceProjectionState ->
            Task<Result<string option, string>>

    val tryHostUserText:
        messageId: TranscriptMessageAddress ->
        partId: HostMessagePartId option ->
        partIndex: int ->
        generation: int ->
        xTrace: XTraceProjectionState ->
            Task<Result<string option, string>>

    val currentProjection:
        xTrace: XTraceProjectionState -> Task<Result<ProviderProjection.ProviderSemanticProjection, string>>

    val currentProjectionBetween:
        range: XTraceRange ->
        xTrace: XTraceProjectionState ->
            Task<Result<ProviderProjection.ProviderSemanticProjection, string>>

    val materializeRange: range: XTraceRange -> xTrace: XTraceProjectionState -> Task<Result<XTraceItem list, string>>

    val materializeWorkRecordRange:
        range: XTraceRange -> xTrace: XTraceProjectionState -> Task<Result<XTraceItem list, string>>

    val renderRange: range: XTraceRange -> xTrace: XTraceProjectionState -> Task<Result<string, string>>

    val renderWorkRecordRange: range: XTraceRange -> xTrace: XTraceProjectionState -> Task<Result<string, string>>
