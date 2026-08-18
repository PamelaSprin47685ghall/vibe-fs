namespace Wanxiangshu.OpenCode.Host

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Requirement.Grounding

module RequirementGroundingRepositorySurface =

    type private RuntimeHandle(handle: JournalHandle, workspace: string, sessionId: string) =
        member _.Handle = handle
        member _.Workspace = workspace
        member _.SessionId = sessionId

    let private runtimeOf value = unbox<RuntimeHandle> value

    let private boot workspace sessionId =
        task {
            let! opened =
                JournalSurface.boot workspace "requirement-grounding-js-surface" 0 (DateTimeOffset.UtcNow.ToString("O"))

            if isNull opened?ok || not (unbox<bool> opened?ok) then
                return
                    Error(
                        if isNull opened?error then
                            "journal boot failed"
                        else
                            string opened?error
                    )
            else
                let handle = unbox<JournalHandle> opened?journal
                return Ok(RuntimeHandle(handle, workspace, sessionId))
        }

    let dispose runtime =
        JournalSurface.dispose (runtimeOf runtime).Handle

    let private summary (runtime: RuntimeHandle) (outcome: JsToolWorkflow.JsToolOutcome) =
        let handle = runtime.Handle
        let journal = handle.Journal
        let session = SessionId.create runtime.SessionId

        let caseName, failureCode, created =
            match outcome with
            | JsToolWorkflow.JsToolOutcome.Succeeded(_, _, created) -> "Succeeded", null, created |> List.toArray
            | JsToolWorkflow.JsToolOutcome.Failed failure -> "Failed", box (JsFailure.code failure), [||]

        box
            {| runtime = (runtime :> obj)
               caseName = caseName
               failureCode = failureCode
               pendingPackages =
                RequirementGroundingRuntime.pending journal session
                |> List.map _.PackageName
                |> List.toArray
               created = created |}

    let runFirstAttempt workspace sessionId program : Task<obj> =
        task {
            match! boot workspace sessionId with
            | Error error -> return raise (InvalidOperationException error)
            | Ok runtime ->
                match JsGeneratorSurface.typedRole "Coder" "en" with
                | None -> return raise (InvalidOperationException "Coder js surface unavailable")
                | Some surface ->
                    let admission paths =
                        RequirementGroundingGate.programAdmission
                            (Some runtime.Handle.Journal)
                            runtime.Workspace
                            runtime.SessionId
                            paths

                    let! outcome =
                        JsToolWorkflow.runWithMutationAdmission
                            runtime.Workspace
                            surface.BaseClassSource
                            program
                            2000
                            (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 60000L)
                            (1 <<< 20)
                            None
                            admission

                    return summary runtime outcome
        }

    let compareNativeAndProgramDecision workspace sessionId path : Task<obj> =
        task {
            match! boot workspace sessionId with
            | Error error -> return raise (InvalidOperationException error)
            | Ok runtime ->
                let journal = runtime.Handle.Journal
                let session = SessionId.create sessionId
                let! nativeResult = RequirementGroundingRuntime.requestPaths journal workspace session [ path ]

                match nativeResult with
                | Error error -> return raise (InvalidOperationException error)
                | Ok nativeDecision ->
                    let programPackages =
                        GroundingCatalog.snapshotsForPaths workspace [ path ] |> List.map _.PackageName

                    let! programDecision =
                        RequirementGroundingGate.programAdmission (Some journal) workspace sessionId [ path ]

                    return
                        box
                            {| runtime = (runtime :> obj)
                               nativePackages = nativeDecision.Packages |> List.toArray
                               programPackages = programPackages |> List.toArray
                               nativeAllowed = not nativeDecision.NeedsGrounding
                               programAllowed = Result.isOk programDecision |}
        }
