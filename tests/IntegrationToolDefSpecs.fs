module Wanxiangshu.Tests.IntegrationToolDefSpecs

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Hosts.Opencode.Plugin
open Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Tests.IntegrationToolDefSpecsMimo

[<Import("Schema", "effect")>]
let private effectSchemaNs: obj = jsNative

let private effectStruct (shape: obj) : obj = effectSchemaNs?("Struct") (shape)
let private effectString: obj = get effectSchemaNs "String"

let toolDefinitionSpec () = ()

let toolExecuteBeforeSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "tool-execute-before-"
        let! p = plugin (box {| directory = workspaceDir |})
        let teb = get p "tool.execute.before"

        let intents: obj array =
            [| sampleCoderIntent "fix bug" "a.ts"; sampleCoderIntent "add feature" "b.ts" |]

        let originalArgs = createObj [ "intents", box intents ]

        let execOut = createObj [ "args", box originalArgs ]

        do!
            teb
            $ (createObj [ "tool", box "coder"; "sessionID", box "s1"; "callID", box "c1" ], execOut)
            |> unbox<JS.Promise<unit>>

        let nextArgs = get execOut "args"
        check "tool.execute.before populates ui_ on returned args" (str nextArgs "ui_" = "fix bug; add feature")

        check
            "tool.execute.before clones and does not mutate in place"
            (not (obj.ReferenceEquals(nextArgs, originalArgs)))

        check
            "tool.execute.before writes ui_ onto host args reference"
            (str originalArgs "ui_" = "fix bug; add feature")

        do! rmAsync workspaceDir
    }
