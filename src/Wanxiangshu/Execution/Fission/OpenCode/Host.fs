namespace Wanxiangshu.Execution.Fission.OpenCode

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Interaction.Admission
open Fable.Core.JsInterop

[<RequireQualifiedAccess>]
module FissionHostRequestProjection =

    let private requireOutputMessage output =
        let message = if isNull output then null else output?message

        if isNull message then
            invalidOp "INTRA-PARTICIPANT-PARALLELISM-013: fission request projection has no mutable output.message"

        message

    let private ensureToolsObject (message: obj) : obj =
        if isNull message?tools then createObj [] else message?tools

    let private projectVisibility hasPhysicalParent output =
        if FissionRequestProjection.apply hasPhysicalParent then
            let message = requireOutputMessage output
            let tools = ensureToolsObject message
            tools?fission <- box false
            message?tools <- tools

    let projectExternalManaged
        (hasPhysicalParent: SessionId -> bool)
        (intent: ChatAdmissionIntent.Decision)
        (output: obj)
        =
        match intent with
        | ChatAdmissionIntent.Decision.ExternalRootIntent evidence ->
            projectVisibility (hasPhysicalParent evidence.Key.SessionId) output
        | ChatAdmissionIntent.Decision.ActiveHumanContinuationIntent evidence ->
            projectVisibility (hasPhysicalParent evidence.Key.SessionId) output
        | ChatAdmissionIntent.Decision.NoManagedExecution _
        | ChatAdmissionIntent.Decision.PendingPromptIntent _
        | ChatAdmissionIntent.Decision.AcceptedInputIntent _
        | ChatAdmissionIntent.Decision.HostInternal _
        | ChatAdmissionIntent.Decision.Reject _ -> ()

    let projectPendingManaged
        (hasPhysicalParent: SessionId -> bool)
        (intent: ChatAdmissionIntent.Decision)
        (output: obj)
        =
        match intent with
        | ChatAdmissionIntent.Decision.PendingPromptIntent evidence ->
            projectVisibility (hasPhysicalParent evidence.Key.SessionId) output
        | ChatAdmissionIntent.Decision.AcceptedInputIntent evidence ->
            projectVisibility (hasPhysicalParent evidence.SessionId) output
        | ChatAdmissionIntent.Decision.ExternalRootIntent _
        | ChatAdmissionIntent.Decision.ActiveHumanContinuationIntent _
        | ChatAdmissionIntent.Decision.NoManagedExecution _
        | ChatAdmissionIntent.Decision.HostInternal _
        | ChatAdmissionIntent.Decision.Reject _ -> ()
