namespace Wanxiangshu.OpenCode

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Kernel.Identity

/// Physical runtime cleanup before Application turn observation (rabbit §19).
/// Prompt authority belongs to Application/Prompting/ChildPromptAuthority.
module TurnRuntimePreparation =

    /// Dispose only the physical Executor runtime for the observed session.
    let prepare (disposeExecutorRuntime: string -> unit) (turn: ReconciledTurn) =
        disposeExecutorRuntime (SessionId.value turn.SessionId)
