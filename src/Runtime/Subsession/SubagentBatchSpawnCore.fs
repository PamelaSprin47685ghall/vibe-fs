module Wanxiangshu.Runtime.SubagentBatchSpawnCore

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.HostAdapter
open Wanxiangshu.Runtime.HostAdapter
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Runtime.SubagentSpawn
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.SubagentIteratorStore
open Wanxiangshu.Runtime.Tooling.ToolOutputBatchToml

module HostAdapter = Wanxiangshu.Kernel.HostAdapter

let private getChildIDForSpawn
    (role: HostAdapter.SubagentRole)
    (registry: ChildAgentRegistry option)
    (host: Host)
    (scope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    : string option =
    match registry with
    | None -> None
    | Some reg ->
        let sessions = reg.GetChildSessions()

        if List.isEmpty sessions then
            None
        else
            let agentName =
                match role with
                | HostAdapter.Coder -> "coder"
                | HostAdapter.Inspector -> "inspector"
                | HostAdapter.Meditator -> "meditator"
                | HostAdapter.Browser -> "browser"

            let matches = sessions |> List.filter (fun (_, name) -> name = agentName)

            if List.isEmpty matches then
                None
            else
                Some(fst (List.last matches))

let private resolveSpawnedChildId
    (provenChildId: string option)
    (role: HostAdapter.SubagentRole)
    (getChildIDForSpawn:
        HostAdapter.SubagentRole
            -> ChildAgentRegistry option
            -> Host
            -> Wanxiangshu.Runtime.RuntimeScope.RuntimeScope
            -> string option)
    (host: Host)
    (registry: ChildAgentRegistry option)
    (scope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    : string option =
    match provenChildId with
    | Some cid -> Some cid
    | None ->
        match getChildIDForSpawn role registry host scope with
        | Some cid -> Some cid
        | None ->
            match host with
            | Opencode ->
                let r = scope.NextChildSessionId()
                Some("child-session-" + string r)
            | Mimocode -> None
            | Mux ->
                let r = scope.NextChildSessionId()
                Some("mux-task-" + string r)
            | Omp ->
                let r = scope.NextChildSessionId()
                Some("omp-session-" + string r)

let private wrapWithIterator
    (adapter: IHostAdapter)
    (host: Host)
    (registry: ChildAgentRegistry option)
    (scope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (toolName: string)
    (provenChildId: string option)
    (text: string)
    (role: HostAdapter.SubagentRole)
    (title: string)
    : JS.Promise<SubagentReport> =
    promise {
        let spawnedChildId =
            resolveSpawnedChildId provenChildId role getChildIDForSpawn host registry scope

        let parsed = Wanxiangshu.Runtime.SubagentReportParse.parseSubagentReportText text

        match spawnedChildId with
        | None -> return parsed
        | Some cid ->
            let roleStr =
                match role with
                | HostAdapter.Coder -> "coder"
                | HostAdapter.Inspector -> "inspector"
                | HostAdapter.Meditator -> "meditator"
                | HostAdapter.Browser -> "browser"

            match registry with
            | Some reg -> reg.RegisterChildAgent(cid, roleStr, None)
            | None -> ()

            let item =
                { childID = cid
                  agent = roleStr
                  host = host }

            let iter = storeSubagentIterator scope.SubagentIteratorStore "global" item
            let root = adapter.WorkspaceRoot
            let parentSid = adapter.SessionId

            if root <> "" && parentSid <> "" then
                do!
                    Wanxiangshu.Runtime.SubsessionEventWriter.appendSubagentSpawnedOrFail
                        root
                        parentSid
                        cid
                        roleStr
                        title

            return
                { parsed with
                    iterator = Some iter }
    }

let spawnOne
    (adapter: IHostAdapter)
    (host: Host)
    (scope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (registry: ChildAgentRegistry option)
    (toolName: string)
    (role: HostAdapter.SubagentRole)
    (title: string)
    (prompt: string)
    : JS.Promise<SubagentReport> =
    let request =
        { Role = role
          Title = title
          Prompt = prompt
          AllowedTools = [||] }

    promise {
        let! response = adapter.SpawnSubagent request

        match response with
        | Spawned(childID, text) ->
            let! res = wrapWithIterator adapter host registry scope toolName (Some childID) text role title
            return res
        | Success text ->
            let! res = wrapWithIterator adapter host registry scope toolName None text role title
            return res
        | Failure err ->
            return
                { iterator = None
                  summary = None
                  error = Some(ToolError err)
                  findings = []
                  relatedFiles = []
                  relatedCode = [] }
        | SubagentResponse.Aborted ->
            return
                { iterator = None
                  summary = None
                  error = Some FailureReason.Aborted
                  findings = []
                  relatedFiles = []
                  relatedCode = [] }
    }

let formatBatchReports (reports: SubagentReport list) : string =
    match BatchReport.create reports with
    | Some batch -> renderBatchReport batch
    | None -> ""
