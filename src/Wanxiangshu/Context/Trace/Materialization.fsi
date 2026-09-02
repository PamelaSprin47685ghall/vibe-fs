namespace Wanxiangshu.Context.Trace

open System.Threading.Tasks
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module XTraceMaterialization =
    val empty: ProviderProjection.ProviderSemanticProjection

    val currentProjection:
        journal: AgentJournal ->
        xTrace: XTraceProjectionState ->
            Task<Result<ProviderProjection.ProviderSemanticProjection, string>>

    val currentProjectionBetween:
        journal: AgentJournal ->
        range: XTraceRange ->
        xTrace: XTraceProjectionState ->
            Task<Result<ProviderProjection.ProviderSemanticProjection, string>>

    val materializeRange:
        journal: AgentJournal ->
        range: XTraceRange ->
        xTrace: XTraceProjectionState ->
            Task<Result<XTraceItem list, string>>

    val materializeWorkRecordRange:
        journal: AgentJournal ->
        range: XTraceRange ->
        xTrace: XTraceProjectionState ->
            Task<Result<XTraceItem list, string>>

    val renderRange:
        journal: AgentJournal -> range: XTraceRange -> xTrace: XTraceProjectionState -> Task<Result<string, string>>

    val renderWorkRecordRange:
        journal: AgentJournal -> range: XTraceRange -> xTrace: XTraceProjectionState -> Task<Result<string, string>>
