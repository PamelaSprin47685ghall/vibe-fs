module Wanxiangshu.Runtime.ExecutorJavascript

open Fable.Core
open Fable.Core.JsInterop
open System.Text.RegularExpressions
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime.ExecutorJavascriptHelper
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ExecutorSpawnHelper

/// Build the ESM prelude that gives a transpiled script require/__dirname/__filename.
let createJavascriptPrelude (cwd: string) : string =
    let requirePath = join cwd "__runner__.cjs"
    let filename = join cwd "__runner__.mjs"

    String.concat
        "\n"
        [ "import { createRequire } from \"node:module\";"
          $"const require = createRequire({jsonEscape requirePath});"
          $"const __dirname = {jsonEscape cwd};"
          $"const __filename = {jsonEscape filename};"
          "" ]

/// Resolve a relative module specifier (`./x`, `../x`) to a file:// URL.
let resolveJavascriptSpecifier (cwd: string) (specifier: string) : string =
    let match' = Regex.Match(specifier, @"^(\.{1,2}(?:/[^?#]*)?)([?#].*)?$")

    if not match'.Success then
        specifier
    else
        let basePath = match'.Groups.[1].Value

        let suffix =
            if match'.Groups.[2].Success then
                match'.Groups.[2].Value
            else
                ""

        $"{(pathToFileURL (resolve cwd basePath))?href}{suffix}"

/// Rewrite relative import specifiers in a JS program to absolute file:// URLs.
/// Uses es-module-lexer parse results to build output from non-overlapping segments
/// rather than string-index replacement, avoiding sourcemap/edge-case fragility.
let rewriteJavascriptModuleSpecifiers (program: string) (cwd: string) : JS.Promise<string> =
    promise {
        let! imports = parseImports program

        if Dyn.isNullish imports || Array.isEmpty imports then
            return program
        else
            let lastEnd, segmentsRev =
                imports
                |> Array.fold
                    (fun (lastEnd, segmentsRev) imp ->
                        let n = Dyn.get imp "n"

                        if Dyn.isNullish n then
                            (lastEnd, segmentsRev)
                        else
                            let ns = string n

                            if not (Regex.IsMatch(ns, @"^\.\.?/")) then
                                (lastEnd, segmentsRev)
                            else
                                let d = Dyn.get imp "d"
                                let isDynamic = not (Dyn.isNullish d) && unbox<int> d <> -1
                                let s = unbox<int> (Dyn.get imp "s")
                                let e = unbox<int> (Dyn.get imp "e")
                                let sAdj = if isDynamic then s + 1 else s
                                let eAdj = if isDynamic then e - 1 else e

                                let withPrefix =
                                    if sAdj > lastEnd then
                                        program.[lastEnd .. sAdj - 1] :: segmentsRev
                                    else
                                        segmentsRev

                                (eAdj, resolveJavascriptSpecifier cwd ns :: withPrefix))
                    (0, [])

            let segmentsRev =
                if lastEnd < program.Length then
                    program.[lastEnd..] :: segmentsRev
                else
                    segmentsRev

            if List.isEmpty segmentsRev then
                return program
            else
                return String.concat "" (List.rev segmentsRev)
    }

/// Ensure the temp project dir is an ESM project with tsx + dependencies.
/// Always guarantees `type: "module"` is present, repairing a pre-existing
/// package.json that lacks it even when no dependencies need installation.
let ensureJavascriptProject
    (scope: RuntimeScope)
    (projectDir: string)
    (dependencies: string list)
    (timeoutMs: int option)
    (sessionId: string option)
    : JS.Promise<RunOutcome option> =
    promise {
        mkdirSync projectDir (box {| recursive = true |})
        let pkgPath = $"{projectDir}/package.json"

        let pkg =
            if existsSync pkgPath then
                parsePkgJson (readFileSync pkgPath "utf8")
            else
                parsePkgJson "{}"

        let deps = Dyn.get pkg "dependencies"
        let required = "tsx" :: dependencies |> List.distinct

        let toInstall =
            required |> List.filter (fun pkgName -> Dyn.isNullish (Dyn.get deps pkgName))

        for pkgName in toInstall do
            setDep deps pkgName "*"

        writeFileSync pkgPath $"{jsonStringify pkg}\n" "utf-8"

        if not toInstall.IsEmpty then
            let! res = npmInstall scope projectDir (Array.ofList toInstall) timeoutMs sessionId
            return Some res
        else
            return None
    }
