namespace Wanxiangshu.Tools

open System
open Fable.Core
open Fable.Core.JsInterop

/// Loads role system prompts from resources/prompts/*-system.md.
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

    let private here () = dirname (fileURLToPath importMetaUrl)

    let private promptAt (root: string) (fileName: string) =
        pathJoin (pathJoin (pathJoin (root, "resources"), "prompts"), fileName)

    /// Walk up from compiled JS until resources/prompts/<file> exists.
    let private resolvePromptPath (fileName: string) : string =
        let rec walk (dir: string) (budget: int) =
            if budget <= 0 then
                None
            else
                let candidate = promptAt dir fileName

                if existsSync candidate then
                    Some candidate
                else
                    walk (dirname dir) (budget - 1)

        match walk (here ()) 12 with
        | Some path -> path
        | None ->
            raise (
                InvalidOperationException(
                    sprintf "system prompt not found from %s (expected resources/prompts/%s)" (here ()) fileName
                )
            )

    let private load (fileName: string) : string =
        let full = resolvePromptPath fileName

        if not (existsSync full) then
            raise (InvalidOperationException(sprintf "Missing system prompt asset: %s" full))

        readFileSync(full, "utf8").Trim()

    let managerSystemPrompt: string = load "manager-system.md"

    let coderSystemPrompt: string = load "coder-system.md"

    let devopsSystemPrompt: string = load "devops-system.md"

    let inspectorSystemPrompt: string = load "inspector-system.md"

    let reviewerSystemPrompt: string = load "reviewer-system.md"

    let browserSystemPrompt: string = load "browser-system.md"

    let meditatorSystemPrompt: string = load "meditator-system.md"

    let orchestratorSystemPrompt: string = load "orchestrator-system.md"

    let executorSystemPrompt: string = load "executor-system.md"

    let bloggerSystemPrompt: string = load "blogger-system.md"
