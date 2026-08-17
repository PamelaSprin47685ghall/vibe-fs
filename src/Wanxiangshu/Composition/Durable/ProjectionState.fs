namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Rebuilt journal state: durable domain projections.
type ProjectionSet =
    { AgentProjections: AgentProjectionSet
      RuntimeId: RuntimeId option }
