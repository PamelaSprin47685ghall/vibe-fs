namespace Wanxiangshu.Execution.Tool

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host.Ports

/// The product's own accessors over the canonical JSON arguments of one tool
/// call. A host adapter turns its decoded arguments into canonical JSON at the
/// boundary; from there the product never sees a host payload object again.
///
/// Every accessor is total: a malformed or absent argument reads as absent,
/// never as a thrown error, because a tool body that throws on a missing
/// argument would answer the host with an exception instead of a refusal.
module ToolArgs =

    [<Emit("JSON.parse($0)")>]
    let private parseJson (text: string) : obj = jsNative

    [<Emit("typeof $0 === 'string'")>]
    let private isString (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'number' && Number.isSafeInteger($0)")>]
    let private isSafeInteger (value: obj) : bool = jsNative

    [<Emit("Array.isArray($0)")>]
    let private isArray (value: obj) : bool = jsNative

    /// The parsed argument object, or `None` when the arguments are not JSON.
    let private tryParse (text: string) : obj option =
        try
            let value = parseJson text

            if isNull value then None else Some value
        with _ ->
            None

    /// The parsed argument object, or `None` when the arguments are not JSON.
    let private root (args: CanonicalJsonText) : obj option =
        match args with
        | CanonicalJsonText text when not (String.IsNullOrWhiteSpace text) -> tryParse text
        | _ -> None

    /// The raw value of one argument, or `None` when it is absent.
    let raw (args: CanonicalJsonText) (name: string) : obj option =
        root args
        |> Option.bind (fun value ->
            let field = value?(name)

            if isNull field then None else Some field)

    /// The text of one argument; an absent or non-text argument reads as empty.
    let text (args: CanonicalJsonText) (name: string) : string =
        raw args name
        |> Option.filter isString
        |> Option.map string
        |> Option.defaultValue ""

    /// The text of one argument, when it is present and not blank.
    let optionalText (args: CanonicalJsonText) (name: string) : string option =
        let value = text args name

        if String.IsNullOrWhiteSpace value then None else Some value

    /// The number of one argument, when it is a whole number.
    let optionalNumber (args: CanonicalJsonText) (name: string) : int option =
        raw args name
        |> Option.filter isSafeInteger
        |> Option.map (fun value -> int (unbox<float> value))

    /// The non-negative whole number of one argument.
    let optionalNonNegativeInteger (args: CanonicalJsonText) (name: string) : Result<int option, unit> =
        match optionalNumber args name with
        | Some value when value >= 0 -> Ok(Some value)
        | Some _ -> Error()
        | None -> Ok None

    /// The texts of one array argument; an absent or non-array argument is empty.
    let texts (args: CanonicalJsonText) (name: string) : string list =
        raw args name
        |> Option.filter isArray
        |> Option.map (fun value ->
            unbox<obj array> value
            |> Array.choose (fun item ->
                if isNull item || not (isString item) then
                    None
                else
                    Some(string item))
            |> Array.toList)
        |> Option.defaultValue []
