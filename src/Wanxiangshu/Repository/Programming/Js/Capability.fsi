namespace Wanxiangshu.Repository.Programming.Js

open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type JsCapability =
    | Read
    | Write
    | Edit
    | Glob
    | Grep

module JsCapability =
    val ofToolPermission: permission: ToolPermission -> JsCapability option
    val ofToolCapabilities: capabilities: Set<ToolPermission> -> Set<JsCapability>
    val order: capability: JsCapability -> int

type JsCapabilityFragment =
    { Capability: JsCapability
      MemberName: string
      Signature: string
      Description: string
      CanonicalExample: string
      RuntimeBindingKey: string }

module JsFragmentRegistry =
    val read: JsCapabilityFragment
    val glob: JsCapabilityFragment
    val grep: JsCapabilityFragment
    val edit: JsCapabilityFragment
    val rewrite: JsCapabilityFragment
    val write: JsCapabilityFragment
    val all: JsCapabilityFragment list
    val byCapability: Map<JsCapability, JsCapabilityFragment list>
