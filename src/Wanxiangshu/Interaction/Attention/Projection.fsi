namespace Wanxiangshu.Interaction.Attention

open Wanxiangshu.Foundation.Identity

type DeferredWorkItem =
    { OccurrenceId: string
      Text: string
      ResurfacedBy: string option }

type AttentionProjectionState =
    { BySession: Map<SessionId, DeferredWorkItem list> }

[<RequireQualifiedAccess>]
module AttentionProjection =
    val empty: AttentionProjectionState
    val pending: sessionId: SessionId -> state: AttentionProjectionState -> DeferredWorkItem list

    val tryFind:
        sessionId: SessionId ->
        occurrenceId: string ->
        state: AttentionProjectionState ->
        DeferredWorkItem option

    val record:
        sessionId: SessionId ->
        occurrenceId: string ->
        text: string ->
        state: AttentionProjectionState ->
        AttentionProjectionState

    val resurface:
        sessionId: SessionId ->
        learningOccurrence: string ->
        workIds: string list ->
        state: AttentionProjectionState ->
        AttentionProjectionState
