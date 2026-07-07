module Wanxiangshu.Shell.MessageTransformHostHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.CapsFileCache
open Wanxiangshu.Shell.MessageTransformPipeline
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.WorkspaceFiles

[<Import("relative", "node:path")>]
let private pathRelative (a: string) (b: string) : string = jsNative

let injectSubagentFilesIfAny
    (scope: RuntimeScope)
    (plan: MessageTransformPlan)
    (baseFiles: CapsFile list)
    : JS.Promise<CapsFile list> =
    promise {
        if plan.Excluded || not plan.IsSubagentSession then
            return baseFiles
        else
            let objectivesAndTexts =
                plan.Cleaned
                |> List.collect (fun m ->
                    m.parts
                    |> List.choose (fun p ->
                        match p with
                        | TextPart text when not (System.String.IsNullOrWhiteSpace text) ->
                            let scalars = Wanxiangshu.Kernel.PromptFrontMatter.parseFrontMatterScalars text
                            match Map.tryFind "objective" scalars with
                            | Some objVal when not (System.String.IsNullOrWhiteSpace objVal) -> Some (objVal.Trim())
                            | _ -> Some (text.Trim())
                        | _ -> None))
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
                let loaded =
                    results
                    |> List.choose (fun r ->
                        match r.content with
                        | Some content ->
                            Some { filePath = r.filePath
                                   label = pathRelative plan.Directory r.filePath
                                   content = content }
                        | _ -> None)
                let merged = baseFiles @ loaded
                let deduped =
                    let folder (seen: Set<string>, acc: CapsFile list) (file: CapsFile) =
                        if seen.Contains file.filePath then (seen, acc)
                        else (seen.Add file.filePath, file :: acc)
                    merged |> List.fold folder (Set.empty, []) |> snd |> List.rev
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
    if plan.Excluded then Promise.lift []
    else
        match policy with
        | RequireDirectory when plan.Directory = "" -> Promise.lift []
        | RequireDirectory | AllowEmptyDirectory ->
            promise {
                let! baseFiles = getOrLoadCapsFilesForScope scope plan.SessionID plan.Directory
                return! injectSubagentFilesIfAny scope plan baseFiles
            }
