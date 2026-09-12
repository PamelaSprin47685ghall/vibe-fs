namespace Wanxiangshu.OpenCode

open Wanxiangshu.Enforcer.InstitutionalLearning

[<RequireQualifiedAccess>]
module InstitutionalLearningTools =
    val admission: ToolAdmission
    val specs: factory: HostToolFactory -> journal: InstitutionalLearningJournalPort option -> ToolSpec list
