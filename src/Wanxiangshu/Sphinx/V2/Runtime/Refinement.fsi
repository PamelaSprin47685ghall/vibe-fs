namespace Wanxiangshu.Sphinx.V2.Runtime

open Wanxiangshu.Sphinx.V2.Core

type DirtyTarget =
    {
        Producer: string
        Target: string
        ScopeId: string
        /// Fingerprint of every input the target last consumed.
        InputFingerprint: string
        Dependencies: Set<string>
    }

type RefinementStep =
    { Target: DirtyTarget
      Applied: bool
      DerivedPatches: PluginDelta option
      Note: string }

type RefinementOutcome =
    { Steps: RefinementStep list
      Remaining: DirtyTarget list
      Iterations: int
      Residual: float
      Converged: bool
      StopReason: string }

module Refinement =
    /// Propagate to an explicit step limit. Never claims a fixed point it cannot prove.
    val propagate: int -> DirtyTarget list -> RefinementOutcome

    /// True when the same inputs are already recorded: closure adds no revision.
    val unchanged: DirtyTarget list -> DirtyTarget list -> bool
    val fingerprint: string list -> string
