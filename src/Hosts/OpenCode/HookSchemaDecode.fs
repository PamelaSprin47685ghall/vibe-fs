module Wanxiangshu.Hosts.Opencode.HookSchemaDecode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime

open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Runtime.SubagentIntentsCodec
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Opencode.HookSchemaDecoration

let private tryBuildJsonSchemaFromEffectSchema (parameters: obj) : obj =
    Wanxiangshu.Hosts.Opencode.HookSchemaDecoration.tryBuildJsonSchemaFromEffectSchema parameters
