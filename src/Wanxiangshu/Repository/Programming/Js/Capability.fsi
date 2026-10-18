namespace Wanxiangshu.Repository.Programming.Js

open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type JsCapability =
    | Read
    | Write
    | Edit
    | Glob
    | Grep
    | Move
    | Remove
    | Exec
    | Fork
    | Join
    | Park

module JsCapability =
    val ofToolPermission: permission: ToolPermission -> JsCapability option
    val ofToolCapabilities: capabilities: Set<ToolPermission> -> Set<JsCapability>

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
    val rm: JsCapabilityFragment
    val mv: JsCapabilityFragment
    val cat: JsCapabilityFragment
    val ls: JsCapabilityFragment
    val stat: JsCapabilityFragment
    val mkdir: JsCapabilityFragment
    val whereAll: JsCapabilityFragment
    val where: JsCapabilityFragment
    val run: JsCapabilityFragment
    val fork: JsCapabilityFragment
    val join: JsCapabilityFragment
    val park: JsCapabilityFragment
    val all: JsCapabilityFragment list
    val byCapability: Map<JsCapability, JsCapabilityFragment list>
