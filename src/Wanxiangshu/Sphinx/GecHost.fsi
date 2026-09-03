namespace Wanxiangshu.Sphinx

module GecHost =
    [<RequireQualifiedAccess>]
    type HostFault =
        | MissingWorkId
        | MissingAttempt
        | MissingSnapshotHash
        | MissingParentSession
        | MissingChildSession
        | MissingEvents
        | DepthExceeded of depth: int
        | ParentChainBreak of detail: string

    val code: fault: HostFault -> string
    val message: fault: HostFault -> string
    val planOpenCodeDispatch: input: obj -> obj
    val planOpenCodeRetry: input: obj -> obj
    val abortOpenCodeWork: input: obj -> obj
    val drainOpenCodeHost: input: obj -> obj
    val foldHostEvents: input: obj -> obj
    val methods: (string * obj) list
