namespace Wanxiangshu.Repository.Programming.Js

open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type JsCapability =
    | Read
    | Write

    | Glob
    | Grep
    | Move
    | Remove
    | Exec
    | Fork
    | Resume
    | Commission
    | DevOps
    | Join
    | Fission
    | Horizon
    | Advisor
    | Ask
    | Park

module JsCapability =
    val ofToolPermission: permission: ToolPermission -> JsCapability option
    val ofToolCapabilities: capabilities: Set<ToolPermission> -> Set<JsCapability>

    val admitsProgrammingSurface: capabilities: Set<ToolPermission> -> bool

type JsCapabilityFragment =
    { Capability: JsCapability
      MemberName: string }

module JsFragmentRegistry =
    val write: JsCapabilityFragment
    val rm: JsCapabilityFragment
    val mv: JsCapabilityFragment
    val cat: JsCapabilityFragment
    val ls: JsCapabilityFragment
    val rg: JsCapabilityFragment
    val stat: JsCapabilityFragment
    val mkdir: JsCapabilityFragment
    val whereAll: JsCapabilityFragment
    val where: JsCapabilityFragment
    val call: JsCapabilityFragment
    val fork: JsCapabilityFragment
    val resume: JsCapabilityFragment
    val commission: JsCapabilityFragment
    val devops: JsCapabilityFragment
    val join: JsCapabilityFragment
    val dag: JsCapabilityFragment
    val fission: JsCapabilityFragment
    val horizon: JsCapabilityFragment
    val advisor: JsCapabilityFragment
    val ask: JsCapabilityFragment
    val park: JsCapabilityFragment
    val additionalCapabilities: fragment: JsCapabilityFragment -> JsCapability list
    val all: JsCapabilityFragment list
    val byCapability: Map<JsCapability, JsCapabilityFragment list>
    val isProjected: capabilities: Set<JsCapability> -> fragment: JsCapabilityFragment -> bool
