namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

open System
open Fable.Core.JsInterop
open Wanxiangshu.Change
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider

/// Delegation-owned join wire surface. Inputs and outputs are plain JavaScript
/// data; completion and error unions remain inside the renderer owner.
[<RequireQualifiedAccess>]
module JoinSurface =
    let private text (value: obj) =
        if isNull value then "" else string value

    let private language (value: string) = ProviderLanguage.parse value

    let private role (value: obj) : Role option =
        match text value with
        | "Manager" -> Some Role.Manager
        | "Orchestrator" -> Some Role.Orchestrator
        | "Coder" -> Some Role.Coder
        | "Inspector" -> Some Role.Inspector
        | "DevOps" -> Some Role.DevOps
        | "Browser" -> Some Role.Browser
        | "Inquiry" -> Some Role.Inquiry
        | "Distiller" -> Some Role.Distiller
        | "Blogger" -> Some Role.Blogger
        | _ -> None

    let private requiredRunId (value: obj) : string option =
        let runId = text value
        if String.IsNullOrWhiteSpace runId then None else Some runId

    let private agentItem (value: obj) : JoinItem option =
        let agentId = text (value?agentId)
        let agentName = text (value?agentName)
        let kind = text (value?kind)
        let rawRole = value?role
        let canonicalRole = role rawRole
        let hasRole = not (isNull rawRole)

        match kind, canonicalRole, requiredRunId (value?runId) with
        | "failed", Some role, Some runId ->
            Some(
                AgentItem(
                    AgentFailedItem
                        { AgentId = agentId
                          ChildSessionId = None
                          RunId = runId
                          Role = Some role
                          Code = text (value?code)
                          Message = text (value?message) }
                )
            )
        | "completed", Some role, Some runId ->
            Some(
                AgentItem(
                    AgentCompletedItem
                        { AgentId = agentId
                          ChildSessionId = None
                          RunId = runId
                          Role = role
                          AuthorityRoot = None
                          ProviderRun = None
                          WorkRecord = text (value?workRecord)
                          Directory = None }
                )
            )
        | "abandoned", _, _ when hasRole && Option.isNone canonicalRole -> None
        | "abandoned", _, _ -> Some(AgentItem(AgentAbandonedItem(agentId, text (value?reason))))
        | _ -> None

    let private ptyItem (value: obj) : JoinItem option =
        let ptyId = text (value?ptyId)
        let outcome = text (value?outcome)
        let code = text (value?code)
        let message = text (value?message)

        match text (value?kind) with
        | "pty-failed" ->
            Some(
                PtyItem(
                    PtyFailed
                        { PtyId = ptyId
                          Outcome = outcome
                          Closed = true
                          Code = code
                          Message = message }
                )
            )
        | "pty-aborted" ->
            Some(
                PtyItem(
                    PtyAborted
                        { PtyId = ptyId
                          Outcome = outcome
                          Closed = true
                          Code = code
                          Message = message }
                )
            )
        | "pty-exited" ->
            Some(
                PtyItem(
                    PtyExited
                        { PtyId = ptyId
                          Outcome = outcome
                          Closed = true }
                )
            )
        | _ -> None

    let private itemOf (value: obj) : JoinItem option =
        if (text (value?kind)).StartsWith("pty-", StringComparison.Ordinal) then
            ptyItem value
        else
            agentItem value

    let private itemName (value: obj) =
        text (value?agentId), text (value?agentName)

    let renderBatch (languageName: string) (items: obj array) : string =
        if isNull items || items.Length = 0 then
            ""
        else
            let converted = items |> Array.map itemOf

            if converted |> Array.exists Option.isNone then
                ""
            else
                let converted = converted |> Array.choose id
                let names = items |> Array.map itemName |> Map.ofArray

                let terminals =
                    items
                    |> Array.choose (fun value ->
                        if (text (value?kind)).StartsWith("pty-", StringComparison.Ordinal) then
                            Some(text (value?ptyId), text (value?terminalLabel))
                        else
                            None)
                    |> Map.ofArray

                let resolveAgentName agentId =
                    match Map.tryFind agentId names with
                    | Some name when not (String.IsNullOrWhiteSpace name) -> name
                    | _ -> ""

                let resolveTerminalLabel ptyId =
                    match Map.tryFind ptyId terminals with
                    | Some label when not (String.IsNullOrWhiteSpace label) -> label
                    | _ -> ptyId

                JoinResultRenderer.renderJoinItemBatch
                    (language languageName)
                    resolveAgentName
                    (NonEmptyBatch.ofHeadTail converted[0] (converted |> Array.skip 1 |> Array.toList))
                    resolveTerminalLabel

    let renderInterrupted (languageName: string) (reason: string) : string =
        let interrupt =
            match reason with
            | "UserMessageArrived" -> JoinInterruptReason.UserMessageArrived
            | "DeadlineExpired" -> JoinInterruptReason.DeadlineExpired
            | _ -> JoinInterruptReason.OperatorAbort

        JoinResultRenderer.renderInterrupted (language languageName) interrupt

    let renderForkError (languageName: string) (error: string) : string =
        let forkError =
            match error with
            | "Cancelled" -> ForkError.Cancelled
            | "JoinInProgress" -> ForkError.JoinInProgress
            | "TimedOut" -> ForkError.TimedOut
            | "NotFound" -> ForkError.NotFound "unknown"
            | "Abandoned" -> ForkError.Abandoned("unknown", "abandoned")
            | "TerminalMaterializationFailed" -> ForkError.TerminalMaterializationFailed "unknown"
            | "Empty" -> ForkError.Empty
            | _ -> ForkError.NothingToJoin

        JoinResultRenderer.renderForkError (language languageName) forkError (fun _ -> "")

    let renderOrchestratorBatch (languageName: string) (verdictNames: string array) : string =
        let verdicts =
            verdictNames
            |> Array.map (fun name ->
                match name with
                | "Published" -> OrchestratorVerdict.Published(ManagerJobId.create "job", CommitHash.create "head")
                | "RejectedDirty" -> OrchestratorVerdict.RejectedDirty "dirty"
                | "IntegrationFailed" -> OrchestratorVerdict.IntegrationFailed(ManagerJobId.create "job", "failed")
                | _ -> OrchestratorVerdict.Empty)
            |> Array.toList

        match verdicts with
        | [] -> ""
        | head :: tail ->
            JoinResultRenderer.renderOrchestratorBatch (language languageName) (NonEmptyBatch.ofHeadTail head tail)
