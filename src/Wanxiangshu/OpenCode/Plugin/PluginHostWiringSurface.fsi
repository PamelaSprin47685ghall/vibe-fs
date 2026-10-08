namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

module PluginHostWiringSurface =
    val createBoot: input: obj -> Task<obj>
    val bootHasJournal: boot: obj -> bool
    val disposeBoot: boot: obj -> Task
    val finalizeDraft: workspaceRoot: string -> store: obj -> delegateSessionId: string -> owner: obj -> Task<obj>

    val finalizeDraftWithBoot:
        workspaceRoot: string -> store: obj -> delegateSessionId: string -> boot: obj -> Task<obj>
