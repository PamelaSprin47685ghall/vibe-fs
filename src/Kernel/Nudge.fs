[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Wanxiangshu.Kernel.Nudge

open System.Text.RegularExpressions

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

let skipTodoRe = Regex(@"<skip-todo-check\s*\/?>", RegexOptions.IgnoreCase)
let skipReviewRe = Regex(@"<skip-review-check\s*\/?>", RegexOptions.IgnoreCase)

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

let internal skipsTodo (text: string) =
    cleanTextOutsideCodeFences text |> skipTodoRe.IsMatch

let internal skipsReview (text: string) =
    cleanTextOutsideCodeFences text |> skipReviewRe.IsMatch
