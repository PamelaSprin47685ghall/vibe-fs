namespace LocalityDependencyFixture

open Fable.Core
open Fable.Core.JsInterop

type PublicValue = { Text: string }

type PublicChoice =
    | PublicChoice of string

module Provider =
    type private MutableCell = { mutable Value: int }

    type private CapabilityPort =
        abstract Invoke: unit -> unit

    type private MutableOwner() =
        let mutable state = 0
        member _.Read() = state
        member _.Write(value) = state <- value

    let mutable private moduleCell = 0

    let private preserveCapability (capability: CapabilityPort) = capability

    let private makeCounter () =
        let mutable count = 0

        fun () ->
            count <- count + 1
            count

    let make text = { Text = text }

    let preserve value = value

    let choose condition whenTrue whenFalse =
        if condition then whenTrue else whenFalse

    let duplicateConstants condition =
        if condition then 7 else 7

    let duplicateExternal values =
        List.map id values, List.map id values

    let classifyMutableScope value =
        let mutable localCell = value
        localCell <- localCell + 1
        moduleCell <- localCell
        let objectCell = { Value = moduleCell }
        objectCell.Value <- objectCell.Value + 1
        objectCell.Value

module FixtureInterop =
    [<Import("join", "node:path")>]
    let join (left: string) (right: string) : string = jsNative

    [<Emit("Date.now()")>]
    let now () : float = jsNative

    let cwd () : string = emitJsExpr () "process.cwd()"
