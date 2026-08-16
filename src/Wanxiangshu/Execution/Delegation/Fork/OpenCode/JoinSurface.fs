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

    let private language (value: string) =
        match value.ToLowerInvariant() with
        | "chinese" -> ProviderLanguage.SimplifiedChinese
        | _ -> ProviderLanguage.English

    let private role (value: obj) =
        match text value with
        | "Manager" -> Role.Manager
        | "Inspector" -> Role.Inspector
        | "DevOps" -> Role.DevOps
        | "Browser" -> Role.Browser
        | "Inquiry" -> Role.Inquiry
        | "Distiller" -> Role.Distiller
        | _ -> Role.Coder

    let private agentItem (value: obj) : JoinItem =
        let agentId = text (value?agentId)
        let runId =
            let value = text (value?runId)
            if String.IsNullOrWhiteSpace value then "run-1" else value
        let agentName = text (value?agentName)
        let canonicalRole = role (value?role)

        match text (value?kind) with
        | "failed" ->
            AgentItem(
                AgentFailedItem
                    { AgentId = agentId
                      ChildSessionId = None
                      RunId = runId
                      Role = Some canonicalRole
                      Code = text (value?code)
                      Message = text (value?message) }
            )
        | "abandoned" -> AgentItem(AgentAbandonedItem(agentId, text (value?reason)))
        | _ ->
            AgentItem(
                AgentCompletedItem
                    { AgentId = agentId
                      ChildSessionId = None
                      RunId = runId
                      Role = canonicalRole
                      AuthorityRoot = None
                      ProviderRun = None
                      WorkRecord = text (value?workRecord)
                      Directory = None }
            )

    let private ptyItem (value: obj) : JoinItem =
        let ptyId = text (value?ptyId)
        let outcome = text (value?outcome)
        let code = text (value?code)
        let message = text (value?message)

        match text (value?kind) with
        | "pty-failed" ->
            PtyItem(
                PtyFailed
                    { PtyId = ptyId
                      Outcome = outcome
                      Closed = true
                      Code = code
                      Message = message }
            )
        | "pty-aborted" ->
            PtyItem(
                PtyAborted
                    { PtyId = ptyId
                      Outcome = outcome
                      Closed = true
                      Code = code
                      Message = message }
            )
        | _ -> PtyItem(PtyExited { PtyId = ptyId; Outcome = outcome; Closed = true })

    let private itemOf (value: obj) : JoinItem =
        if (text (value?kind)).StartsWith("pty-", StringComparison.Ordinal) then ptyItem value else agentItem value

    let private itemName (value: obj) = text (value?agentId), text (value?agentName)

    let renderBatch (languageName: string) (items: obj array) : string =
        if isNull items || items.Length = 0 then
            ""
        else
            let converted = items |> Array.map itemOf
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
                | "NeedsReview" -> OrchestratorVerdict.NeedsReview(ManagerJobId.create "job", "review")
                | "IntegrationFailed" -> OrchestratorVerdict.IntegrationFailed(ManagerJobId.create "job", "failed")
                | _ -> OrchestratorVerdict.Empty)
            |> Array.toList

        match verdicts with
        | [] -> ""
        | head :: tail ->
            JoinResultRenderer.renderOrchestratorBatch
                (language languageName)
                (NonEmptyBatch.ofHeadTail head tail)
