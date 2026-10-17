namespace Wanxiangshu.Mission.WorkRecord

open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity

type CompletedWorkRecordProjectionState

module CompletedWorkRecordProjection =
    val empty: CompletedWorkRecordProjectionState

    val capture:
        causality: (EventId * (EventId -> bool)) option ->
        providerRun: ProviderRunIdentity ->
        frontier: XTraceCursor ->
        state: CompletedWorkRecordProjectionState ->
            CompletedWorkRecordProjectionState

    val recordFrame:
        causality: (EventId * (EventId -> bool)) option ->
        replacedCount: int ->
        frame: BlogFrame ->
        currentFramesNewestFirst: BlogFrame list ->
        state: CompletedWorkRecordProjectionState ->
            CompletedWorkRecordProjectionState

    val markUnproven:
        providerRun: ProviderRunIdentity ->
        observedHead: XTraceCursor ->
        state: CompletedWorkRecordProjectionState ->
            CompletedWorkRecordProjectionState

    val framesForRange:
        providerRun: ProviderRunIdentity option ->
        endExclusive: XTraceCursor ->
        state: CompletedWorkRecordProjectionState ->
            Result<BlogFrame list option, unit>
