module VibeFs.Kernel.PatchParser

open System.Text.RegularExpressions

let private patchPathRe = Regex(@"^\*\*\* (?:Add File|Update File|Move to): (.+)$")

/// Extract every `*** Add File|Update File|Move to: <path>` target from patch text.
let pathsFromPatchText (patchText: string) : string list =
    if System.String.IsNullOrEmpty patchText then []
    else
        patchText.Split('\n')
        |> Seq.choose (fun line ->
            let m = patchPathRe.Match line
            if m.Success then Some m.Groups.[1].Value else None)
        |> List.ofSeq
        |> List.distinct