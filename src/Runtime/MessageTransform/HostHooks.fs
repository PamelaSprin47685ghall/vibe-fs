module Wanxiangshu.Runtime.MessageTransform.HostHooks

open Wanxiangshu.Runtime

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.CapsFileCache
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.Pipeline
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.WorkspaceFiles
open Wanxiangshu.Runtime.WorkspaceReverieFiles

[<Import("relative", "node:path")>]
let private pathRelative (a: string) (b: string) : string = jsNative

let extractObjectives (plan: MessageTransformPlan) : string list =
    plan.Cleaned
    |> List.collect (fun m ->
        m.parts
        |> List.choose (fun p ->
            match p with
            | TextPart text when not (System.String.IsNullOrWhiteSpace text) -> Some(text.Trim())
            | _ -> None))

let buildCapsFileFromResult (baseDir: string) (r) : CapsFile option =
    match r.content with
    | Some content ->
        Some
            { filePath = r.filePath
              label = pathRelative baseDir r.filePath
              content = content }
    | _ -> None

let deduplicateCapsFiles (files: CapsFile list) : CapsFile list =
    let folder (seen: Set<string>, acc: CapsFile list) (file: CapsFile) =
        if seen.Contains file.filePath then
            (seen, acc)
        else
            (seen.Add file.filePath, file :: acc)

    files |> List.fold folder (Set.empty, []) |> snd |> List.rev

let injectSubagentFilesIfAny
    (scope: RuntimeScope)
    (plan: MessageTransformPlan)
    (baseFiles: CapsFile list)
    : JS.Promise<CapsFile list> =
    promise {
        let isExcluded =
            match plan.CapsInjectionPolicy with
            | Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Exclude -> true
            | Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Include -> false

        if isExcluded || not plan.IsSubagentSession then
            return baseFiles
        else
            let objectivesAndTexts = extractObjectives plan

            let tempFiles =
                objectivesAndTexts
                |> List.tryPick (fun key ->
                    match scope.TryGetTempFiles(plan.SessionID + "\u0000" + key) with
                    | Some files -> Some files
                    | None -> scope.TryGetTempFiles(key))
                |> Option.defaultValue []

            if tempFiles.IsEmpty then
                return baseFiles
            else
                let! results = readReverieFiles plan.Directory tempFiles

                let loaded = results |> List.choose (buildCapsFileFromResult plan.Directory)

                let merged = baseFiles @ loaded

                let deduped = deduplicateCapsFiles merged

                return deduped
    }

type CapsLoadPolicy =
    | RequireDirectory
    | AllowEmptyDirectory

let loadCapsForScope
    (scope: RuntimeScope)
    (policy: CapsLoadPolicy)
    (plan: MessageTransformPlan)
    : JS.Promise<CapsFile list> =
    let isExcluded =
        match plan.CapsInjectionPolicy with
        | Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Exclude -> true
        | Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Include -> false

    if isExcluded then
        Promise.lift []
    else
        match policy with
        | RequireDirectory when plan.Directory = "" -> Promise.lift []
        | RequireDirectory
        | AllowEmptyDirectory ->
            promise {
                let! baseFiles = getOrLoadCapsFilesForScope scope plan.SessionID plan.Directory
                return! injectSubagentFilesIfAny scope plan baseFiles
            }
