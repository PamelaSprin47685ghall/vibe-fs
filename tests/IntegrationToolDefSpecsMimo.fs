module Wanxiangshu.Tests.IntegrationToolDefSpecsMimo

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Hosts.Opencode.Plugin
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel

let mimoApplyPatchExecuteBeforeSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mimo-apply-patch-before-"
        let! p = Wanxiangshu.Hosts.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |})
        let teb = get p "tool.execute.before"
        let stringArgsOut = createObj [ "args", box "*** Begin Patch\n*** End Patch" ]

        let input1 =
            createObj
                [ "tool", box "apply_patch"
                  "sessionID", box "s1"
                  "callID", box "c1"
                  "args", box (createObj [ "warn_tdd", box WarnTdd.canonicalValue ]) ]

        do! teb $ (input1, stringArgsOut) |> unbox<JS.Promise<unit>>

        check
            "mimo apply_patch execute.before wraps string args"
            (str (get stringArgsOut "args") "patchText" = "*** Begin Patch\n*** End Patch")

        let patchArgsOut =
            createObj [ "args", box (createObj [ "patch", box "*** Begin Patch\n*** End Patch" ]) ]

        let input2 =
            createObj
                [ "tool", box "apply_patch"
                  "sessionID", box "s1"
                  "callID", box "c2"
                  "args", box (createObj [ "warn_tdd", box WarnTdd.canonicalValue ]) ]

        do! teb $ (input2, patchArgsOut) |> unbox<JS.Promise<unit>>

        check
            "mimo apply_patch execute.before rewrites patch field"
            (str (get patchArgsOut "args") "patchText" = "*** Begin Patch\n*** End Patch")

        let correctArgsOut =
            createObj [ "args", box (createObj [ "patchText", box "already-correct" ]) ]

        let input3 =
            createObj
                [ "tool", box "apply_patch"
                  "sessionID", box "s1"
                  "callID", box "c3"
                  "args", box (createObj [ "warn_tdd", box WarnTdd.canonicalValue ]) ]

        do! teb $ (input3, correctArgsOut) |> unbox<JS.Promise<unit>>

        check
            "mimo apply_patch execute.before preserves patchText"
            (str (get correctArgsOut "args") "patchText" = "already-correct")

        let invalidArgsOut = createObj [ "args", box (createObj []) ]

        let input4 =
            createObj
                [ "tool", box "apply_patch"
                  "sessionID", box "s1"
                  "callID", box "c4"
                  "args", box (createObj [ "warn_tdd", box WarnTdd.canonicalValue ]) ]

        do! teb $ (input4, invalidArgsOut) |> unbox<JS.Promise<unit>>

        let errText = str invalidArgsOut "error"

        let expected =
            wireEncodeToolError "apply_patch" (InvalidIntent("apply_patch", "patchText", "required"))

        check "mimo apply_patch execute.before invalid args sets error" (errText <> "")
        check "mimo apply_patch execute.before error uses wireEncodeToolError InvalidIntent" (errText = expected)
        do! rmAsync workspaceDir
    }
