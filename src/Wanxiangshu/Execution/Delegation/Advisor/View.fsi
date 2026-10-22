namespace Wanxiangshu.Execution.Delegation.Advisor

type AdvisorViewPlan =
    { ViewPrompt: string
      DeliveredAnchors: int list
      DeliveredMaterial: string list
      HasNewMaterial: bool }

[<RequireQualifiedAccess>]
module AdvisorView =
    val plan: language: string -> delivered: Set<string> -> material: obj array -> AdvisorViewPlan
