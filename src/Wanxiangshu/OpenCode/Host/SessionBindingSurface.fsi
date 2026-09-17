namespace Wanxiangshu.OpenCode

module SessionBindingSurface =
    val bind: parentId: string -> childId: string -> agent: string -> unit
    val bindChild: parentId: string -> childId: string -> agent: string -> obj
    val bindDevOpsModel: sessionId: string -> model: obj -> unit
    val verifyDevOpsModel: sessionId: string -> model: obj -> unit
    val tryParent: sessionId: string -> string
    val observeUserFacingAgent: sessionId: string -> agent: string -> unit
    val observeHostAuxiliaryChild: sessionId: string -> unit
    val isUnboundHostAuxiliaryChild: sessionId: string -> bool
    val tryAgent: sessionId: string -> string
    val prepareManaged: sessionId: string -> agent: string -> overrideBinding: bool -> model: obj -> obj
    val prepareUserFacing: sessionId: string -> agent: string -> overrideBinding: bool -> model: obj -> obj

    val acceptPromptExecution:
        sessionId: string -> promptKey: string -> physicalUserMessageId: string -> agent: string -> model: obj -> unit

    val acceptExternalExecution:
        sessionId: string -> physicalUserMessageId: string -> agent: string -> model: obj -> unit

    val beginProviderAttempt: sessionId: string -> physicalUserMessageId: string -> promptKey: string -> obj
    val validateObservedProvider: sessionId: string -> agent: string -> model: obj -> obj
    val exactExecutionBindingCount: sessionId: string -> physicalUserMessageId: string -> int
    val drop: sessionId: string -> unit
