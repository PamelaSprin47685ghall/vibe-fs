namespace Wanxiangshu.Repository.Programming.Js.Anchor

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Repository.Programming.Js

[<RequireQualifiedAccess>]
type AnchorCarryItem =
    | Reference of int
    | Literal of string

type AnchorViewDeclaration =
    { PrefixUntil: int
      Carry: AnchorCarryItem list }

type AnchorMessage =
    { Id: string option
      Text: string
      Raw: obj }

type AnchorSegment =
    { Anchor: int
      Messages: AnchorMessage list }

type AnchorViewReport =
    { Total: int
      Used: int
      Cached: int
      Kept: int
      Inserted: int
      Dropped: int
      PrefixCount: int }

[<RequireQualifiedAccess>]
module AnchorCarry =
    let toValue (items: AnchorCarryItem list) : obj =
        items
        |> List.map (function
            | AnchorCarryItem.Reference anchor -> box anchor
            | AnchorCarryItem.Literal text -> box text)
        |> List.toArray
        |> box

[<RequireQualifiedAccess>]
module AnchorDeclarationRule =
    [<AllowNullLiteral>]
    type private CompressionLaw =
        abstract parseView: value: obj -> obj
        abstract parseCarry: value: obj -> obj

    [<Import("Compression", "wanxiangli/compression")>]
    let private createCompression: unit -> CompressionLaw = jsNative

    let private law = createCompression ()

    let private carryOf (value: obj) =
        (unbox<obj array> value)
        |> Array.toList
        |> List.map (fun item ->
            if jsTypeof item = "number" then
                AnchorCarryItem.Reference(unbox<int> item)
            else
                AnchorCarryItem.Literal(unbox<string> item))

    let parseCarry (value: obj) : Result<AnchorCarryItem list, JsFailure> =
        let parsed = law.parseCarry value

        if parsed?ok then
            Ok(carryOf parsed?carry)
        else
            Error(JsFailure.ViewDeclarationInvalid parsed?reason)

    let parse (value: obj) : Result<AnchorViewDeclaration, JsFailure> =
        let parsed = law.parseView value

        if parsed?ok then
            let view = parsed?view

            Ok
                { PrefixUntil = view?prefixUntil
                  Carry = carryOf view?carry }
        else
            Error(JsFailure.ViewDeclarationInvalid parsed?reason)

[<RequireQualifiedAccess>]
module AnchorHistory =
    let append (numberOf: string -> int) (segments: AnchorSegment list) (messages: AnchorMessage list) =
        let add rows message =
            match message.Id, rows with
            | Some id, _ ->
                { Anchor = numberOf id
                  Messages = [ message ] }
                :: rows
            | None, head :: tail ->
                { head with
                    Messages = head.Messages @ [ message ] }
                :: tail
            | None, [] -> [ { Anchor = 0; Messages = [ message ] } ]

        messages |> List.fold add (List.rev segments) |> List.rev

    let private materialForAnchor (segments: AnchorSegment list) anchor =
        match segments |> List.tryFind (fun segment -> segment.Anchor = anchor) with
        | Some segment -> Ok(Some anchor, segment.Messages |> List.map _.Text |> String.concat "\n")
        | None -> Error(JsFailure.ViewDeclarationInvalid(sprintf "carry references an unavailable anchor: %d" anchor))

    let select (segments: AnchorSegment list) (items: AnchorCarryItem list) =
        let selectOne item =
            match item with
            | AnchorCarryItem.Literal text -> Ok(None, text)
            | AnchorCarryItem.Reference anchor -> materialForAnchor segments anchor

        let rec loop remaining selected =
            match remaining with
            | [] -> Ok(List.rev selected)
            | item :: tail -> selectOne item |> Result.bind (fun material -> loop tail (material :: selected))

        loop items []

    let validate (segments: AnchorSegment list) (declaration: AnchorViewDeclaration) =
        select segments declaration.Carry |> Result.map ignore
