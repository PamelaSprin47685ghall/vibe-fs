namespace Wanxiangshu.Repository.Programming.Js.Anchor

open Fable.Core

/// Shared view projection. Each operation replaces its captured, latest interval.
[<RequireQualifiedAccess>]
module AnchorView =

    type AnchorMessage =
        { Id: string option
          Text: string
          Raw: obj }

    /// One selected message: kept as it stands, or standing in for a replaced
    /// anchor as the summary the model declared. Building the replacement
    /// message is the caller's job — this module never touches host shapes.
    /// Rows folded into an open replacement simply do not appear.
    type AnchorViewEntry =
        | Kept of AnchorMessage
        | Summarized of message: AnchorMessage * summary: string
        | OpeningSummary of summary: string

    type AnchorViewReport =
        { Total: int
          Used: int
          Cached: int
          Kept: int
          Summarized: int
          Folded: int
          Replaced: int list }

    let private textOf =
        function
        | Kept message -> message.Text
        | Summarized(_, summary)
        | OpeningSummary summary -> summary

    let apply
        (numberOf: string -> int)
        (countTokens: string -> int)
        (history: AnchorReplacement list)
        (declaration: AnchorViewDeclaration)
        (messages: AnchorMessage list)
        : AnchorViewEntry list * AnchorViewReport =
        let positions, _ =
            messages
            |> List.mapFold
                (fun position message ->
                    let anchor = message.Id |> Option.map numberOf |> Option.defaultValue position
                    anchor, anchor)
                0

        let inputs = List.toArray messages
        let projection = AnchorCoverage.project positions history declaration.PrefixUntil

        let rows =
            projection.Rows
            |> List.map (function
                | AnchorProjectedRow.Kept(index, cached) -> Kept inputs.[index], cached
                | AnchorProjectedRow.Summarized(index, summary, cached) -> Summarized(inputs.[index], summary), cached
                | AnchorProjectedRow.Opening(summary, cached) -> OpeningSummary summary, cached)

        let entries = rows |> List.map fst

        entries,
        { Total = messages |> List.sumBy (fun message -> countTokens message.Text)
          Used = entries |> List.sumBy (textOf >> countTokens)
          Cached =
            rows
            |> List.sumBy (fun (entry, cached) -> if cached then countTokens (textOf entry) else 0)
          Kept = projection.Kept
          Summarized = projection.Summarized
          Folded = projection.Folded
          Replaced = projection.Replaced }

    let generate numberOf countTokens history declaration messages =
        let anchors =
            messages |> List.choose (fun message -> message.Id |> Option.map numberOf)

        let changes = AnchorViewDeclaration.plan anchors history declaration
        apply numberOf countTokens (history @ changes) declaration messages

    let material numberOf entries =
        entries
        |> List.map (function
            | Kept message -> message.Id |> Option.map numberOf |> Option.defaultValue 0, message.Text
            | Summarized(message, summary) -> message.Id |> Option.map numberOf |> Option.defaultValue 0, summary
            | OpeningSummary summary -> 0, summary)

    [<AllowNullLiteral>]
    type private MaterialIdentityLaw =
        abstract materialKey: anchor: int * text: string -> string

    [<Import("AdvisorLaw", "wanxiangli/advisor")>]
    let private advisorLawClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : MaterialIdentityLaw = jsNative

    let private identityLaw = construct advisorLawClass

    /// A material identity includes its representative and exact rendered bytes. The identity
    /// law is owned by `wanxiangli/advisor` (CUT-39); handoff selection and advisor deltas share it.
    let materialKey (anchor: int) (text: string) = identityLaw.materialKey (anchor, text)
