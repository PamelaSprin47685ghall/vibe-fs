module Wanxiangshu.Tests.AmendSchemaTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Opencode.HookSchemaDecode
open Wanxiangshu.Shell.MuxPluginCatalogShell
open Wanxiangshu.Shell.MuxToolDefinition
open Wanxiangshu.Omp.OmpToolSchema

module Dyn = Wanxiangshu.Shell.Dyn

let testAmendSchemaInjected () =
    let opencodeSchema =
        createObj [ "type", box "object"; "properties", createObj [ "name", box (createObj []) ] ]

    let opencodeResult = injectAmendIntoJsonSchema opencodeSchema
    let opencodeProps = Dyn.get opencodeResult "properties"
    check "opencode schema has amend property" (not (Dyn.isNullish (Dyn.get opencodeProps "amend")))
    let amendProp = Dyn.get opencodeProps "amend"
    equal "opencode amend type" "integer" (string (Dyn.get amendProp "type"))
    check "opencode amend minimum = 1" (string (Dyn.get amendProp "minimum") = "1")

    let muxTool =
        { name = "coder"
          description = "test"
          parameters = mkSchema (createObj [ "file", box (createObj []) ]) [| "file" |]
          execute = (fun _ _ -> failwith "not implemented")
          condition = (None: (obj -> bool) option) }

    let muxResult = injectAmendIntoMuxSchema muxTool
    let muxProps = muxResult.parameters.properties
    check "mux schema has amend property" (not (Dyn.isNullish (Dyn.get muxProps "amend")))
    let muxAmend = Dyn.get muxProps "amend"
    equal "mux amend type" "integer" (string (Dyn.get muxAmend "type"))

    let ompSchema = createObj [ "properties", createObj [ "file", box (createObj []) ] ]
    let ompResult = injectAmendIntoOmpParameters ompSchema
    let ompProps = Dyn.get ompResult "properties"
    check "omp schema has amend property" (not (Dyn.isNullish (Dyn.get ompProps "amend")))
    let ompAmend = Dyn.get ompProps "amend"
    equal "omp amend type" "integer" (string (Dyn.get ompAmend "type"))

let runAll () : unit =
    timed "testAmendSchemaInjected" testAmendSchemaInjected
