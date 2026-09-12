namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module RequirementGroundingJournalAdapter =
    val forRequirementGrounding: journal: AgentJournal -> RequirementGroundingPort
