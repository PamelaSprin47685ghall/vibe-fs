namespace Wanxiangshu.OpenCode

open Wanxiangshu.Interaction.Attention

[<RequireQualifiedAccess>]
module AttentionTools =
    val admission: ToolAdmission
    val specs: factory: HostToolFactory -> journal: AttentionJournalPort option -> ToolSpec list
