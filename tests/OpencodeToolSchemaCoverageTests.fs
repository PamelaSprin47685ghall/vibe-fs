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

let hookSchemaWarnTddProps () =
    let p = warnTddProperty
    equal "warnTdd type" "string" (Dyn.str p "type")
    check "warnTdd has no hard minLength" (Dyn.isNullish (Dyn.get p "minLength"))
    check "warnTdd desc non-empty" ((Dyn.str p "description") <> "")
    check "warnTdd has no hard enum" (Dyn.isNullish (Dyn.get p "enum"))
    let ip = inlineJsonWarnTddProperty
    equal "inline type" "string" (Dyn.str ip "type")
    check "inline has no hard enum" (Dyn.isNullish (Dyn.get ip "enum"))

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

let hookSchemaPtySpawnWarnSets () =
    // (4) pty_spawn is in both the WarnTdd modification set and the warn-required set.
    check "pty_spawn isModificationTool" (Wanxiangshu.Kernel.WarnTdd.isModificationTool "pty_spawn")
    check "pty_spawn isWarnRequiredTool" (Wanxiangshu.Kernel.WarnTdd.isWarnRequiredTool "pty_spawn")

let hookSchemaMethodologyNotInWarnSets () =
    // (5) A methodology tool name is in neither set.
    // methodology is the representative methodology tool name.
    let methodologyTool = "methodology"

    check
        "methodology tool NOT isModificationTool"
        (not (Wanxiangshu.Kernel.WarnTdd.isModificationTool methodologyTool))

    check
        "methodology tool NOT isWarnRequiredTool"
        (not (Wanxiangshu.Kernel.WarnTdd.isWarnRequiredTool methodologyTool))

let run () =
    toolHelpers ()
    hookSchemaPtySpawnWarnSets ()
    hookSchemaMethodologyNotInWarnSets ()
