namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

module SessionRecoveryHostSurface =
    type RecoveryHostHandle =
        { Journal: Wanxiangshu.Persistence.Journal.JournalHandle
          Scope: PluginRecoveryScope
          Host: SessionRecoveryHost
          PortOutcome: string
          ResumeCalls: System.Collections.Generic.List<bool> }

    val bootRecoveryHost: directory: string -> portOutcome: string -> Task<RecoveryHostHandle>

    val resumeAccepted: handle: RecoveryHostHandle -> sessionId: string -> physicalUserMessageId: string -> Task<obj>

    val seedAccepted: handle: RecoveryHostHandle -> sessionId: string -> physicalUserMessageId: string -> Task<unit>

    val seedProviderStarted:
        handle: RecoveryHostHandle ->
        sessionId: string ->
        physicalUserMessageId: string ->
        providerRun: string ->
            Task<unit>

    val finalizeCompleted:
        handle: RecoveryHostHandle ->
        sessionId: string ->
        physicalUserMessageId: string ->
        providerRun: string ->
            Task<obj>

    val signalCancelled: handle: RecoveryHostHandle -> sessionId: string -> physicalUserMessageId: string -> Task<obj>

    /// provider-attempt-recovery-023：宿主 session idle。扫描只考虑该 session 中 `Accepted` 且无
    /// `ProviderStarted` 的执行；返回值是完整可观察效果（port 调用 + manual）。
    val signalSessionQuiesced: handle: RecoveryHostHandle -> sessionId: string -> Task<obj>

    val disposeRecoveryHost: handle: RecoveryHostHandle -> unit
