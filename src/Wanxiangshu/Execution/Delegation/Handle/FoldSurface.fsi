namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Composition.Durable

/// JS-native fold replay boundary for durable handle lifecycle
/// (MANAGED-SESSION-006/008/015).
///
/// This surface calls the production `ExecutionFactFold.fold` directly — no
/// second interpreter. JS sends a list of fact envelopes; the surface
/// constructs typed `ExecutionFactCases` and folds them through the real fold.
/// The typed `AgentProjectionSet`, `ExecutionFactCases` union, and
/// `FoldRejection` never cross to JS.
///
/// Compile position: after `ExecutionFactFold.fs` and `Composition/Durable/Fold.fs`
/// so the production fold is in scope.
module HandleFoldSurface =

    /// A fold state holds an `AgentProjectionSet`. JS treats it as opaque.
    type FoldState =
        internal new: projection: AgentProjectionSet -> FoldState
        member internal Internal: AgentProjectionSet

    val foldEmpty: unit -> FoldState

    /// Fold a list of fact envelopes through the production `ExecutionFactFold`.
    /// Each envelope is `{ seq, stream, fact }` where `fact` is
    /// `{ case, payload }`. Returns `{ ok: true, state }` or
    /// `{ ok: false, error: { Fact, Reason } }` (fold rejection) or
    /// `{ ok: false, error: { kind, value } }` (invalid input).
    val foldApply: state: FoldState -> envelopes: obj array -> obj

    /// Extract the handle projection for one parent session from a fold state.
    /// Returns a `HandleProjectionState` (from `HandleSurface`) that the
    /// `read`/`views`/etc. helpers accept.
    val foldSession: state: FoldState -> parentId: string -> HandleSurface.HandleProjectionState
