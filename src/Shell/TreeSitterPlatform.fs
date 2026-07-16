module Wanxiangshu.Shell.TreeSitterPlatform

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn

type internal Position = { row: int; column: int }

[<Import("createRequire", "node:module")>]
let private createRequire (url: string) : (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta: obj = jsNative

let private importMetaUrl: string = string importMeta?url

[<Import("default", "highlight.js")>]
let private hljs: {| highlightAuto: string -> {| language: string; relevance: float |} |} =
    jsNative

let internal callOrGet (o: obj) (key: string) (call: unit -> obj) : obj =
    if Dyn.typeIs (Dyn.get o key) "function" then
        call ()
    else
        Dyn.get o key

let internal getOrCall (o: obj) (key: string) : obj = callOrGet o key (fun () -> o?(key) ())

let internal getOrCallWith (o: obj) (key: string) (arg: obj) : obj =
    callOrGet o key (fun () -> o?(key) (arg))

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

[<Import("resolve", "node:path")>]
let internal pathResolve (cwd: string) (filePath: string) : string = jsNative

let private processPlatform () : string = nodeProcess?platform
let private processArch () : string = nodeProcess?arch

let private platformSuffix (platform: string) (arch: string) : string option =
    match platform, arch with
    | "darwin", "arm64" -> Some "darwin-arm64"
    | "darwin", "x64" -> Some "darwin-x64"
    | "linux", "x64" -> Some "linux-x64-gnu"
    | "linux", "arm64" -> Some "linux-arm64-gnu"
    | "win32", "x64" -> Some "win32-x64-msvc"
    | "win32", "arm64" -> Some "win32-arm64-msvc"
    | _ -> None

let private nodeRequire = createRequire importMetaUrl

let mutable private cachedPack: Result<obj, string> option = None

let internal tryGetPack () : Result<obj, string> =
    match cachedPack with
    | Some packResult -> packResult
    | None ->
        // Monkey-patch: force glibc detection to succeed even when Node.js
        // report doesn't expose glibcVersion (broken musl false-positive in
        // @kreuzberg/tree-sitter-language-pack/index.js).
        emitJsExpr
            ()
            """
        if (typeof process.report === 'object' && typeof process.report.getReport === 'function') {
            const orig = process.report.getReport.bind(process.report);
            process.report.getReport = function() {
                const r = orig();
                if (r && r.header && r.header.glibcVersion === undefined) {
                    r.header.glibcVersion = '2.31';
                }
                return r;
            };
        }
        """

        let warmPack (pack: obj) =
            try
                getOrCall pack "downloadAll" |> ignore
            with _ ->
                ()

            pack

        let loadFromRootPackage () =
            try
                Result.Ok(nodeRequire "@kreuzberg/tree-sitter-language-pack" |> warmPack)
            with e ->
                Result.Error $"root package load failed: {e.Message}"

        let loadFromNativePath () =
            match platformSuffix (processPlatform ()) (processArch ()) with
            | None -> Result.Error "Unsupported platform"
            | Some suffix ->
                try
                    let nativePath =
                        nodeRequire?resolve ($"@kreuzberg/tree-sitter-language-pack/ts-pack-core-node.{suffix}.node")

                    Result.Ok(nodeRequire nativePath |> warmPack)
                with e ->
                    Result.Error $"native pack load failed: {e.Message}"

        let result =
            match loadFromRootPackage () with
            | Result.Ok pack -> Result.Ok pack
            | Result.Error rootError ->
                match loadFromNativePath () with
                | Result.Ok pack -> Result.Ok pack
                | Result.Error nativeError -> Result.Error(rootError + "; " + nativeError)

        cachedPack <- Some result
        result

let internal detectLanguage (pack: obj) (content: string) (filePath: string) : string =
    let probePath () : string option =
        try
            let r = getOrCallWith pack "detectLanguageFromPath" filePath
            if isNullish r then None else Some(string r)
        with _ ->
            None

    let probeContent () : string option =
        try
            let r = getOrCallWith pack "detectLanguageFromContent" content
            if isNullish r then None else Some(string r)
        with _ ->
            None

    let probeHljs () : string option =
        try
            let hl = hljs.highlightAuto content

            if not (isNullish hl) && not (isNullish hl.language) && hl.relevance >= 5.0 then
                let hasLang = getOrCallWith pack "hasLanguage" hl.language
                if truthy hasLang then Some(string hl.language) else None
            else
                None
        with _ ->
            None

    [ probePath; probeContent; probeHljs ]
    |> List.tryPick (fun probe -> probe ())
    |> Option.defaultValue ""
