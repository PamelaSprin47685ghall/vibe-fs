namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

module PluginLifecycleSurface =
    val create: input: obj -> Task<obj>
    val hooks: runtime: obj -> obj
    val invokeEngineer: runtime: obj -> ownerSessionId: string -> charge: string -> Task<obj>
    val attachedEngineer: runtime: obj -> ownerSessionId: string -> string
    val awaitAssignmentReady: runtime: obj -> delegateSessionId: string -> Task<bool>

    /// Consumes the original completed draft; this is not a read-only peek.
    val takeEngineerDraft: runtime: obj -> ownerSessionId: string -> obj
