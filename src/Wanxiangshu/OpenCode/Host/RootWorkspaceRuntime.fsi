namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.HostSession

type RootWorkspaceRuntime =
    new: unit -> RootWorkspaceRuntime
    member Binder: IRootWorkspaceBinder
    member Reader: IRootWorkspaceReader

module RootWorkspaceProcess =
    val local: unit -> RootWorkspaceRuntime
