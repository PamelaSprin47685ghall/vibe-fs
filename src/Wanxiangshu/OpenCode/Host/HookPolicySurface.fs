namespace Wanxiangshu.OpenCode

open Fable.Core.JsInterop

module HookPolicySurface =

    let private criticalityName =
        function
        | HookCriticality.Security -> "Security"
        | HookCriticality.Workflow -> "Workflow"
        | HookCriticality.Invariant -> "Invariant"
        | HookCriticality.Degradable -> "Degradable"
        | HookCriticality.AuditOnly -> "AuditOnly"

    let private failureName =
        function
        | HookFailureDisposition.TypedPolicyFailClosed -> "TypedPolicyFailClosed"
        | HookFailureDisposition.BestEffortDiagnostic -> "BestEffortDiagnostic"

    let private identityName =
        function
        | IdentityPermission.NoIdentityAccess -> "NoIdentityAccess"
        | IdentityPermission.ObserveIdentity -> "ObserveIdentity"

    let private admissionName =
        function
        | AdmissionPermission.NoAdmissionAccess -> "NoAdmissionAccess"
        | AdmissionPermission.OwnedAdmissionGate -> "OwnedAdmissionGate"

    let private keys =
        [ HookKey.ChatMessage
          HookKey.ChatParams
          HookKey.MessagesTransform
          HookKey.SystemTransform
          HookKey.Config
          HookKey.SessionCompacting
          HookKey.CompactionAutoContinue
          HookKey.ToolDefinition
          HookKey.ToolBefore
          HookKey.ToolAfter
          HookKey.Event
          HookKey.Dispose
          HookKey.CommandBefore ]

    let rows () : obj array =
        keys
        |> List.map (fun key ->
            let row = HookPolicy.metadata key |> HookPolicy.validate

            createObj
                [ "key", box row.HostKey
                  "criticality", box (criticalityName row.Criticality)
                  "failure", box (failureName row.Failure)
                  "identity", box (identityName row.Identity)
                  "admission", box (admissionName row.Admission) ])
        |> List.toArray

    let acceptsPolicy criticality disposition =
        let typedCriticality =
            match criticality with
            | "Security" -> HookCriticality.Security
            | "Workflow" -> HookCriticality.Workflow
            | "Invariant" -> HookCriticality.Invariant
            | "Degradable" -> HookCriticality.Degradable
            | "AuditOnly" -> HookCriticality.AuditOnly
            | other -> invalidArg "criticality" $"unknown Hook criticality '{other}'"

        let typedDisposition =
            match disposition with
            | "TypedPolicyFailClosed" -> HookFailureDisposition.TypedPolicyFailClosed
            | "BestEffortDiagnostic" -> HookFailureDisposition.BestEffortDiagnostic
            | other -> invalidArg "disposition" $"unknown Hook failure disposition '{other}'"

        HookPolicy.accepts typedCriticality typedDisposition

    let runOptionalCasebookEffect (criticalResult: obj) (effect: unit -> unit) : obj =
        HookPolicy.observeOptional Diagnostic.emit OptionalHookEffect.CasebookObservation effect
        |> ignore

        criticalResult
