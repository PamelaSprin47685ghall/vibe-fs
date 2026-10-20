namespace Wanxiangshu.Execution.Delegation.Advisor

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

/// The shared view is generated first. Delivery facts filter its output, never its input.
[<RequireQualifiedAccess>]
module AdvisorView =
    let private headerLines language =
        ProviderProse.instructionLines (ProviderLanguage.parse language) "lifecycle/advisor/view" Map.empty

    let private recordBlock text =
        SyntheticToml.tableArrayEntry
            "new_work_to_record"
            [ SyntheticToml.field "record" (SyntheticToml.renderString text) ]

    let plan language (delivered: Set<string>) (request: AdvisorViewRequest) =
        let messages =
            request.Messages
            |> List.sortBy _.Anchor
            |> List.map (fun message ->
                { AnchorView.AnchorMessage.Id = Some(string message.Anchor)
                  AnchorView.AnchorMessage.Text = message.Text
                  AnchorView.AnchorMessage.Raw = null })

        let numberOf (value: string) = int value

        let entries, _ =
            AnchorView.generate numberOf (fun _ -> 0) request.History request.Declaration messages

        let view = AnchorView.material numberOf entries

        let delta =
            view
            |> List.filter (fun (anchor, text) -> not (Set.contains (AnchorView.materialKey anchor text) delivered))

        { ViewPrompt =
            delta
            |> List.map (snd >> recordBlock)
            |> SyntheticToml.document (headerLines language)
          DeliveredAnchors = view |> List.map fst |> List.distinct
          DeliveredMaterial = delta |> List.map (fun (anchor, text) -> AnchorView.materialKey anchor text)
          HasNewMaterial = not (List.isEmpty delta) }
