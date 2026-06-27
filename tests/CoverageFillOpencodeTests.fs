module Wanxiangshu.Tests.CoverageFillOpencodeTests

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Opencode.ToolHelpers
open Wanxiangshu.Opencode.HookSchema
open Wanxiangshu.Kernel.Domain
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.Dyn

// ── Opencode.ToolHelpers ───────────────────────────────────────────────────

let toolHelpers () =
    equal "formatDomainError" "ctx failed: session busy" (Wanxiangshu.Opencode.ToolHelpers.formatDomainError "ctx" SessionBusy)
    let o = createObj [ "a", box "hi"; "b", box 42; "c", box true; "d", box null ]
    equal "optStr some" (Some "hi") (optStr o "a")
    equal "optStr none" None (optStr o "missing")
    equal "optStr null none" None (optStr o "d")
    equal "optInt some" (Some 42) (optInt o "b")
    equal "optInt none" None (optInt o "missing")
    equal "optBool some" (Some true) (optBool o "c")
    equal "optBool none" None (optBool o "missing")

// ── Opencode.HookSchema ────────────────────────────────────────────────────

let hookSchemaWarnTddProps () =
    let p = warnTddProperty
    equal "warnTdd type" "string" (Dyn.str p "type")
    equal "warnTdd minLength" 1 (unbox<int> (Dyn.get p "minLength"))
    check "warnTdd desc non-empty" ((Dyn.str p "description") <> "")
    let enumArr = unbox<obj[]> (Dyn.get p "enum")
    check "warnTdd enum has value" (enumArr.Length > 0)
    let ip = inlineJsonWarnTddProperty
    equal "inline type" "string" (Dyn.str ip "type")
    check "inline enum" (Dyn.isArray (Dyn.get ip "enum"))

let hookSchemaBuildWorkBacklogSchema () =
    let s = buildWorkBacklogSchema ()
    equal "schema type" "object" (Dyn.str s "type")
    let props = Dyn.get s "properties"
    check "has todos" (not (Dyn.isNullish (Dyn.get props "todos")))
    check "has completedWorkReport" (not (Dyn.isNullish (Dyn.get props "completedWorkReport")))
    check "has select_methodology" (not (Dyn.isNullish (Dyn.get props "select_methodology")))
    let req = unbox<obj[]> (Dyn.get s "required")
    check "required non-empty" (req.Length > 0)

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

let run () =
    toolHelpers ()
    hookSchemaWarnTddProps ()
    hookSchemaBuildWorkBacklogSchema ()
    hookSchemaRewriteToolJsonSchema ()
