namespace Wanxiangshu.Mission.Planning

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

/// Entry point for the first prompt of a plan session (planning-020): opens
/// the work, binds the fixed DevOps and starts the first incumbency at S1.
/// Idempotent for a work that already exists; fails with zero writes when the
/// store cannot be read or an append fails.
module PlanWorkOpen =

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    // Shared with PlanTools.executeAsk's single-flight binding so the entry
    // and the first ask resolve the identical DevOps identity (planning-010).
    let internal deriveDevOpsId (workId: string) : string =
        "devops-" + (HostDigest.sha256Hex ("plan-devops:" + workId)).[..15]

    let ensurePlanWorkOpened
        (store: IEventStore)
        (root: string)
        (sessionId: string)
        (observedHead: int64 option)
        : Task<Result<unit, string>> =
        if String.IsNullOrWhiteSpace sessionId then
            Task.FromResult(Error "Plan work session id cannot be empty")
        elif observedHead.IsNone then
            Task.FromResult(Error "opening cursor was not observed")
        else
            let stamped = emitJsExpr store "$0 && typeof $0.gitCommonDir === 'string' ? $0.gitCommonDir : ''"
            let stampedDir = if isNull stamped then "" else string stamped
            let derived =
                if not (String.IsNullOrWhiteSpace stampedDir) then
                    stampedDir
                elif root.EndsWith("/wanxiangshu") then
                    root.Substring(0, root.Length - "/wanxiangshu".Length)
                else
                    root
            let parentExists = existsSync derived
            if String.IsNullOrWhiteSpace derived || not parentExists then
                Task.FromResult(Error "the plan store cannot be read")
            else
            let gitCommonDir = derived
            let head = observedHead.Value
            let snapshot = Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspect gitCommonDir sessionId
            let opened = snapshot?opened
            if unbox<bool> opened then
                Task.FromResult(Ok())
            else
                let requestId = sessionId + ":open:" + string head
                let command =
                    Wanxiangshu.Mission.Lifecycle.DurableWorkHost.command
                        "Open"
                        [ "root", box root
                          "sequence", Wanxiangshu.Mission.Lifecycle.DurableWorkHost.planSequence ()
                          "ownerId", box sessionId
                          "openingCursor", box (float head) ]
                let receipt =
                    Wanxiangshu.Mission.Lifecycle.DurableWorkHost.execute gitCommonDir sessionId requestId command
                if receipt.Status = "Committed" || receipt.Status = "Replayed" then
                    Task.FromResult(Ok())
                else
                    Task.FromResult(Error(if receipt.Reason = "" then receipt.Status else receipt.Reason))
