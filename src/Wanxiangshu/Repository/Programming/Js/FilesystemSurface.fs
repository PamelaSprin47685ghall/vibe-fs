namespace Wanxiangshu.Repository.Programming.Js

open Fable.Core.JsInterop

/// JS-native filesystem owner boundary. Paths, anchor declarations, listings
/// and mutation plans are plain data; Node filesystem effects remain behind the
/// production adapters.
[<RequireQualifiedAccess>]
module JsFilesystemSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private failureResult failure =
        box
            {| ok = false
               code = JsFailure.code failure
               reason = JsFailure.reason failure |}

    let private anchorOf (value: obj) : AnchorSpec =
        match text (value?kind) with
        | "exact" -> AnchorSpec.Exact(text (value?text))
        | "regex" -> AnchorSpec.Regex(text (value?text))
        | other -> invalidArg "kind" (sprintf "unknown anchor kind: %s" other)

    let private planOf (value: obj) : (string * string) list =
        if isNull value then
            []
        else
            unbox<obj array> value
            |> Array.toList
            |> List.map (fun item ->
                let pair = unbox<obj array> item
                text pair[0], text pair[1])

    let private rollbackOf (value: obj) : (string * string option) list =
        if isNull value then
            []
        else
            unbox<obj array> value
            |> Array.toList
            |> List.map (fun item ->
                let pair = unbox<obj array> item
                let original = if isNull pair[1] then None else Some(text pair[1])
                text pair[0], original)

    let readUtf8 (path: string) : obj =
        match JsUtf8Fs.readUtf8Classified path with
        | Ok value -> box {| ok = true; value = value |}
        | Error failure -> failureResult failure

    let glob (root: string) (pattern: string) : obj =
        match JsGlobFs.glob root pattern with
        | Ok listing ->
            box
                {| ok = true
                   paths = listing.Paths |> List.toArray |}
        | Error failure -> failureResult failure

    let findAnchor (textValue: string) (declaration: obj) (occurrence: int) : obj =
        match JsAnchorFs.findAnchor textValue (anchorOf declaration) occurrence with
        | Ok(startIndex, endIndex) ->
            box
                {| ok = true
                   value = [| box startIndex; box endIndex |] |}
        | Error failure -> failureResult failure

    let requireUnique (textValue: string) (declaration: obj) : obj =
        match JsAnchorFs.requireUnique textValue (anchorOf declaration) with
        | Ok(startIndex, endIndex) ->
            box
                {| ok = true
                   value = [| box startIndex; box endIndex |] |}
        | Error failure -> failureResult failure

    let grep (root: string) (declaration: obj) (pattern: string) : obj =
        match JsAnchorFs.grep root (anchorOf declaration) pattern with
        | Error failure -> failureResult failure
        | Ok listing ->
            let matches =
                listing.Matches
                |> List.map (fun hit ->
                    box
                        {| path = hit.Path
                           line = hit.Line
                           column = hit.Column
                           text = hit.Text |})
                |> List.toArray

            box {| ok = true; matches = matches |}

    let commitPlan (root: string) (plan: obj array) : obj =
        match JsMutationFs.commitPlan root (planOf (box plan)) with
        | Ok() -> box {| ok = true |}
        | Error failure -> failureResult failure

    let rollbackPlan (root: string) (plan: obj array) : unit =
        JsMutationFs.rollbackPlan root (rollbackOf (box plan))
