namespace Wanxiangshu.Execution.Delegation.Advisor

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider
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
    [<AllowNullLiteral>]
    type private AdvisorDeltaLaw =
        abstract plan: delivered: string array * material: obj array -> obj

    [<Import("AdvisorLaw", "wanxiangli/advisor")>]
    let private advisorLawClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : AdvisorDeltaLaw = jsNative

    let private law = construct advisorLawClass

    let private headerLines language =
        ProviderProse.instructionLines (ProviderLanguage.parse language) "lifecycle/advisor/view" Map.empty

    let private recordBlock text =
        LlmFacing.Data.tableArray "new_work_to_record" [ LlmFacing.Data.stringMember "record" text ]

    let plan language (delivered: Set<string>) (request: AdvisorViewRequest) =
        let messages =
            request.Messages
            |> List.map (fun message ->
                { AnchorView.AnchorMessage.Id =
                    if message.Anchor = 0 then
                        None
                    else
                        Some(string message.Anchor)
                  AnchorView.AnchorMessage.Text = message.Text
                  AnchorView.AnchorMessage.Raw = null })

        let numberOf (value: string) = int value

        let entries, _ =
            AnchorView.generate numberOf (fun _ -> 0) request.History request.Declaration messages

        let material =
            AnchorView.material numberOf entries
            |> List.map (fun (anchor, text) -> createObj [ "anchor" ==> anchor; "text" ==> text ])
            |> List.toArray

        let planned = law.plan (Set.toArray delivered, material)
        let delta: obj array = planned?delta
        let deliveredAnchors: int array = planned?deliveredAnchors
        let deliveredMaterial: string array = planned?deliveredMaterial

        { ViewPrompt =
            LlmFacing.instructions (headerLines language)
            |> LlmFacing.withData (
                delta
                |> Array.toList
                |> List.map (fun piece -> recordBlock (unbox<string> piece?text))
            )
            |> LlmFacing.render
          DeliveredAnchors = List.ofArray deliveredAnchors
          DeliveredMaterial = List.ofArray deliveredMaterial
          HasNewMaterial = unbox<bool> planned?hasNewMaterial }
