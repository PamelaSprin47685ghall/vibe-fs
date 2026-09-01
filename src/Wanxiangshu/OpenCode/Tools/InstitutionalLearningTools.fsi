namespace Wanxiangshu.OpenCode

open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module InstitutionalLearningTools =
    val admission: ToolAdmission
    val specs: factory: HostToolFactory -> journal: AgentJournal option -> ToolSpec list
