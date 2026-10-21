namespace Wanxiangshu.Repository.Programming.Js.Anchor

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Repository.Programming.Js

/// One declaration protects the prefix and states the remaining anchors' actions.
type AnchorViewDeclaration =
    { PrefixUntil: int
      ThenMap: (int * string option) list }

/// Bounds are captured before the map runs; later anchors cannot enter an old replacement.
type AnchorReplacement =
    { Anchor: int
      Until: int
      Summary: string }

/// One projected row; `Index` points back at the representative input message.
[<RequireQualifiedAccess>]
type AnchorProjectedRow =
    | Kept of index: int * cached: bool
    | Summarized of index: int * summary: string * cached: bool
    | Opening of summary: string * cached: bool

type AnchorProjection =
    { Rows: AnchorProjectedRow list
      Kept: int
      Summarized: int
      Folded: int
      Replaced: int list }

/// The view coverage law (shape, live partitions, permanent interval replacements and their
/// causal fold) is owned by wanxiangli/compression; this host keeps numbering, storage,
/// token metering and rendering.
[<RequireQualifiedAccess>]
module AnchorCoverage =
    [<AllowNullLiteral>]
    type private CompressionLaw =
        abstract parseView: value: obj -> obj
        abstract live: anchors: obj * history: obj -> obj
        abstract plan: anchors: obj * history: obj * view: obj -> obj
        abstract project: positions: obj * history: obj * prefixUntil: obj -> obj

    [<Import("Compression", "wanxiangli/compression")>]
    let private createCompression: unit -> CompressionLaw = jsNative

    let private law = createCompression ()

    let private intsToJs (values: int list) : obj = values |> List.toArray |> box

    let private historyToJs (history: AnchorReplacement list) : obj =
        history
        |> List.map (fun change ->
            createObj
                [ "anchor" ==> change.Anchor
                  "until" ==> change.Until
                  "summary" ==> change.Summary ])
        |> List.toArray
        |> box

    let private viewToJs (declaration: AnchorViewDeclaration) : obj =
        createObj
            [ "prefixUntil" ==> declaration.PrefixUntil
              "thenMap"
              ==> (declaration.ThenMap
                   |> List.map (fun (anchor, summary) ->
                       box [| box anchor; (summary |> Option.map box |> Option.defaultValue null) |])
                   |> List.toArray) ]

    let private replacementOf (value: obj) : AnchorReplacement =
        { Anchor = value?anchor
          Until = value?until
          Summary = value?summary }

    let parse (value: obj) : Result<AnchorViewDeclaration, string> =
        let parsed = law.parseView value

        if parsed?ok then
            let view = parsed?view

            Ok
                { PrefixUntil = view?prefixUntil
                  ThenMap =
                    (view?thenMap: obj array)
                    |> Array.toList
                    |> List.map (fun pair ->
                        let items = unbox<obj array> pair
                        let summary = items.[1]
                        unbox<int> items.[0], (if isNull summary then None else Some(unbox<string> summary))) }
        else
            Error(parsed?reason: string)

    let live (anchors: int list) (history: AnchorReplacement list) : int list =
        unbox<int array> (law.live (intsToJs anchors, historyToJs history)) |> Array.toList

    let plan (anchors: int list) (history: AnchorReplacement list) (declaration: AnchorViewDeclaration) =
        unbox<obj array> (law.plan (intsToJs anchors, historyToJs history, viewToJs declaration))
        |> Array.toList
        |> List.map replacementOf

    let project (positions: int list) (history: AnchorReplacement list) (prefixUntil: int) : AnchorProjection =
        let projected =
            law.project (intsToJs positions, historyToJs history, box prefixUntil)

        { Rows =
            (projected?entries: obj array)
            |> Array.toList
            |> List.map (fun entry ->
                let cached: bool = entry?cached

                match (entry?kind: string) with
                | "kept" -> AnchorProjectedRow.Kept(entry?index, cached)
                | "summarized" -> AnchorProjectedRow.Summarized(entry?index, entry?summary, cached)
                | _ -> AnchorProjectedRow.Opening(entry?summary, cached))
          Kept = projected?kept
          Summarized = projected?summarized
          Folded = projected?folded
          Replaced = (projected?replaced: int array) |> Array.toList }

[<RequireQualifiedAccess>]
module AnchorViewDeclaration =
    let empty = { PrefixUntil = 0; ThenMap = [] }

    let live (anchors: int list) (history: AnchorReplacement list) = AnchorCoverage.live anchors history

    let plan (anchors: int list) (history: AnchorReplacement list) (declaration: AnchorViewDeclaration) =
        AnchorCoverage.plan anchors history declaration

/// The single declaration rule every caller shares: the coverage law decides
/// the shape, the host maps the first violation onto its stable failure code.
/// F# callers use this typed core directly; the JS boundary in
/// `AnchorSurface` answers with the plain-JS receipt of the same decision.
[<RequireQualifiedAccess>]
module AnchorDeclarationRule =
    /// Strict `{ prefixUntil, thenMap }` shape, judged by the coverage law.
    let parse (value: obj) : Result<AnchorViewDeclaration, JsFailure> =
        AnchorCoverage.parse value |> Result.mapError JsFailure.ViewDeclarationInvalid

    /// A report mapping is exactly a declaration with `prefixUntil = 0`, so it
    /// is judged by the same rule.
    let parseReportMapping (mapping: obj) : Result<AnchorViewDeclaration, JsFailure> =
        parse (createObj [ "prefixUntil" ==> 0; "thenMap" ==> mapping ])
