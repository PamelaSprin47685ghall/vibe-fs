namespace LocalityDependencyFixture

open Fable.Core
open Fable.Core.JsInterop

type PublicValue = { Text: string }

type PublicChoice =
    | PublicChoice of string

module Provider =
    let make text = { Text = text }

    let preserve value = value

    let choose condition whenTrue whenFalse =
        if condition then whenTrue else whenFalse

module FixtureInterop =
    [<Import("join", "node:path")>]
    let join (left: string) (right: string) : string = jsNative

    [<Emit("Date.now()")>]
    let now () : float = jsNative

    let cwd () : string = emitJsExpr () "process.cwd()"
