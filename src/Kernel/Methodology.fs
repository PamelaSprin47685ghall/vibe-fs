module Wanxiangshu.Kernel.Methodology

open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.MethodologyCatalog

let selectMethodologyToolName = "select_methodology"

let methodologyToolResultText (methodologies: string list) =
    match methodologies with
    | [] -> invalidArg "methodologies" "methodologyToolResultText requires at least one methodology"
    | _ ->
        let joined = String.concat ", " methodologies
        $"Great! Now please explain how to apply [{joined}] to the work step."

let todoResultText (methodologies: string list) : string =
    match methodologies with
    | [] -> "Todos updated."
    | _ ->
        let joined = String.concat ", " methodologies
        $"Great! Please apply [{joined}] to the subsequent steps. If all work has been fully completed and this is purely a summary/handover, you do NOT need to call the methodology tool. Otherwise, for any remaining tasks, the more difficult/complex the problem is, the more critical it is to think over and call the methodology tool with [{joined}] selected to guide your next work step."

let methodologyCatalog = Wanxiangshu.Kernel.MethodologyCatalog.methodologyCatalog

let selectMethodologyFieldDescription =
    "Required when calling this tool: record `select_methodology` with one or more methodology names that must guide the next work step. Choose by definitions, not by keyword vibes.\n\n"
    + methodologyCatalog
