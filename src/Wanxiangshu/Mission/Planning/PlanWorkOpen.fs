namespace Wanxiangshu.Mission.Planning

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host
open Wanxiangshu.Foundation.Identity

/// Entry point for the first prompt of a plan session (work-036): opens
/// the work and starts the first incumbency at S1. DevOps is bound by the
/// first successful ask, not by opening.
module PlanWorkOpen =

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    // Shared with PlanTools.executeAsk's single-flight binding so the entry
    // and the first ask resolve the identical DevOps identity (work-028).
    let internal deriveDevOpsId (workId: string) : string =
        "devops-" + (HostDigest.sha256Hex ("plan-devops:" + workId)).[..15]

    let private ensureOpened
        (root: string)
        (sessionId: string)
        (head: int64)
        (gitCommonDir: string)
        : Task<Result<unit, string>> =
        Wanxiangshu.Mission.Lifecycle.DurableWorkHost.ensureOpened
            gitCommonDir
            sessionId
            (sessionId + ":open:" + string head)
            root
            (Wanxiangshu.Mission.Lifecycle.DurableWorkHost.planSequence ())
            sessionId
            (float head)
        |> Result.map ignore
        |> Task.FromResult

    let private openAt
        (gitCommonDir: string)
        (root: string)
        (sessionId: string)
        (head: int64)
        : Task<Result<unit, string>> =
        let parentExists = existsSync gitCommonDir

        if String.IsNullOrWhiteSpace gitCommonDir || not parentExists then
            Task.FromResult(Error "the lifecycle journal directory cannot be read")
        else
            ensureOpened root sessionId head gitCommonDir

    let ensurePlanWorkOpened
        (gitCommonDir: string)
        (root: string)
        (sessionId: string)
        (observedHead: int64 option)
        : Task<Result<unit, string>> =
        if String.IsNullOrWhiteSpace sessionId then
            Task.FromResult(Error "Plan work session id cannot be empty")
        elif observedHead.IsNone then
            Task.FromResult(Error "opening cursor was not observed")
        else
            openAt gitCommonDir root sessionId observedHead.Value
