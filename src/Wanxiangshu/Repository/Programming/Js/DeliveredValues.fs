namespace Wanxiangshu.Repository.Programming.Js

open System
open System.Collections.Generic
open Fable.Core
open Wanxiangshu.Foundation

/// Process-live delivered-value cell, keyed by session like the parked-site
/// table. A restart drops it; the durable terminal record is written when the
/// call settles, so nothing is read back from the cell across processes.
module JsDeliveredValues =

    // DSL-MUTABLE: resource — sessionId → delivered value
    let private delivered = Dictionary<string, obj>()

    [<Emit("$0 === undefined || $0 === null")>]
    let private isNullOrUndefined (value: obj) : bool = jsNative

    let record (sessionId: string) (value: obj) : unit = delivered.[sessionId] <- value

    let take (sessionId: string) : obj =
        match delivered.TryGetValue sessionId with
        | true, value ->
            delivered.Remove sessionId |> ignore
            value
        | false, _ -> null

    let peek (sessionId: string) : obj =
        match delivered.TryGetValue sessionId with
        | true, value -> value
        | false, _ -> null

    let private renderNonBlank (text: string) : string option =
        if String.IsNullOrWhiteSpace text then None else Some text

    let private renderStructured (structured: LlmFacing.Data.Value) : string option =
        (LlmFacing.empty
         |> LlmFacing.withData (LlmFacing.Data.structuredValue structured)
         |> LlmFacing.render)
            .TrimEnd('\n')
        |> renderNonBlank

    let private renderPresentValue (value: obj) : string option =
        match LlmFacing.Data.ofJs value with
        | LlmFacing.Data.Value.Null -> None
        | LlmFacing.Data.Value.String text -> renderNonBlank text
        | structured -> renderStructured structured

    let render (value: obj) : string option =
        if isNullOrUndefined value then
            None
        else
            renderPresentValue value

    let tryTakeRendered (sessionId: string) : string option = take sessionId |> render

    let tryPeekRendered (sessionId: string) : string option = peek sessionId |> render

    let clear (sessionId: string) : unit = delivered.Remove sessionId |> ignore

    let clearAll () : unit = delivered.Clear()
