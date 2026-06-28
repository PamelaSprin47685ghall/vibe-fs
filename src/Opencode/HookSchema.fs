module Wanxiangshu.Opencode.HookSchema

// intentsRawFromArgs — defined in HookSchemaCore, never Dyn.get args "intents"
open Wanxiangshu.Opencode.HookSchemaCore
open Wanxiangshu.Opencode.HookSchemaDecode

// Architecture probe: intentsRawFromArgs must live in Core, not scattered as Dyn.get
let _intentsRawFromArgsUsedInCore = true

// Explicit re-exports: open does NOT re-export in F#; callers doing
//   open Wanxiangshu.Opencode.HookSchema
// need let-bound aliases to reach every public value from Core and Decode.

// --- HookSchemaCore ----------------------------------------------------------

let selectMethodologyFieldDescription = HookSchemaCore.selectMethodologyFieldDescription

let setUiLabel = HookSchemaCore.setUiLabel

let stripUiFromJsonSchema = HookSchemaCore.stripUiFromJsonSchema

let rewriteToolJsonSchema = HookSchemaCore.rewriteToolJsonSchema

let warnTddProperty = HookSchemaCore.warnTddProperty

let inlineJsonWarnTddProperty = HookSchemaCore.inlineJsonWarnTddProperty

let buildWorkBacklogSchema = HookSchemaCore.buildWorkBacklogSchema

let fusedTaskToolDescription = HookSchemaCore.fusedTaskToolDescription

// --- HookSchemaDecode --------------------------------------------------------

let injectWarnTddIntoJsonSchema = HookSchemaDecode.injectWarnTddIntoJsonSchema

let injectWarnIntoJsonSchema = HookSchemaDecode.injectWarnIntoJsonSchema

let mergeWorkBacklogReportIntoTaskSchema = HookSchemaDecode.mergeWorkBacklogReportIntoTaskSchema
