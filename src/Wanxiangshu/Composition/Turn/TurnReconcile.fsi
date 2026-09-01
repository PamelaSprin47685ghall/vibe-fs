namespace Wanxiangshu.Composition.Turn

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode

module TurnReconcile =
    val reconcile: messages: SessionMessage list -> binding: ActiveRunBinding -> ReconciledTurn option
