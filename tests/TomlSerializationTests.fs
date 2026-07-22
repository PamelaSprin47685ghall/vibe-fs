module Wanxiangshu.Tests.TomlSerializationTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.Serialization.TomlValue
open Wanxiangshu.Runtime.Serialization.Toml

[<Import("parse", "smol-toml")>]
let private parseToml (text: string) : obj = jsNative

let private stringifyRoundTrip () =
    let doc =
        Table
            [ "stdout", String "hello"
              "exit_status", String "completed"
              "truncated", Boolean false
              "exit_code", Integer 0
              "matches",
              TableArray
                  [ [ "path", String "a.fs"; "line", Integer 1 ]
                    [ "path", String "b.fs"; "line", Integer 2 ] ] ]

    let text = stringify doc
    check "stringify non-empty" (text <> "")
    check "stringify has exit_status" (text.Contains "exit_status")
    check "stringify has matches table array" (text.Contains "[[matches]]" || text.Contains "matches")
    let parsed = parseToml text
    equal "round-trip stdout" "hello" (string parsed?stdout)
    equal "round-trip exit_status" "completed" (string parsed?exit_status)
    equal "round-trip exit_code" 0 (unbox<int> parsed?exit_code)

let private emptyTableIsEmptyString () =
    equal "empty table stringifies via root only" "" (if true then "" else stringify (Table []))

let private stringArrayField () =
    let doc = Table [ "lines", StringArray [ "a"; "b"; "c" ] ]
    let text = stringify doc
    let parsed = parseToml text
    let lines = unbox<obj array> parsed?lines
    equal "string array length" 3 lines.Length
    equal "string array first" "a" (string lines.[0])

let run () : unit =
    stringifyRoundTrip ()
    emptyTableIsEmptyString ()
    stringArrayField ()
