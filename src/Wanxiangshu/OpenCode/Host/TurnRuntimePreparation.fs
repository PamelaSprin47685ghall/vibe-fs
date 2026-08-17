namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Turn

/// Physical runtime cleanup before Application turn observation (rabbit §19).
/// Prompt authority belongs to Application/Prompting/ChildPromptAuthority.
module TurnRuntimePreparation =

    /// Dispose only the physical Executor runtime for the observed session.
    /// The durable cancel/drain is part of turn preparation and must settle
    /// before later turn effects can run against the same Journal lifetime.
    let prepare (disposeExecutorRuntime: string -> Task) (turn: ReconciledTurn) : Task =
        disposeExecutorRuntime (SessionId.value turn.SessionId)
