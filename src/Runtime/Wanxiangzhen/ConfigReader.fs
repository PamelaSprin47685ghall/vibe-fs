module Wanxiangshu.Runtime.Wanxiangzhen.ConfigReader

open Fable.Core
open Wanxiangshu.Kernel.Wanxiangzhen.SquadConfig
open Wanxiangshu.Runtime.Yaml
open Wanxiangshu.Runtime.Dyn

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string) (encoding: string) : string = jsNative

[<Import("existsSync", "node:fs")>]
let private existsSync (path: string) : bool = jsNative

[<Import("join", "node:path")>]
let private pathJoin (a: string) (b: string) : string = jsNative

let private extractFrontmatter (text: string) : string option =
    let trimmed = text.TrimStart()

    if not (trimmed.StartsWith "---") then
        None
    else
        let afterFirst = trimmed.Substring 3
        let endIdx = afterFirst.IndexOf "---"

        if endIdx < 0 then
            None
        else
            Some(afterFirst.Substring(0, endIdx).Trim())

let private safeInt (o: obj) : int =
    try
        unbox<int> o
    with _ ->
        try
            int (unbox<float> o)
        with _ ->
            try
                int (string o)
            with _ ->
                0

let private parseSquadConfig (parsed: obj) : SquadConfig =
    let squad = get parsed "squad"

    if isNullish squad then
        defaults
    else
        let mc = get squad "maxConcurrent"
        let term = str squad "terminal"
        let mb = get squad "masterBranch"
        let sd = get squad "sharedDirs"

        { MaxConcurrent = if isNullish mc then defaults.MaxConcurrent else safeInt mc
          Terminal =
            if System.String.IsNullOrEmpty term then
                defaults.Terminal
            else
                term
          MasterBranch = if isNullish mb then None else Some(string mb)
          SharedDirs =
            if isNullish sd || not (isArray sd) then
                []
            else
                (sd :?> obj array) |> Array.map string |> Array.toList }

let readConfig (worktree: string) : SquadConfig =
    let path = pathJoin worktree "AGENTS.md"

    if not (existsSync path) then
        defaults
    else
        let text = readFileSync path "utf-8"

        match extractFrontmatter text with
        | None -> defaults
        | Some fm ->
            try
                let parsed = parse fm

                if isNullish parsed then
                    defaults
                else
                    mergeWithDefaults (Some(parseSquadConfig parsed))
            with _ ->
                defaults
