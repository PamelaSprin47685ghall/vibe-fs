module Wanxiangshu.Runtime.Nudge.NudgeSkipTokens

open System.Text.RegularExpressions

/// Boundary parsers for model opt-out tokens in untrusted assistant text.
/// Call once when writing assistant_completed; never re-scan in fold/derive.
let private skipTodoRe = Regex(@"<skip-todo-check\s*\/?>", RegexOptions.IgnoreCase)
let private skipReviewRe = Regex(@"<skip-review-check\s*\/?>", RegexOptions.IgnoreCase)

let private cleanTextOutsideCodeFences (text: string) : string =
    if not (text.Contains "```") && not (text.Contains "~~~") then
        text
    else
        let lines = text.Split('\n')
        let mutable inFence = false
        let cleanedLines = ResizeArray<string>()

        for line in lines do
            let trimmed = line.TrimStart()

            if trimmed.StartsWith("```") || trimmed.StartsWith("~~~") then
                inFence <- not inFence
            else if not inFence then
                cleanedLines.Add(line)

        String.concat "\n" cleanedLines

let parseSkipTodo (text: string) : bool =
    cleanTextOutsideCodeFences text |> skipTodoRe.IsMatch

let parseSkipReview (text: string) : bool =
    cleanTextOutsideCodeFences text |> skipReviewRe.IsMatch
