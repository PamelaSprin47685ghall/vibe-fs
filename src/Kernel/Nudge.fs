[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Wanxiangshu.Kernel.Nudge

/// Which kind of nudge, if any, a session needs right now.
type NudgeAction =
    | NudgeTodo
    | NudgeLoop
    | NudgeRunner
    | NudgeNone

let ofString =
    function
    | "nudge-todo" -> Ok NudgeTodo
    | "nudge-loop" -> Ok NudgeLoop
    | "nudge-runner" -> Ok NudgeRunner
    | "none" -> Ok NudgeNone
    | other -> Error $"Invalid NudgeAction: \"{other}\""

let toString =
    function
    | NudgeTodo -> "nudge-todo"
    | NudgeLoop -> "nudge-loop"
    | NudgeRunner -> "nudge-runner"
    | NudgeNone -> "none"

/// Decode `openTodosJson` written by Encode.Auto for `string list`.
/// Rejects object forms; fold never re-parses freeform prose.
let decodeOpenTodosJson (json: string) : string list =
    let trimmed = json.Trim()

    if trimmed = "" || trimmed = "[]" then
        []
    elif trimmed.Length < 2 || trimmed.[0] <> '[' || trimmed.[trimmed.Length - 1] <> ']' then
        []
    else
        let inner = trimmed.Substring(1, trimmed.Length - 2).Trim()

        if inner = "" then
            []
        elif inner.Contains "{" then
            []
        else
            let rec loop (s: string) (acc: string list) =
                let s = s.TrimStart()

                if s = "" then
                    List.rev acc
                elif s.[0] <> '"' then
                    List.rev acc
                else
                    let rec scan i escaped =
                        if i >= s.Length then
                            None
                        elif escaped then
                            scan (i + 1) false
                        elif s.[i] = '\\' then
                            scan (i + 1) true
                        elif s.[i] = '"' then
                            Some i
                        else
                            scan (i + 1) false

                    match scan 1 false with
                    | None -> List.rev acc
                    | Some endQuote ->
                        let raw = s.Substring(1, endQuote - 1).Replace("\\\"", "\"").Replace("\\\\", "\\")
                        let rest = s.Substring(endQuote + 1).TrimStart()

                        let rest =
                            if rest.StartsWith(",") then
                                rest.Substring(1)
                            else
                                rest

                        loop rest (raw :: acc)

            loop inner []

let payloadBool (key: string) (payload: Map<string, string>) : bool =
    match Map.tryFind key payload with
    | Some "true"
    | Some "True"
    | Some "1" -> true
    | _ -> false

/// Dedup anchor from stable typed fields only — never assistant prose.
let nudgeAnchorKey (turnId: string) (agent: string option) (model: string option) : string =
    let parts =
        [ turnId.Trim()
          defaultArg agent ""
          defaultArg model "" ]
        |> List.filter (fun s -> s <> "")

    String.concat "\u001e" parts
