namespace Wanxiangshu.Execution.Delegation.Advisor

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider

type AdvisorViewPlan =
    { ViewPrompt: string
      DeliveredAnchors: int list
      DeliveredMaterial: string list
      HasNewMaterial: bool }

/// The caller resolves carry once. Delivery facts filter its output, never its input.
/// The delta law (filter the generated view by delivered identities, in view order) is owned by
/// `wanxiangli/advisor` (CUT-39); this host only renders the resulting material.
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
        SyntheticToml.tableArrayEntry
            "new_work_to_record"
            [ SyntheticToml.field "record" (SyntheticToml.renderString text) ]

    let plan language (delivered: Set<string>) (material: obj array) =
        let planned = law.plan (Set.toArray delivered, material)
        let delta: obj array = planned?delta
        let deliveredAnchors: int array = planned?deliveredAnchors
        let deliveredMaterial: string array = planned?deliveredMaterial

        { ViewPrompt =
            delta
            |> Array.toList
            |> List.map (fun piece -> recordBlock (unbox<string> piece?text))
            |> SyntheticToml.document (headerLines language)
          DeliveredAnchors = List.ofArray deliveredAnchors
          DeliveredMaterial = List.ofArray deliveredMaterial
          HasNewMaterial = unbox<bool> planned?hasNewMaterial }
