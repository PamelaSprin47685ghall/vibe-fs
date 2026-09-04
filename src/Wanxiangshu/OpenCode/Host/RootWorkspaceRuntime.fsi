namespace Wanxiangshu.OpenCode

type RootWorkspaceRuntime =
    new: unit -> RootWorkspaceRuntime
    member Binder: IRootWorkspaceBinder
    member Reader: IRootWorkspaceReader

module RootWorkspaceProcess =
    val local: unit -> RootWorkspaceRuntime
