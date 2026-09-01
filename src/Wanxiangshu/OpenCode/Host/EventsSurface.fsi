namespace Wanxiangshu.OpenCode

module EventsSurface =
    val create: unit -> obj
    val notify: port: obj -> sessionId: string -> kind: string -> providerRun: string -> text: string -> bool

    val notifyForAuthority:
        port: obj -> sessionId: string -> kind: string -> authorityRoot: string -> text: string -> bool

    val subscribe: port: obj -> listener: (obj -> obj -> unit) -> obj
    val subscribeFuture: port: obj -> listener: (obj -> obj -> unit) -> obj
    val dispose: subscription: obj -> unit

    val notifyCompleted:
        port: obj -> sessionId: string -> terminalText: string -> formalText: string -> roleLabel: string -> bool

    val acquireSharedForWorkspace: workspace: string -> obj
    val releaseSharedForWorkspace: workspace: string -> port: obj -> unit
