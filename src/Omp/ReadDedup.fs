module Wanxiangshu.Omp.ReadDedup

open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Dedup
open Wanxiangshu.Kernel.MessageDedup
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.ReadDedupCore
open Wanxiangshu.Shell.TreeSitterShell

module Dyn = Wanxiangshu.Shell.Dyn

let private setOutput (state: obj) (v: string) : unit = state?output <- v

let applyReadDedup (entries: obj array) : unit =
    if Dyn.isNullish entries || not (Dyn.isArray entries) then
        ()
    else
        let getParts (msg: obj) =
            let parts: obj = Dyn.get msg "parts"

            if Dyn.isNullish parts || not (Dyn.isArray parts) then
                [||]
            else
                unbox<obj array> parts

        let getHit i j part =
            if
                not (Dyn.isNullish part)
                && Dyn.str part "type" = "tool"
                && Dyn.str part "tool" = "read"
            then
                let state = Dyn.get part "state"

                if not (Dyn.isNullish state) then
                    let output = Dyn.get state "output"

                    if
                        not (Dyn.isNullish output)
                        && Dyn.typeIs output "string"
                        && not (isNoChangeOutput (string output))
                    then
                        let pathKey =
                            match extractFilePaths (Dyn.get state "input") with
                            | path :: _ -> path
                            | [] -> ""

                        Some
                            { msgIndex = i
                              partIndex = j
                              payload =
                                { path = pathKey
                                  content = string output } }
                    else
                        None
                else
                    None
            else
                None

        let verdicts, _ = processDedupHits Map.empty entries getParts getHit

        for hit, verdict in verdicts do
            match verdict with
            | AlreadySeen ->
                let entry = entries.[hit.msgIndex]
                let parts = unbox<obj array> (Dyn.get entry "parts")
                let part = parts.[hit.partIndex]
                setOutput (Dyn.get part "state") (noChangeEnvelope ())
            | NewContent _ -> ()
