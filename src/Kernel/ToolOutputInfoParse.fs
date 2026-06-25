module VibeFs.Kernel.ToolOutputInfoParse

open VibeFs.Kernel.PromptFrontMatter
open VibeFs.Kernel.ToolOutputInfoTypes

let private readBlockAt (lines: string[]) start (prefix: string) =
    let rec gather j acc =
        if j >= lines.Length then String.concat "\n" (List.rev acc), j
        elif lines.[j].StartsWith prefix then
            gather (j + 1) (lines.[j].Substring(prefix.Length) :: acc)
        else String.concat "\n" (List.rev acc), j
    gather start []

let private parseScalarTail raw =
    match parseYamlStringValue raw with
    | Some v -> v
    | None -> raw.Trim()

let private parseInfoItemLine (lines: string[]) (i: int) : InfoItem option * int =
    if i >= lines.Length || not (lines.[i].StartsWith("  - ")) then None, i + 1
    else
        let rest = lines.[i].Substring(4)
        let sep = rest.IndexOf(": ")
        if sep <= 0 then None, i + 1
        else
            let key = rest.Substring(0, sep)
            let raw = rest.Substring(sep + 2)
            match key, raw with
            | "hint", r ->
                match parseYamlStringValue r with
                | Some v -> Some(InfoItem.Hint v), i + 1
                | None when r = "|" ->
                    let v, ni = readBlockAt lines (i + 1) "    "
                    Some(InfoItem.Hint v), ni
                | None -> Some(InfoItem.Hint (r.Trim())), i + 1
            | "syntax", r ->
                match parseYamlStringValue r with
                | Some v -> Some(InfoItem.Syntax v), i + 1
                | None when r = "|" ->
                    let v, ni = readBlockAt lines (i + 1) "    "
                    Some(InfoItem.Syntax v), ni
                | None -> Some(InfoItem.Syntax (r.Trim())), i + 1
            | "iterator", r -> Some(InfoItem.Iterator (parseScalarTail r)), i + 1
            | "status", r -> Some(InfoItem.Status (parseScalarTail r)), i + 1
            | "exit_code", r ->
                let t = parseScalarTail r
                match System.Int32.TryParse t with
                | true, n -> Some(InfoItem.ExitCode n), i + 1
                | false, _ -> None, i + 1
            | "signal", r -> Some(InfoItem.Signal (parseScalarTail r)), i + 1
            | "timeout_ms", r ->
                let t = parseScalarTail r
                match System.Int32.TryParse t with
                | true, n -> Some(InfoItem.TimeoutMs n), i + 1
                | false, _ -> None, i + 1
            | "tool_output", r ->
                let t = parseScalarTail r
                let ref' =
                    if t = seeBelow then ToolOutputBodyRef.SeeBelow
                    elif t = seeBelowTruncated then ToolOutputBodyRef.SeeBelowTruncated
                    elif t = noChangeSincePreviousReadWrite then ToolOutputBodyRef.NoChangeSincePreviousReadWrite
                    else ToolOutputBodyRef.SeeBelow
                Some(InfoItem.BodyRef ref'), i + 1
            | _ -> None, i + 1

let tryParseInfoList (lines: string[]) (startIndex: int) : InfoItem list * int option =
    let rec loop i acc =
        if i >= lines.Length then acc, None
        elif lines.[i] = "---" then acc, Some i
        else
            let item, next = parseInfoItemLine lines i
            match item with
            | Some it -> loop next (acc @ [ it ])
            | None -> loop (if next > i then next else i + 1) acc
    if startIndex >= lines.Length then [], None
    elif lines.[startIndex] <> "info:" then [], None
    else loop (startIndex + 1) []