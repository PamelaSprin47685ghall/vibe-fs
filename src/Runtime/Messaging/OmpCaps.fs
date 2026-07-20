module Wanxiangshu.Runtime.OmpCaps

open Fable.Core
open Fable.Core.JsInterop
open System.Text.RegularExpressions
open Wanxiangshu.Runtime.OmpFileScanner
open Wanxiangshu.Runtime.Dyn

type OmpCapsFile = Wanxiangshu.Runtime.OmpFileScanner.OmpCapsFile

let private capsMarker = "<caps-context"

let private escapeXmlAttr (value: string) : string =
    value
        .Replace("&", "&amp;")
        .Replace("\"", "&quot;")
        .Replace("'", "&apos;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")

let private formatOmpCapsContext (files: (string * string) list) : string =
    if List.isEmpty files then
        ""
    else
        files
        |> List.map (fun f -> $"<caps-context file=\"{escapeXmlAttr (fst f)}\">\n{snd f}\n</caps-context>")
        |> String.concat "\n\n"

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

let private stripDirContextSegment (segment: string) : string option =
    let pattern = Regex("<dir-context>[\s\S]*?</dir-context>", RegexOptions.IgnoreCase)
    let stripped = pattern.Replace(segment, "").Trim()
    if stripped = "" then None else Some stripped

let stripHostAgentsPrompt (systemPrompt: obj) : string array =
    normalizeSystemPrompt systemPrompt |> Array.choose stripDirContextSegment
