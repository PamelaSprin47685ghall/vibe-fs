namespace Wanxiangshu.OpenCode

open Wanxiangshu.Persistence.Journal

module ChatParamsHook =
    /// The chat.params observation barrier. Managed-ness and the expected
    /// participant/model come from the durable `Accepted` execution plus the
    /// exact committed lease for this physical user message.
    val createWith: journal: AgentJournal option -> obj
    val create: unit -> obj
