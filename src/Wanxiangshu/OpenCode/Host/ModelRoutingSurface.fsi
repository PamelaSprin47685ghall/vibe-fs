namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

module ModelRoutingSurface =
    val initialize: unit -> Task

    val acquireSharedExecutionAdmission:
        sessionId: string -> physicalUserMessageId: string -> effectiveAgent: string -> Task<obj>

    val sharedExecutionAdmissionTarget: token: obj -> obj
    val sharedCapacitySnapshot: unit -> obj
    val commitSharedExecutionAdmission: token: obj -> observed: obj -> obj
    val releaseSharedExecutionAdmissionBeforeProvider: token: obj -> observed: obj -> obj
    val releasePhysical: sessionId: string -> physicalUserMessageId: string -> obj
    val bootstrapAndLoadAt: path: string -> template: string -> Task<obj>
    val invokeScheduler: scheduler: obj -> role: string -> running: obj -> previous: obj -> obj
    val createRuntime: scheduler: obj -> obj

    val acquireExecutionAdmission:
        runtime: obj -> sessionId: string -> physicalUserMessageId: string -> effectiveAgent: string -> Task<obj>

    val beginExecutionAdmission:
        runtime: obj -> sessionId: string -> physicalUserMessageId: string -> effectiveAgent: string -> Task<obj>

    val awaitQueuedExecutionAdmission: queueToken: obj -> Task<obj>
    val executionAdmissionTarget: runtime: obj -> token: obj -> obj
    val commitExecutionAdmission: runtime: obj -> token: obj -> observed: obj -> obj
    val releaseExecutionAdmissionBeforeProvider: runtime: obj -> token: obj -> observed: obj -> obj
    val executionAdmissionLifecycle: runtime: obj -> token: obj -> obj
    val tryReserveManaged: runtime: obj -> sessionId: string -> agent: string -> obj
    val tryLease: runtime: obj -> sessionId: string -> physicalUserMessageId: string -> agent: string -> obj
    val releasePhysicalExecution: runtime: obj -> sessionId: string -> physicalUserMessageId: string -> obj
    val cancelPendingExecution: runtime: obj -> sessionId: string -> obj
    val bindCapacityChild: runtime: obj -> parentSessionId: string -> childSessionId: string -> unit
    val bindCapacityCompanion: runtime: obj -> ownerSessionId: string -> bloggerSessionId: string -> unit
    val dropCapacityLineage: runtime: obj -> sessionId: string -> unit

    val enterProviderStep:
        runtime: obj -> sessionId: string -> physicalUserMessageId: string -> visibleProviderRuns: string array -> Task

    val endProviderStep:
        runtime: obj -> sessionId: string -> physicalUserMessageId: string -> providerRun: string -> unit

    val suppressProviderStep: runtime: obj -> sessionId: string -> physicalUserMessageId: string -> unit
    val snapshotOccupied: runtime: obj -> obj array
    val capacitySnapshot: runtime: obj -> obj
    val reconcileCapacityEvidence: evidence: obj -> obj
    val pendingCount: runtime: obj -> int
    val admissionSnapshot: routingRuntime: obj -> sessionId: string -> physicalUserMessageId: string -> obj
    val pendingBound: runtime: obj -> int
    val pendingContractVersion: runtime: obj -> int
    val createSdkClientPort: client: obj -> obj
    val sendPrompt: port: obj -> sessionId: string -> text: string -> options: obj -> Task<obj>
