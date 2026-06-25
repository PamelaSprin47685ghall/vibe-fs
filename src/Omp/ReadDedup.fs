module VibeFs.Omp.ReadDedup

open Fable.Core.JsInterop
open VibeFs.Kernel.MessageDedup
open VibeFs.Kernel.ToolOutputInfo
open VibeFs.Shell.Dyn
open VibeFs.Shell.TreeSitterShell

module Dyn = VibeFs.Shell.Dyn

let private setOutput (state: obj) (v: string) : unit = state?output <- v

let applyReadDedup (entries: obj array) : unit =
    if Dyn.isNullish entries || not (Dyn.isArray entries) then ()
    else
        let seenByPath = createObj []
        for i = 0 to entries.Length - 1 do
            let entry = entries.[i]
            if not (Dyn.isNullish entry) then
                let partsObj : obj = Dyn.get entry "parts"
                if not (Dyn.isNullish partsObj) && Dyn.isArray partsObj then
                    let partsArr = unbox<obj array> partsObj
                    for j = 0 to partsArr.Length - 1 do
                        let part = partsArr.[j]
                        if not (Dyn.isNullish part)
                           && Dyn.str part "type" = "tool"
                           && Dyn.str part "tool" = "read" then
                            let state = Dyn.get part "state"
                            if not (Dyn.isNullish state) then
                                let output = Dyn.get state "output"
                                if not (Dyn.isNullish output) && Dyn.typeIs output "string" then
                                    let currentOutput = string output
                                    let pathKey =
                                        match extractFilePaths (Dyn.get state "input") with
                                        | path :: _ -> path
                                        | [] -> ""
                                    let payload = { path = pathKey; content = currentOutput }
                                    let pathState =
                                        let existing = Dyn.get seenByPath pathKey
                                        if Dyn.isNullish existing then { seenContents = [] }
                                        else unbox<DedupState> existing
                                    let verdict, nextState = processDedup pathState payload
                                    seenByPath?(pathKey) <- box nextState
                                    match verdict with
                                    | AlreadySeen -> setOutput state (noChangeEnvelope ())
                                    | NewContent _ -> ()