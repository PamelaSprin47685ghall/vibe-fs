namespace Wanxiangshu.OpenCode

[<RequireQualifiedAccess>]
type HookKey =
    | ChatMessage
    | ChatParams
    | MessagesTransform
    | SystemTransform
    | Config
    | SessionCompacting
    | CompactionAutoContinue
    | ToolDefinition
    | ToolBefore
    | ToolAfter
    | Event
    | Dispose
    | CommandBefore

[<RequireQualifiedAccess>]
type HookCriticality =
    | Security
    | Workflow
    | Invariant
    | Degradable
    | AuditOnly

[<RequireQualifiedAccess>]
type HookContext =
    | ChatRequest
    | MessageTransform
    | SystemPrompt
    | HostConfiguration
    | SessionCompaction
    | ToolDefinition
    | ToolExecution
    | HostEvent
    | PluginLifecycle
    | CommandExecution

[<RequireQualifiedAccess>]
type HookEffect =
    | ObserveIdentity
    | AdmitChatExecution
    | RouteModel
    | ValidateModelParameters
    | TransformMessages
    | TransformSystemPrompt
    | EnforceHostConfiguration
    | ObserveCompaction
    | DenyAutoContinue
    | DefineToolSchema
    | AdmitToolExecution
    | MutateToolResult
    | ObserveCasebook
    | ObserveHostEvent
    | DisposeOwnedResources
    | AdmitExplicitResume

[<RequireQualifiedAccess>]
type HookRetryPermission = | RetryForbidden

[<RequireQualifiedAccess>]
type HookCapacityOwner =
    | NoCapacity
    | ManagedChatAdmission

[<RequireQualifiedAccess>]
type HookFailureDisposition =
    | TypedPolicyFailClosed
    | BestEffortDiagnostic

[<RequireQualifiedAccess>]
type IdentityPermission =
    | NoIdentityAccess
    | ObserveIdentity

[<RequireQualifiedAccess>]
type AdmissionPermission =
    | NoAdmissionAccess
    | OwnedAdmissionGate

[<RequireQualifiedAccess>]
type OptionalHookEffect = | CasebookObservation

[<RequireQualifiedAccess>]
type OptionalEffectOutcome =
    | Observed
    | Failed

type HookMetadata =
    { HostKey: string
      DiagnosticOperation: string
      Criticality: HookCriticality
      Context: HookContext
      Effects: HookEffect list
      Retry: HookRetryPermission
      Capacity: HookCapacityOwner
      Failure: HookFailureDisposition
      Identity: IdentityPermission
      Admission: AdmissionPermission }

module HookPolicy =
    val metadata: HookKey -> HookMetadata
    val accepts: criticality: HookCriticality -> disposition: HookFailureDisposition -> bool
    val validate: row: HookMetadata -> HookMetadata

    val observeOptional:
        emitDiagnostic: (string -> (string * string) list -> unit) ->
        effect: OptionalHookEffect ->
        action: (unit -> unit) ->
            OptionalEffectOutcome
