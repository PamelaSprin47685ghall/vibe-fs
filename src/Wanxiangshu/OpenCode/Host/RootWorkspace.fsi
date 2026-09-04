namespace Wanxiangshu.OpenCode

type IRootWorkspaceReader =
    abstract TryRead: unit -> string option

type IRootWorkspaceBinder =
    abstract TryBind: workspaceDirectory: string option -> bool

module RootWorkspaceDirectory =
    val select:
        pathExists: (string -> bool) -> rootWorkspace: IRootWorkspaceReader -> candidate: string option -> string option
