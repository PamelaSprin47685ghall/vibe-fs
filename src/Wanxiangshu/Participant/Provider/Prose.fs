namespace Wanxiangshu.Participant.Provider

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// PROMPT-019: load + substitute provider prose. LLM-facing layout stays in LlmFacing.
/// Domain owns semantic paths and pure assembly; strict `{{name}}`
/// substitution, pairing and reads are owned by `wanxiangli/resources`.
[<RequireQualifiedAccess>]
module ProviderProse =

    [<Import("Resources", "wanxiangli/resources")>]
    let private library: obj = jsNative

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string, b: string) : string = jsNative

    [<Import("dirname", "node:path")>]
    let private dirname (path: string) : string = jsNative

    [<Import("fileURLToPath", "node:url")>]
    let private fileURLToPath (url: string) : string = jsNative

    [<Emit("import.meta.url")>]
    let private importMetaUrl: string = jsNative

    let private packageRoot () : string =
        let rec walk (dir: string) : string =
            if isNull dir then
                raise (InvalidOperationException "package root not found")
            elif existsSync (pathJoin (dir, "package.json")) then
                dir
            else
                walk (dirname dir)

        walk (dirname (fileURLToPath importMetaUrl))

    // DSL-MUTABLE: resource — lazily created library binding to this package root
    let mutable private instance: obj = null

    let private resources () : obj =
        if isNull instance then
            instance <- emitJsExpr (library, packageRoot ()) "new $0($1)"

        instance

    let private substitutionsObject (substitutions: Map<string, string>) : obj =
        createObj (Map.toList substitutions |> List.map (fun (key, value) -> key, box value))

    /// Language of a session: the live global preference. The session id
    /// decides nothing (HOST-026 single authority).
    let languageOf (sessionId: SessionId) : ProviderLanguage =
        SessionProviderLanguage.languageOf sessionId

    /// Replace `{{name}}` with values. Values are not translated. Leftover placeholders fail closed.
    let substitute (template: string) (substitutions: Map<string, string>) : string =
        resources()?renderTemplate(template, substitutionsObject substitutions)

    /// Load the semantic pair, verify placeholder parity across languages,
    /// then substitute on the requested language.
    let render (language: ProviderLanguage) (semanticPath: string) (substitutions: Map<string, string>) : string =
        resources()?render(ProviderLanguage.externalName language, semanticPath, substitutionsObject substitutions)

    /// Instruction lines for LlmFacing: preserve blank lines as "".
    let instructionLines
        (language: ProviderLanguage)
        (semanticPath: string)
        (substitutions: Map<string, string>)
        : string list =
        render language semanticPath substitutions
        |> fun text -> text.Replace("\r\n", "\n").TrimEnd('\n')
        |> fun text -> text.Split([| '\n' |], StringSplitOptions.None)
        |> Array.toList

    let document (language: ProviderLanguage) (semanticPath: string) (substitutions: Map<string, string>) : string =
        LlmFacing.renderInstructions (instructionLines language semanticPath substitutions)

    let documentFor (sessionId: SessionId) (semanticPath: string) (substitutions: Map<string, string>) : string =
        document (SessionProviderLanguage.languageOf sessionId) semanticPath substitutions
