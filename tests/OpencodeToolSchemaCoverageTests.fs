module Wanxiangshu.Tests.OpencodeToolSchemaCoverageTests

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Hosts.Opencode.HookSchemaDecoration
open Wanxiangshu.Hosts.Opencode.HookSchemaDecode
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Runtime.DynField

module Dyn = Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Dyn

// ── DynField / ToolResult probes (HostField deleted) ───────────────────────

let toolHelpers () =
    equal "formatDomainError" "ctx failed: session busy" (wireEncodeToolError "ctx" SessionBusy)

    let o = createObj [ "a", box "hi"; "b", box 42; "c", box true; "d", box null ]
    equal "optStr some" (Some "hi") (strField o "a")
    equal "optStr none" None (strField o "missing")
    equal "optStr null none" None (strField o "d")
    equal "optInt some" (Some 42) (optInt o "b")
    equal "optInt none" None (optInt o "missing")
    equal "optBool some" (Some true) (optBool o "c")
    equal "optBool none" None (optBool o "missing")

// ── Opencode.HookSchema ────────────────────────────────────────────────────

let hookSchemaDummySchema () = ()

let hookSchemaRewriteToolJsonSchema () =
    let rewrite (o: obj) : obj =
        o?("tag") <- "rewritten"
        o

    let mutable lastKey = ""

    let setKey (o: obj) (k: string) (v: obj) : unit =
        lastKey <- k
        o?(k) <- v

    let outJson = createObj [ "jsonSchema", createObj [ "a", box 1 ] ]
    rewriteToolJsonSchema setKey rewrite outJson |> ignore
    equal "jsonSchema rewritten" "rewritten" (string (outJson?("jsonSchema")?("tag")))
    let outParams = createObj [ "parameters", createObj [ "b", box 2 ] ]
    rewriteToolJsonSchema setKey rewrite outParams |> ignore
    equal "parameters rewritten" "rewritten" (string (outParams?("parameters")?("tag")))
    let outArgs = createObj [ "args", createObj [ "c", box 3 ] ]
    rewriteToolJsonSchema setKey rewrite outArgs |> ignore
    equal "args rewritten" "rewritten" (string (outArgs?("args")?("tag")))
    let outNone = createObj []
    rewriteToolJsonSchema setKey rewrite outNone |> ignore
    equal "no schema no setKey" "" lastKey

let run () = toolHelpers ()
