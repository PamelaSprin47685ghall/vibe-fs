namespace Wanxiangshu.Next.Tools

open System
open Fable.Core
open Fable.Core.JsInterop

/// Loads role system prompts from next/prompts/*.md.
/// These are AgentConfig.prompt values (host system prompt), not user messages.
module PromptAssets =

    [<Import("readFileSync", "node:fs")>]
    let private readFileSync (path: string, encoding: string) : string = jsNative

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    [<Import("fileURLToPath", "node:url")>]
    let private fileURLToPath (url: string) : string = jsNative

    [<Import("dirname", "node:path")>]
    let private dirname (path: string) : string = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string, b: string) : string = jsNative

    [<Emit("import.meta.url")>]
    let private importMetaUrl: string = jsNative

    let private promptsDir () =
        // Compiled to build/next/Tools/*.js → sibling ../prompts
        pathJoin (dirname (fileURLToPath importMetaUrl), "../prompts")

    let private load (fileName: string) : string =
        let full = pathJoin (promptsDir (), fileName)

        if not (existsSync full) then
            raise (InvalidOperationException(sprintf "Missing system prompt asset: %s" full))

        readFileSync(full, "utf8").Trim()

    let managerSystemPrompt: string = load "manager-system.md"
