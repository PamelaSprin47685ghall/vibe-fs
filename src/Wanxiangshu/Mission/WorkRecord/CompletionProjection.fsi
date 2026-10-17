namespace Wanxiangshu.Mission.WorkRecord

open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity

type CompletedWorkRecordProjectionState

module CompletedWorkRecordProjection =
    val empty: CompletedWorkRecordProjectionState

    val capture:
        providerRun: ProviderRunIdentity ->
        frontier: XTraceCursor ->
        framesNewestFirst: BlogFrame list ->
        state: CompletedWorkRecordProjectionState ->
            CompletedWorkRecordProjectionState

    val framesForRange:
        providerRun: ProviderRunIdentity option ->
        endExclusive: XTraceCursor ->
        state: CompletedWorkRecordProjectionState ->
            BlogFrame list option
