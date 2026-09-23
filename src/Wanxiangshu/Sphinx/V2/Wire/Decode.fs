namespace Wanxiangshu.Sphinx.V2.Wire

open System
open Fable.Core.JsInterop
open Wanxiangshu.Sphinx.V2.Core

/// The v2 wire boundary.
///
/// WHAT[sphinx-v2-018]: strict decoding. Nothing is defaulted into shape. A missing
/// field, an unknown enum, aNaN, an Infinity, a non-integer attempt, a negative
/// resource, a repeated reference or an unknown schema is refused with a typed error
/// that names the field — not folded into a plausible-looking value.
///
/// WHAT[sphinx-v2-020]: revisions cross the wire as decimal strings. A JavaScript
/// number cannot represent every 64-bit integer, so `revision: 9007199254740993` and
/// `revision: 9007199254740992` must not compare equal.

type WireError =
    { Code: string
      Path: string
      Message: string }

module Decode =

    let private error code path message : Result<'value, WireError> =
        Error
            { Code = code
              Path = path
              Message = message }

    let private isBlank (value: string) = String.IsNullOrWhiteSpace value

    let private isString (value: obj) : bool =
        emitJsExpr value "typeof $0 === 'string'"

    let private isFiniteNumber (value: obj) : bool =
        emitJsExpr value "typeof $0 === 'number' && Number.isFinite($0)"

    let private isSafeCount (value: obj) : bool =
        emitJsExpr value "typeof $0 === 'number' && Number.isSafeInteger($0) && $0 >= 0"

    let private isArray (value: obj) : bool = emitJsExpr value "Array.isArray($0)"

    let private field (raw: obj) (name: string) : obj = emitJsExpr (raw, name) "$0[$1]"

    let private blankText (value: obj) : bool = isBlank (string value)

    /// A string field, present and non-blank.
    let stringField (raw: obj) (name: string) : Result<string, WireError> =
        let value = field raw name

        match isString value, blankText value with
        | false, _ -> error "INVALID_SCHEMA" name (sprintf "field %s must be a string" name)
        | true, true -> error "INVALID_SCHEMA" name (sprintf "field %s must not be blank" name)
        | true, false -> Ok(unbox<string> value)

    /// A finite number. NaN and Infinity are refused rather than clamped: a caller that
    /// sends them has a bug, and silently repairing it hides the bug.
    let finiteField (raw: obj) (name: string) : Result<float, WireError> =
        let value = field raw name

        match isFiniteNumber value with
        | true -> Ok(unbox<float> value)
        | false -> error "INVALID_SCHEMA" name (sprintf "field %s must be a finite number" name)

    /// A checked non-negative quantity. Negative tokens or calls are a defect, not a
    /// request to be normalized.
    let nonNegativeIntegerField (raw: obj) (name: string) : Result<int64, WireError> =
        let value = field raw name

        match isSafeCount value with
        | true -> Ok(unbox<int64> value)
        | false -> error "INVALID_SCHEMA" name (sprintf "field %s must be a non-negative safe integer" name)

    let private revisionFault (message: string) : WireError =
        { Code = "INVALID_REVISION"
          Path = "revision"
          Message = message }

    let private revisionOf (parsed: int64) : Result<Revision, WireError> =
        match Revision.tryCreate parsed with
        | Ok revision -> Ok revision
        | Error message -> Error(revisionFault message)

    let private parsedRevision (text: string) : Result<Revision, WireError> =
        match Int64.TryParse text with
        | true, value -> revisionOf value
        | false, _ -> Error(revisionFault "revision must be a decimal string")

    /// A revision arrives as a decimal string so no precision is lost in JavaScript.
    let revisionField (raw: obj) (name: string) : Result<Revision, WireError> =
        stringField raw name |> Result.bind parsedRevision

    /// A list of unique, non-blank strings. A repeated entry is a defect.
    let uniqueStringListField (raw: obj) (name: string) : Result<string list, WireError> =
        let value = field raw name

        let items () =
            unbox<obj array> value |> Array.map string |> Array.toList

        let blankEntry () =
            error "INVALID_SCHEMA" name (sprintf "field %s must not contain blank entries" name)

        let repeated () =
            error "INVALID_SCHEMA" name (sprintf "field %s must not repeat an entry" name)

        let distinct () =
            let listed = items ()
            let unique = List.length listed = (listed |> Set.ofList |> Set.count)

            match unique with
            | true -> Ok listed
            | false -> repeated ()

        match isArray value, List.exists isBlank (items ()) with
        | false, _ -> error "INVALID_SCHEMA" name (sprintf "field %s must be an array" name)
        | true, true -> blankEntry ()
        | true, false -> distinct ()
