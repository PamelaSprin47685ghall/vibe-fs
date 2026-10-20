namespace Wanxiangshu.Execution.Delegation.Advisor

open Wanxiangshu.Repository.Programming.Js.Anchor

type AdvisorViewMessage = { Anchor: int; Text: string }
type AdvisorViewDeclaration = AnchorViewDeclaration

type AdvisorViewRequest =
    { Messages: AdvisorViewMessage list
      Declaration: AnchorViewDeclaration
      History: AnchorReplacement list }

type AdvisorViewPlan =
    { ViewPrompt: string
      DeliveredAnchors: int list
      DeliveredMaterial: string list
      HasNewMaterial: bool }

[<RequireQualifiedAccess>]
module AdvisorView =
    val plan: language: string -> delivered: Set<string> -> request: AdvisorViewRequest -> AdvisorViewPlan
