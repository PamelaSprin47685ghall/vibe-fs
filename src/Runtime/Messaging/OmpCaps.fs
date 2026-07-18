module Wanxiangshu.Runtime.OmpCaps

open Wanxiangshu.Runtime.OmpFileScanner
open Wanxiangshu.Runtime.OmpPromptContext

type OmpCapsFile = Wanxiangshu.Runtime.OmpFileScanner.OmpCapsFile

let findOmpCapsFiles (projectRoot: string) : JS.Promise<OmpCapsFile list> =
    Wanxiangshu.Runtime.OmpFileScanner.findOmpCapsFiles projectRoot

let buildCapsContextAsync (projectRoot: string) : JS.Promise<string> =
    promise {
        let! files = findOmpCapsFiles projectRoot
        return formatOmpCapsContext (files |> List.map (fun f -> (f.label, f.content)))
    }

let private normalizeSystemPrompt (systemPrompt: obj) : string array =
    if isNull systemPrompt then
        [||]
    elif Dyn.isArray systemPrompt then
        systemPrompt :?> obj array |> Array.map string
    else
        [| string systemPrompt |]

let private hasCapsContext (parts: string array) =
    parts |> Array.exists (fun s -> s.Contains capsMarker)

let appendCapsContext (systemPrompt: obj) (projectRoot: string) : JS.Promise<string array> =
    promise {
        let parts = normalizeSystemPrompt systemPrompt

        if hasCapsContext parts then
            return parts
        else
            let! caps = buildCapsContextAsync projectRoot

            if caps = "" then
                return parts
            else
                return Array.append [| caps |] parts
    }

let stripHostAgentsPrompt (systemPrompt: obj) : string array =
    let normalizeSystemPrompt (systemPrompt: obj) : string array =
        if isNull systemPrompt then
            [||]
        elif Dyn.isArray systemPrompt then
            systemPrompt :?> obj array |> Array.map string
        else
            [| string systemPrompt |]

    let stripDirContextSegment (segment: string) : string option =
        let pattern = Regex("<dir-context>[\s\S]*?</dir-context>", RegexOptions.IgnoreCase)
        let stripped = pattern.Replace(segment, "").Trim()
        if stripped = "" then None else Some stripped

    normalizeSystemPrompt systemPrompt |> Array.choose stripDirContextSegment
