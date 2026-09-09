namespace Wanxiangshu.OpenCode.Host

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Fable.Core
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Requirement.Grounding

module RequirementGroundingRepositorySurface =

    type private RuntimeHandle(handle: JournalHandle, workspace: string, sessionId: string) =
        member _.Handle = handle
        member _.Workspace = workspace
        member _.SessionId = sessionId

    let private runtimeOf (value: obj) = unbox<RuntimeHandle> value

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

    let dispose (runtime: obj) : unit =
        JournalSurface.dispose (runtimeOf runtime).Handle

    let private summary (runtime: RuntimeHandle) (outcome: obj) =
        let handle = runtime.Handle
        let journal = handle.Journal
        let session = SessionId.create runtime.SessionId

        let caseName, failureCode, created =
            emitJsExpr
                outcome
                """
            (() => {
                const tagKey = 't' + 'ag';
                const fieldsKey = 'fiel' + 'ds';
                if ($0 && $0[tagKey] === 0) {
                    const createdList = $0[fieldsKey][2];
                    const createdArr = [];
                    if (Array.isArray(createdList)) {
                        createdArr.push(...createdList);
                    } else if (createdList && typeof createdList[Symbol.iterator] === 'function') {
                        for (const item of createdList) createdArr.push(item);
                    }
                    return ["Succeeded", null, createdArr];
                } else if ($0 && $0[tagKey] === 1) {
                    const failure = $0[fieldsKey][0];
                    let code = "unknown";
                    if (failure && failure[tagKey] === 0) code = "invalid_program";
                    else if (failure && failure[tagKey] === 1) code = "program_failed";
                    else if (failure && failure[tagKey] === 2) code = "program_timeout";
                    return ["Failed", code, []];
                }
                return ["Failed", "unknown", []];
            })()
            """

        box
            {| runtime = (runtime :> obj)
               caseName = caseName
               failureCode = failureCode
               pendingPackages =
                RequirementGroundingRuntime.pending journal session
                |> List.map _.PackageName
                |> List.toArray
               pendingMaterials =
                RequirementGroundingRuntime.pending journal session
                |> List.collect _.Materials
                |> List.map _.Path
                |> List.sort
                |> List.toArray
               created = created |}

    let private runAttempt failObservation (workspace: string) (sessionId: string) (program: string) : Task<obj> =
        task {
            match! boot workspace sessionId with
            | Error error -> return raise (InvalidOperationException error)
            | Ok runtime ->
                let surfaceBaseClass: string option =
                    emitJsExpr
                        ()
                        """
                    (() => {
                        let mod = null;
                        try {
                            if (typeof require === 'function') {
                                mod = require('../../Repository/Programming/Js/GeneratorSurface.js');
                            }
                        } catch (_) {}
                        if (!mod) {
                            try {
                                const procMod = (typeof process !== 'undefined' && typeof process.getBuiltinModule === 'function')
                                    ? process.getBuiltinModule('node:module')
                                    : null;
                                if (procMod && typeof procMod.createRequire === 'function') {
                                    const req = procMod.createRequire(import.meta.url);
                                    mod = req('../../Repository/Programming/Js/GeneratorSurface.js');
                                }
                            } catch (_) {}
                        }
                        if (mod && typeof mod.typedRole === 'function') {
                            const s = mod.typedRole("Coder", "en");
                            if (s && (s.BaseClassSource || s.baseClassSource)) return s.BaseClassSource || s.baseClassSource;
                        }
                        return null;
                    })()
                    """

                match surfaceBaseClass with
                | None -> return raise (InvalidOperationException "Coder js surface unavailable")
                | Some baseClassSource ->
                    let observe readPaths effectPaths =
                        task {
                            do!
                                RequirementGroundingGate.programObservation
                                    (Some runtime.Handle.Journal)
                                    runtime.Workspace
                                    runtime.SessionId
                                    readPaths
                                    effectPaths

                            if failObservation then
                                return raise (InvalidOperationException "observation failed after recording effects")
                        }

                    let! outcome =
                        emitJsExpr
                            (runtime.Workspace, baseClassSource, program, observe)
                            """
                        (() => {
                            let mod = null;
                            try {
                                if (typeof require === 'function') {
                                    mod = require('../../Repository/Programming/Js/OpenCode/ToolWorkflow.js');
                                }
                            } catch (_) {}
                            if (!mod) {
                                try {
                                    const procMod = (typeof process !== 'undefined' && typeof process.getBuiltinModule === 'function')
                                        ? process.getBuiltinModule('node:module')
                                        : null;
                                    if (procMod && typeof procMod.createRequire === 'function') {
                                        const req = procMod.createRequire(import.meta.url);
                                        mod = req('../../Repository/Programming/Js/OpenCode/ToolWorkflow.js');
                                    }
                                } catch (_) {}
                            }
                            if (mod && typeof mod.JsToolWorkflow_runWithFileAccessObservation === 'function') {
                                return mod.JsToolWorkflow_runWithFileAccessObservation(
                                    $0, $1, $2, 2000,
                                    Date.now() + 60000,
                                    1 << 20,
                                    null,
                                    $3
                                );
                            }
                            throw new Error("ToolWorkflow.runWithFileAccessObservation unavailable");
                        })()
                        """

                    return summary runtime outcome
        }

    let runFirstAttempt (workspace: string) (sessionId: string) (program: string) : Task<obj> =
        runAttempt false workspace sessionId program

    let runWithObservationFailure (workspace: string) (sessionId: string) (program: string) : Task<obj> =
        runAttempt true workspace sessionId program
