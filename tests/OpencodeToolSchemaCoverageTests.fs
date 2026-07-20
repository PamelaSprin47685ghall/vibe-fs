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
    equal
        "formatDomainError"
        "ctx failed: session busy"
        (wireEncodeToolError "ctx" SessionBusy)

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

let hookSchemaBuildWorkBacklogSchema () =
    let s = buildWorkBacklogSchema ()
    equal "schema type" "object" (Dyn.str s "type")
    let props = Dyn.get s "properties"
    check "has todos" (not (Dyn.isNullish (Dyn.get props "todos")))
    check "has ahaMoments" (not (Dyn.isNullish (Dyn.get props "ahaMoments")))
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

let hookSchemaWarnRequiredAlways () =
    // (1) injectWarnIntoJsonSchema must ensure 'warn' is in properties with soft-required metadata, but NOT in required.
    // First: empty required → NOT in required.
    let schemaEmpty =
        createObj
            [ "type", box "object"
              "properties",
              createObj
                  [ "warn",
                    box (
                        createObj
                            [| "type", box "string"
                               "enum", box [| box Wanxiangshu.Kernel.WarnTdd.warnCanonicalValue |]
                               "description", box Wanxiangshu.Kernel.WarnTdd.warnDescription |]
                    ) ]
              "required", box [||] ]

    let resultEmpty = injectWarnIntoJsonSchema schemaEmpty
    let reqEmpty = unbox<obj[]> (Dyn.get resultEmpty "required")
    check "warn NOT required after injectWarn (was empty)" (not (reqEmpty |> Array.exists (fun x -> string x = "warn")))
    // Second: 'warn' already in required → removed.
    let schemaPresent =
        createObj
            [ "type", box "object"
              "properties",
              createObj
                  [ "warn",
                    box (
                        createObj
                            [| "type", box "string"
                               "enum", box [| box Wanxiangshu.Kernel.WarnTdd.warnCanonicalValue |]
                               "description", box Wanxiangshu.Kernel.WarnTdd.warnDescription |]
                    ) ]
              "required", box [| box "warn" |] ]

    let resultPresent = injectWarnIntoJsonSchema schemaPresent
    let reqPresent = unbox<obj[]> (Dyn.get resultPresent "required")

    let warnCount =
        reqPresent |> Array.filter (fun x -> string x = "warn") |> Array.length

    equal "warn count after injectWarn (should be kept/added in required)" 1 warnCount

let hookSchemaWarnTddRequiredAlways () =
    // (2) injectWarnTddIntoJsonSchema must ensure 'warn_tdd' is in properties, but NOT in required.
    // First: empty required → NOT in required.
    let schemaEmpty =
        createObj
            [ "type", box "object"
              "properties",
              createObj
                  [ "warn_tdd",
                    box (
                        createObj
                            [| "type", box "string"
                               "enum", box [| box Wanxiangshu.Kernel.WarnTdd.canonicalValue |]
                               "description", box Params.warnTddDesc |]
                    ) ]
              "required", box [||] ]

    let resultEmpty = injectWarnTddIntoJsonSchema schemaEmpty
    let reqEmpty = unbox<obj[]> (Dyn.get resultEmpty "required")

    check
        "warn_tdd NOT required after injectWarnTdd (was empty)"
        (not (reqEmpty |> Array.exists (fun x -> string x = "warn_tdd")))
    // Second: 'warn_tdd' already in required → removed.
    let schemaPresent =
        createObj
            [ "type", box "object"
              "properties",
              createObj
                  [ "warn_tdd",
                    box (
                        createObj
                            [| "type", box "string"
                               "enum", box [| box Wanxiangshu.Kernel.WarnTdd.canonicalValue |]
                               "description", box Params.warnTddDesc |]
                    ) ]
              "required", box [| box "warn_tdd" |] ]

    let resultPresent = injectWarnTddIntoJsonSchema schemaPresent
    let reqPresent = unbox<obj[]> (Dyn.get resultPresent "required")

    let warnTddCount =
        reqPresent |> Array.filter (fun x -> string x = "warn_tdd") |> Array.length

    equal "warn_tdd count after injectWarnTdd (should be kept/added in required)" 1 warnTddCount

let hookSchemaExecutorCombinedWarns () =
    // (3) Real Opencode tool.definition hook provides output.jsonSchema directly.
    // Pre-populate jsonSchema with a synthetic executor schema; injectors rewrite in-place.
    let executorJsonSchema =
        createObj
            [ "type", box "object"
              "properties",
              createObj [ "command", box (createObj [ "type", box "string"; "description", box "Command to run" ]) ]
              "required", box [| box "command" |] ]

    let output = createObj [ "jsonSchema", executorJsonSchema ]
    // Compose both injectors: warn_tdd first, then warn.
    let rewrite (schema: obj) : obj =
        injectWarnTddIntoJsonSchema schema |> ignore
        injectWarnIntoJsonSchema schema |> ignore
        schema

    rewriteToolJsonSchema (fun _ _ _ -> ()) rewrite output |> ignore
    let resultSchema = Dyn.get output "jsonSchema"
    check "output.jsonSchema is non-nullish" (not (Dyn.isNullish resultSchema))
    let resultReq = unbox<obj[]> (Dyn.get resultSchema "required")

    check
        "warn_tdd NOT in required (prompt constraint only)"
        (not (resultReq |> Array.exists (fun x -> string x = "warn_tdd")))

    check "warn NOT in required (prompt constraint only)" (not (resultReq |> Array.exists (fun x -> string x = "warn")))

    check "command in required" (resultReq |> Array.exists (fun x -> string x = "command"))

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
    hookSchemaWarnTddProps ()
    hookSchemaBuildWorkBacklogSchema ()
    hookSchemaRewriteToolJsonSchema ()
    hookSchemaWarnTddRequiredAlways ()
    hookSchemaWarnRequiredAlways ()
    hookSchemaExecutorCombinedWarns ()
    hookSchemaPtySpawnWarnSets ()
    hookSchemaMethodologyNotInWarnSets ()
