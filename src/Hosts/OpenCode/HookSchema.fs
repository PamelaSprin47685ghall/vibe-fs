module Wanxiangshu.Hosts.Opencode.HookSchema

// intentsRawFromArgs — defined in HookSchemaDecoration, never Dyn.get args "intents"
open Wanxiangshu.Hosts.Opencode.HookSchemaDecoration
open Wanxiangshu.Hosts.Opencode.HookSchemaDecode

// Explicit re-exports: open does NOT re-export in F#; callers doing
//   open Wanxiangshu.Hosts.Opencode.HookSchema
// need let-bound aliases to reach every public value from Core and Decode.

// --- HookSchemaDecoration -----------------------------------------------------

let selectMethodologyFieldDescription =
    HookSchemaDecoration.selectMethodologyFieldDescription

let setUiLabel = HookSchemaDecoration.setUiLabel

let stripUiFromJsonSchema = HookSchemaDecoration.stripUiFromJsonSchema

let rewriteToolJsonSchema = HookSchemaDecoration.rewriteToolJsonSchema

let warnTddProperty = HookSchemaDecoration.warnTddProperty

let inlineJsonWarnTddProperty = HookSchemaDecoration.inlineJsonWarnTddProperty

let buildWorkBacklogSchema = HookSchemaDecoration.buildWorkBacklogSchema

let fusedTaskToolDescription = HookSchemaDecoration.fusedTaskToolDescription

// --- HookSchemaDecode --------------------------------------------------------

let injectWarnTddIntoJsonSchema = HookSchemaDecode.injectWarnTddIntoJsonSchema

let injectWarnIntoJsonSchema = HookSchemaDecode.injectWarnIntoJsonSchema

let injectWarnReuseIntoJsonSchema = HookSchemaDecode.injectWarnReuseIntoJsonSchema


let mergeWorkBacklogReportIntoTaskSchema =
    HookSchemaDecode.mergeWorkBacklogReportIntoTaskSchema
