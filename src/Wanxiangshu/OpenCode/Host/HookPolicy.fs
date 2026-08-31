namespace Wanxiangshu.OpenCode

[<RequireQualifiedAccess>]
/// DSL-class: Vocabulary
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
/// DSL-class: Vocabulary
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
/// DSL-class: Vocabulary
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

    let metadata =
        function
        | HookKey.ChatMessage ->
            { HostKey = "chat.message"
              DiagnosticOperation = "plugin-hook-chat-message-failed"
              Criticality = HookCriticality.Security
              Context = HookContext.ChatRequest
              Effects =
                [ HookEffect.ObserveIdentity
                  HookEffect.AdmitChatExecution
                  HookEffect.RouteModel ]
              Retry = HookRetryPermission.RetryForbidden
              Capacity = HookCapacityOwner.ManagedChatAdmission
              Failure = HookFailureDisposition.TypedPolicyFailClosed
              Identity = IdentityPermission.ObserveIdentity
              Admission = AdmissionPermission.OwnedAdmissionGate }
        | HookKey.ChatParams ->
            { HostKey = "chat.params"
              DiagnosticOperation = "plugin-hook-chat-params-failed"
              Criticality = HookCriticality.Security
              Context = HookContext.ChatRequest
              Effects = [ HookEffect.ValidateModelParameters ]
              Retry = HookRetryPermission.RetryForbidden
              Capacity = HookCapacityOwner.NoCapacity
              Failure = HookFailureDisposition.TypedPolicyFailClosed
              Identity = IdentityPermission.NoIdentityAccess
              Admission = AdmissionPermission.NoAdmissionAccess }
        | HookKey.MessagesTransform ->
            { HostKey = "experimental.chat.messages.transform"
              DiagnosticOperation = "plugin-hook-messages-transform-failed"
              Criticality = HookCriticality.Workflow
              Context = HookContext.MessageTransform
              Effects = [ HookEffect.TransformMessages ]
              Retry = HookRetryPermission.RetryForbidden
              Capacity = HookCapacityOwner.NoCapacity
              Failure = HookFailureDisposition.TypedPolicyFailClosed
              Identity = IdentityPermission.ObserveIdentity
              Admission = AdmissionPermission.NoAdmissionAccess }
        | HookKey.SystemTransform ->
            { HostKey = "experimental.chat.system.transform"
              DiagnosticOperation = "plugin-hook-system-transform-failed"
              Criticality = HookCriticality.Workflow
              Context = HookContext.SystemPrompt
              Effects = [ HookEffect.TransformSystemPrompt ]
              Retry = HookRetryPermission.RetryForbidden
              Capacity = HookCapacityOwner.NoCapacity
              Failure = HookFailureDisposition.TypedPolicyFailClosed
              Identity = IdentityPermission.ObserveIdentity
              Admission = AdmissionPermission.NoAdmissionAccess }
        | HookKey.Config ->
            { HostKey = "config"
              DiagnosticOperation = "plugin-hook-config-failed"
              Criticality = HookCriticality.Invariant
              Context = HookContext.HostConfiguration
              Effects = [ HookEffect.EnforceHostConfiguration ]
              Retry = HookRetryPermission.RetryForbidden
              Capacity = HookCapacityOwner.NoCapacity
              Failure = HookFailureDisposition.TypedPolicyFailClosed
              Identity = IdentityPermission.NoIdentityAccess
              Admission = AdmissionPermission.NoAdmissionAccess }
        | HookKey.SessionCompacting ->
            { HostKey = "experimental.session.compacting"
              DiagnosticOperation = "plugin-hook-session-compacting-failed"
              Criticality = HookCriticality.Invariant
              Context = HookContext.SessionCompaction
              Effects = [ HookEffect.ObserveCompaction ]
              Retry = HookRetryPermission.RetryForbidden
              Capacity = HookCapacityOwner.NoCapacity
              Failure = HookFailureDisposition.TypedPolicyFailClosed
              Identity = IdentityPermission.ObserveIdentity
              Admission = AdmissionPermission.NoAdmissionAccess }
        | HookKey.CompactionAutoContinue ->
            { HostKey = "experimental.compaction.autocontinue"
              DiagnosticOperation = "plugin-hook-compaction-autocontinue-failed"
              Criticality = HookCriticality.Security
              Context = HookContext.SessionCompaction
              Effects = [ HookEffect.DenyAutoContinue ]
              Retry = HookRetryPermission.RetryForbidden
              Capacity = HookCapacityOwner.NoCapacity
              Failure = HookFailureDisposition.TypedPolicyFailClosed
              Identity = IdentityPermission.ObserveIdentity
              Admission = AdmissionPermission.OwnedAdmissionGate }
        | HookKey.ToolDefinition ->
            { HostKey = "tool.definition"
              DiagnosticOperation = "plugin-hook-tool-definition-failed"
              Criticality = HookCriticality.Invariant
              Context = HookContext.ToolDefinition
              Effects = [ HookEffect.DefineToolSchema ]
              Retry = HookRetryPermission.RetryForbidden
              Capacity = HookCapacityOwner.NoCapacity
              Failure = HookFailureDisposition.TypedPolicyFailClosed
              Identity = IdentityPermission.NoIdentityAccess
              Admission = AdmissionPermission.NoAdmissionAccess }
        | HookKey.ToolBefore ->
            { HostKey = "tool.execute.before"
              DiagnosticOperation = "plugin-hook-tool-before-failed"
              Criticality = HookCriticality.Security
              Context = HookContext.ToolExecution
              Effects = [ HookEffect.ObserveIdentity; HookEffect.AdmitToolExecution ]
              Retry = HookRetryPermission.RetryForbidden
              Capacity = HookCapacityOwner.NoCapacity
              Failure = HookFailureDisposition.TypedPolicyFailClosed
              Identity = IdentityPermission.ObserveIdentity
              Admission = AdmissionPermission.OwnedAdmissionGate }
        | HookKey.ToolAfter ->
            { HostKey = "tool.execute.after"
              DiagnosticOperation = "plugin-hook-tool-after-failed"
              Criticality = HookCriticality.Workflow
              Context = HookContext.ToolExecution
              Effects = [ HookEffect.MutateToolResult; HookEffect.ObserveCasebook ]
              Retry = HookRetryPermission.RetryForbidden
              Capacity = HookCapacityOwner.NoCapacity
              Failure = HookFailureDisposition.TypedPolicyFailClosed
              Identity = IdentityPermission.ObserveIdentity
              Admission = AdmissionPermission.NoAdmissionAccess }
        | HookKey.Event ->
            { HostKey = "event"
              DiagnosticOperation = "plugin-hook-event-failed"
              Criticality = HookCriticality.Workflow
              Context = HookContext.HostEvent
              Effects = [ HookEffect.ObserveHostEvent ]
              Retry = HookRetryPermission.RetryForbidden
              Capacity = HookCapacityOwner.NoCapacity
              Failure = HookFailureDisposition.TypedPolicyFailClosed
              Identity = IdentityPermission.ObserveIdentity
              Admission = AdmissionPermission.NoAdmissionAccess }
        | HookKey.Dispose ->
            { HostKey = "dispose"
              DiagnosticOperation = "plugin-hook-dispose-failed"
              Criticality = HookCriticality.Invariant
              Context = HookContext.PluginLifecycle
              Effects = [ HookEffect.DisposeOwnedResources ]
              Retry = HookRetryPermission.RetryForbidden
              Capacity = HookCapacityOwner.NoCapacity
              Failure = HookFailureDisposition.TypedPolicyFailClosed
              Identity = IdentityPermission.NoIdentityAccess
              Admission = AdmissionPermission.NoAdmissionAccess }
        | HookKey.CommandBefore ->
            { HostKey = "command.execute.before"
              DiagnosticOperation = "plugin-hook-command-before-failed"
              Criticality = HookCriticality.Security
              Context = HookContext.CommandExecution
              Effects = [ HookEffect.ObserveIdentity; HookEffect.AdmitExplicitResume ]
              Retry = HookRetryPermission.RetryForbidden
              Capacity = HookCapacityOwner.NoCapacity
              Failure = HookFailureDisposition.TypedPolicyFailClosed
              Identity = IdentityPermission.ObserveIdentity
              Admission = AdmissionPermission.OwnedAdmissionGate }

    let accepts criticality disposition =
        match criticality, disposition with
        | HookCriticality.Security, HookFailureDisposition.BestEffortDiagnostic
        | HookCriticality.Workflow, HookFailureDisposition.BestEffortDiagnostic
        | HookCriticality.Invariant, HookFailureDisposition.BestEffortDiagnostic -> false
        | _ -> true

    let validate row =
        if not (accepts row.Criticality row.Failure) then
            invalidOp $"Hook '{row.HostKey}' cannot downgrade its critical failure disposition"

        row

    let observeOptional emitDiagnostic effect action =
        let operation =
            match effect with
            | OptionalHookEffect.CasebookObservation -> "plugin-hook-casebook-observation-failed"

        try
            action ()
            OptionalEffectOutcome.Observed
        with ex ->
            emitDiagnostic operation [ "result", ex.Message ]
            OptionalEffectOutcome.Failed
