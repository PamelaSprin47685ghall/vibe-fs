namespace Wanxiangshu.Sphinx

open Fable.Core
open FsToolkit.ErrorHandling

module DecodePrimitives =

    [<Emit("$0 == null")>]
    let isNullish (value: obj) : bool = jsNative

    [<Emit("typeof $0")>]
    let jsType (value: obj) : string = jsNative

    [<Emit("Array.isArray($0)")>]
    let isArray (value: obj) : bool = jsNative

    [<Emit("Object.keys($0)")>]
    let private keys (value: obj) : string array = jsNative

    [<Emit("$0[$1]")>]
    let private get (value: obj) (key: obj) : obj = jsNative

    [<Emit("Number.isFinite($0)")>]
    let private isFiniteNumber (value: obj) : bool = jsNative

    let private field name value =
        let found = get value (box name)
        if isNullish found then None else Some found

    let asString value =
        if jsType value = "string" then
            Ok(unbox<string> value)
        else
            Error "expected string"

    let asFloat value =
        if jsType value = "number" && isFiniteNumber value then
            Ok(unbox<float> value)
        else
            Error "expected finite number"

    let asBool value =
        if jsType value = "boolean" then
            Ok(unbox<bool> value)
        else
            Error "expected boolean"

    let required name decoder value =
        match field name value with
        | None -> Error($"{name} required")
        | Some raw -> decoder raw |> Result.mapError (fun error -> $"{name}: {error}")

    let optional name decoder fallback value =
        match field name value with
        | None -> Ok fallback
        | Some raw -> decoder raw |> Result.mapError (fun error -> $"{name}: {error}")

    let asArray decoder value =
        if not (isArray value) then
            Error "expected array"
        else
            let length: int = unbox (get value (box "length"))

            [ 0 .. length - 1 ]
            |> List.fold
                (fun state index ->
                    result {
                        let! accumulated = state
                        let! item = decoder (get value (box index))
                        return item :: accumulated
                    })
                (Ok [])
            |> Result.map List.rev

    let stringList value = asArray asString value

    let stringMap value =
        if isNullish value || jsType value <> "object" || isArray value then
            Error "expected object"
        else
            keys value
            |> Array.toList
            |> List.fold
                (fun state key ->
                    result {
                        let! accumulated = state
                        let! number = asFloat (get value (box key))
                        return Map.add key number accumulated
                    })
                (Ok Map.empty)

    let private parseForm =
        function
        | "Why" -> Some QuestionForm.Why
        | "How" -> Some QuestionForm.How
        | "What" -> Some QuestionForm.What
        | "Who" -> Some QuestionForm.Who
        | "Where" -> Some QuestionForm.Where
        | "When" -> Some QuestionForm.When
        | "Which" -> Some QuestionForm.Which
        | "Polar" -> Some QuestionForm.Polar
        | "Other" -> Some QuestionForm.Other
        | _ -> None

    let formMap value =
        result {
            let! raw = stringMap value

            return!
                raw
                |> Map.toList
                |> List.fold
                    (fun state (key, probability) ->
                        result {
                            let! accumulated = state

                            match parseForm key with
                            | Some form -> return Map.add form probability accumulated
                            | None -> return! Error($"unknown QuestionForm: {key}")
                        })
                    (Ok Map.empty)
        }

    let parseEvidenceKind =
        function
        | "document" -> EvidenceKind.Document
        | "tool" -> EvidenceKind.Tool
        | "user" -> EvidenceKind.UserSupplied
        | "measurement" -> EvidenceKind.Measurement
        | "dataset" -> EvidenceKind.Dataset
        | _ -> EvidenceKind.Other
