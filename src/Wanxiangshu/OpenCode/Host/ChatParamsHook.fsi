namespace Wanxiangshu.OpenCode

open Wanxiangshu.Host.Ports
open Wanxiangshu.Persistence.Journal

module ChatParamsHook =
    /// The chat.params observation barrier. Managed-ness and the expected
    /// participant/model come from the durable `Accepted` execution plus the
    /// exact committed lease for this physical user message.
    val createWith: journal: AgentJournal option -> obj
    val create: unit -> obj

    /// HNI P-06: chat.params observation barrier integrated with ITurnTransform.
    val createWithTurn: journal: AgentJournal option -> turnOpt: ITurnTransform option -> obj
