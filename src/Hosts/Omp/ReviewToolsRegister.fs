module Wanxiangshu.Hosts.Omp.ReviewToolsRegister

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.ReviewPrompts
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.ExecutorTools
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Hosts.Omp.OmpToolSchema
open Wanxiangshu.Hosts.Omp.ReviewLoop
open Wanxiangshu.Hosts.Omp.ReviewToolsLoop
open Wanxiangshu.Hosts.Omp.Schema
open Wanxiangshu.Runtime.DynField
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ReviewEventWriter
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Hosts.Omp.ReviewToolsExecute

module Dyn = Wanxiangshu.Runtime.Dyn

let private description (name: string) : string =
    match Wanxiangshu.Kernel.ToolCatalog.description name with
    | Ok d -> d
    | Error e -> failwith e

open Wanxiangshu.Runtime.ReviewRuntime

let private optBool (o: obj) (key: string) : bool option =
    let v = Dyn.get o key
    if Dyn.isNullish v then None else Some(unbox<bool> v)

let registerLoopFeatures (pi: obj) (store: ReviewStore) : unit =
    let tb = Dyn.get pi "typebox"

    pi?registerCommand (
        loopCommand,
        createObj
            [ "description", box "Enable loop review mode for the current session"
              "handler", box (fun (args: string) (ctx: obj) -> handleLoopCommand pi store args ctx) ]
    )

    pi?registerTool (
        createObj
            [ "name", box "submit_review"
              "label", box "Submit Review"
              "description", box (description "submit_review")
              "parameters",
              objectOf
                  [| ("report", str "Detailed description of what was changed." tb)
                     ("affectedFiles", stringArraySchema pi "Modified or created file path.")
                     ("wip",
                      opt
                          "Defaults to true: record progress without starting a reviewer. Set to false to start the reviewer for final review."
                          tb
                          bool_) |]
                  tb
              "execute",
              box (
                  System.Func<string, obj, obj, obj, obj, JS.Promise<ToolResult>>(fun id p s u c ->
                      ReviewToolsExecute.executeSubmitReview (store, pi, c, id, p, s, u))
              ) ]
    )

    pi?registerTool (
        createObj
            [ "name", box "return_reviewer"
              "label", box "Return Reviewer"
              "description", box (description "return_reviewer")
              "defaultInactive", box true
              "parameters", returnReviewerParameters tb
              "execute",
              box (
                  System.Func<string, obj, obj, obj, obj, JS.Promise<ToolResult>>(fun id p s u c ->
                      ReviewToolsExecute.executeReturnReviewer (store, c, id, p, s, u))
              ) ]
    )

let registerInputHandler (pi: obj) (store: ReviewStore) : unit =
    pi?on (
        "input",
        box (fun (event: obj) (ctx: obj) ->
            promise {
                let text = (Dyn.str event "text").Trim()

                if not (text.StartsWith("/" + loopCommand)) then
                    return box null
                else
                    let rest = text.Substring(loopCommand.Length + 1).Trim()
                    do! handleLoopCommand pi store rest ctx
                    return createObj [ "handled", box true ]
            })
    )
