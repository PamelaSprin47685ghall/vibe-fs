module Wanxiangshu.Mux.WrappersReview

open Fable.Core
open Fable.Core.JsInterop
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.DynField
open Wanxiangshu.Shell.MuxHostBindings
open Wanxiangshu.Shell.DelegateToolsCodec

let private strField = Wanxiangshu.Shell.DynField.strField

/// Encapsulates the host's native file_read execute function captured during
/// wrapper registration. Replaces the old `obj option ref` pseudo-interface
/// (REFACTOR.md §12): the mutable slot is private, callers go through methods.
type HostFunctionCapture() =
    let mutable captured : obj option = None
    member _.Capture(fn: obj) : unit = captured <- Some fn
    member _.TryGet() : obj option = captured

let mkFileReadCapture (hostReadExec: HostFunctionCapture) : obj =
    let wrapperFn =
        System.Func<obj, obj, obj>(fun (hostTool: obj) (_config: obj) ->
            hostReadExec.Capture(getToolExecute hostTool)
            let execFn =
                System.Func<obj, obj, JS.Promise<string>>(fun (_args: obj) (_opts: obj) ->
                    Promise.lift "disabled")
            createObj [ "execute", box execFn ])
    createObj [ "targetTool", box "file_read"; "wrapper", box wrapperFn ]

let private reviewerAgentReportDefinition () : obj =
    createObj
        [ "name", box "agent_report"
          "description", box muxReviewerAgentReportDescription
          "parameters",
              box (createObj
                  [ "type", box "object"
                    "properties",
                        box (createObj
                            [ "verdict", box (createObj [ "type", box "string"; "enum", box [| "PASS"; "REJECT" |]; "description", box "PASS accepts the work; REJECT sends actionable feedback." ])
                              "feedback", box (createObj [ "type", box "string"; "description", box "Detailed actionable feedback. Empty string when passing." ]) ])
                    "required", box [| "verdict"; "feedback" |]
                    "additionalProperties", box false ]) ]

let private formatReviewerAgentReportMarkdown (args: obj) : string =
    let verdict = defaultArg (strField args "verdict") "" |> fun value -> value.Trim().ToUpperInvariant()
    let feedback = defaultArg (strField args "feedback") "" |> fun value -> value.Trim()
    if verdict = "REJECT" || feedback <> "" then
        if feedback = "" then "REJECT: No feedback provided."
        else "REJECT: " + feedback
    else
        "PASS"

let private reviewerAgentReportPayload (args: obj) : obj =
    createObj [ "reportMarkdown", box (formatReviewerAgentReportMarkdown args) ]

let private isThenable (value: obj) : bool =
    not (Dyn.isNullish value) && Dyn.typeIs (Dyn.get value "then") "function"

let mkAgentReportOverride () : obj =
    let wrapperFn =
        System.Func<obj, obj, obj>(fun (tool: obj) (config: obj) ->
            if decodeSubagentRole config <> "reviewer" then
                tool
            else
                let definition = reviewerAgentReportDefinition ()
                let execFn =
                    System.Func<obj, obj, JS.Promise<obj>>(fun (args: obj) (opts: obj) ->
                        promise {
                            let upstreamArgs = reviewerAgentReportPayload args
                            let raw = invokeToolExecute tool upstreamArgs opts
                            let! result = if isThenable raw then unbox<JS.Promise<obj>> raw else Promise.lift raw
                            if Dyn.typeIs result "object" && Dyn.truthy (Dyn.get result "success") then
                                return Dyn.withKey result "report" (box upstreamArgs)
                            else
                                return result
                        })
                createObj [ "description", box (Dyn.get definition "description")
                            "parameters", box (Dyn.get definition "parameters")
                            "execute", box execFn ])
    createObj [ "targetTool", box "agent_report"; "wrapper", box wrapperFn ]
