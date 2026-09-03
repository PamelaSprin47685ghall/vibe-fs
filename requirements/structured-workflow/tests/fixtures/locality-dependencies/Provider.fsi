namespace LocalityDependencyFixture

open Fable.Core

type PublicValue = { Text: string }

type PublicChoice =
    | PublicChoice of string

module Provider =
    val make: text: string -> PublicValue
    val preserve: value: 'value -> 'value
    val choose: condition: bool -> whenTrue: 'value -> whenFalse: 'value -> 'value

module FixtureInterop =
    [<Import("join", "node:path")>]
    val join: left: string -> right: string -> string

    [<Emit("Date.now()")>]
    val now: unit -> float

    val cwd: unit -> string
