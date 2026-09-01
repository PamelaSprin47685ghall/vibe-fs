namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Foundation.Identity

type ProjectionSet =
    { AgentProjections: AgentProjectionSet
      RuntimeId: RuntimeId option }
