module Wanxiangshu.Kernel.Nudge

open System.Text.RegularExpressions

/// Which kind of nudge, if any, a session needs right now.
type NudgeAction = NudgeTodo | NudgeLoop | NudgeRunner | NudgeNone

let ofString = function
    | "nudge-todo" -> Ok NudgeTodo
    | "nudge-loop" -> Ok NudgeLoop
    | "nudge-runner" -> Ok NudgeRunner
    | "none" -> Ok NudgeNone
    | other -> Error $"Invalid NudgeAction: \"{other}\""

let toString = function
    | NudgeTodo -> "nudge-todo" | NudgeLoop -> "nudge-loop"
    | NudgeRunner -> "nudge-runner" | NudgeNone -> "none"

let skipTodoRe = Regex(@"<skip-todo-check\s*\/?>", RegexOptions.IgnoreCase)
let skipLoopRe = Regex(@"<skip-loop-check\s*\/?>", RegexOptions.IgnoreCase)
let questionRe = Regex(@"\?\s*$")

/// Detect whether position is inside a markdown code fence (``` or ~~~).
let private isInsideCodeFence (text: string) : bool =
    text.Split('\n')
    |> Array.fold (fun inFence line ->
        let trimmed = line.TrimStart()
        if trimmed.StartsWith("```") || trimmed.StartsWith("~~~") then not inFence
        else inFence) false

let private outsideCodeFence (text: string) (predicate: string -> bool) =
    not (isInsideCodeFence text) && predicate text

let internal isQuestion (text: string) = outsideCodeFence text questionRe.IsMatch
let internal skipsTodo (text: string) = outsideCodeFence text skipTodoRe.IsMatch
let internal skipsLoop (text: string) = outsideCodeFence text skipLoopRe.IsMatch
