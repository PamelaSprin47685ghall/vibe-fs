module Wanxiangshu.Shell.MessageTransformPipeline

open Fable.Core
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.MessageTransformCore

type MessageTransformPlan = {
    SessionID: string
    Agent: string
    Directory: string
    Excluded: bool
    Cleaned: Message<obj> list
}

let runMessageTransformPipeline
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (encodeMessages: Message<obj> list -> obj array)
    (dedupFn: bool -> obj array -> obj array)
    (loadCaps: unit -> JS.Promise<CapsFile list>)
    (buildCaps: obj array -> CapsFile list -> string option -> obj array)
    : JS.Promise<obj array> =
    promise {
        if plan.Cleaned.IsEmpty then return [||]
        else
            let afterBacklog =
                applyBacklogProjection plan.SessionID plan.Excluded backlogOps plan.Cleaned
            let encoded = encodeMessages afterBacklog
            let deduped = dedupFn plan.Excluded encoded
            let! capsFiles = loadCaps ()
            return buildCaps deduped capsFiles None
    }