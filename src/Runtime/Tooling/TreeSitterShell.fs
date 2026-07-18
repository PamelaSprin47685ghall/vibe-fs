module Wanxiangshu.Runtime.TreeSitterShell

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.TreeSitterPlatform
open Wanxiangshu.Runtime.TreeSitterDiagnostics
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Kernel.PatchParser

let extractFilePaths (args: obj) : string list =
    if isNullish args then
        []
    else
        let path =
            [ "path"; "file_path"; "filePath" ]
            |> List.tryPick (fun key ->
                let value = get args key
                if isNullish value then None else Some(string value))

        match path with
        | Some p when p <> "" -> [ p ]
        | _ ->
            let patchText = get args "patchText"

            if isNullish patchText then
                []
            else
                pathsFromPatchText (string patchText)

let extractFilePath (args: obj) : string option = extractFilePaths args |> List.tryHead

let checkSyntax (content: string) (filePath: string) : JS.Promise<SyntaxCheckResult> =
    promise {
        if isExcludedPath filePath then
            return Ok("", [||])
        else
            match tryGetPack () with
            | Result.Error _ ->
                let styleErrors = runGeneralStyleChecks content
                return Ok("", styleErrors)
            | Result.Ok pack ->
                let lang = detectLanguage pack content filePath

                if lang = "" then
                    let styleErrors = runGeneralStyleChecks content
                    return Ok("", styleErrors)
                else
                    let parserResult =
                        try
                            Result.Ok(getOrCallWith pack "getParser" lang)
                        with e ->
                            Result.Error $"parser load failed: {e.Message}"

                    match parserResult with
                    | Result.Error reason -> return Failed(lang, reason)
                    | Result.Ok parser ->
                        let treeResult =
                            try
                                let tree = getOrCallWith parser "parse" content

                                if isNullish tree then
                                    Result.Error "parser returned undefined"
                                else
                                    Result.Ok tree
                            with e ->
                                Result.Error $"parse failed: {e.Message}"

                        match treeResult with
                        | Result.Error reason -> return Failed(lang, reason)
                        | Result.Ok tree ->
                            let rootNode = getOrCall tree "rootNode"
                            let errors, _ = collectDiagnostics rootNode []
                            let astErrors = errors |> List.rev |> Array.ofList
                            let astNodes = collectAstNodes rootNode [] |> Array.ofList

                            let styleErrors =
                                let funcErrors = checkFunctionLengths defaultStyleLimits astNodes
                                let fileErrors = checkFileLineCount defaultStyleLimits content
                                Array.append funcErrors fileErrors

                            return Ok(lang, Array.append astErrors styleErrors)
    }

[<Import("promises", "node:fs")>]
let private fsPromises: obj = jsNative

let readAndCheckSyntax (filePath: string) (cwd: string) (includeOk: bool) : JS.Promise<string option> =
    promise {
        try
            let abs = pathResolve cwd filePath
            let! (content: string) = fsPromises?readFile (abs, "utf-8")
            let! result = checkSyntax content filePath
            return formatSyntaxDiagnostics filePath result includeOk
        with ex ->
            return Some $"[tree-sitter] check failed for {filePath}: {ex.Message}"
    }

let appendSyntaxDiagnostics (filePath: string) (content: string) (includeOk: bool) : JS.Promise<string option> =
    promise {
        let! result = checkSyntax content filePath
        return formatSyntaxDiagnostics filePath result includeOk
    }
