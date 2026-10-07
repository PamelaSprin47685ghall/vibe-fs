namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

module PluginHostWiringSurface =
    val finalizeDraft: workspaceRoot: string -> store: obj -> delegateSessionId: string -> owner: obj -> Task<obj>
