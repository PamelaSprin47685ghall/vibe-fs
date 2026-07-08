module Wanxiangshu.Shell.MessageTransformPipeline

open Fable.Core
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.MessageTransformCore

type MessageTransformPlan =
    { SessionID: string
      Agent: string
      Directory: string
      Excluded: bool
      IsSubagentSession: bool
      Cleaned: Message<obj> list
      RawArray: obj array option }

let runMessageTransformPipeline
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (encodeMessages: Message<obj> list -> obj array)
    (injectFn: bool -> obj array -> JS.Promise<obj array>)
    (loadCaps: unit -> JS.Promise<CapsFile list>)
    (buildCaps: obj array -> CapsFile list -> string option -> obj array)
    : JS.Promise<obj array> =
    promise {
        if plan.Cleaned.IsEmpty then
            return [||]
        else
            let afterAmend =
                AmendFilter.filterAmendMessages
                    (fun raw ->
                        match DynField.optField raw "amend" with
                        | None -> None
                        | Some v ->
                            match v with
                            | :? int as n when n > 0 -> Some n
                            | :? float as f when f > 0.0 -> Some(int f)
                            | _ -> None)
                    plan.Cleaned

            let afterBacklog =
                applyBacklogProjection plan.SessionID plan.Excluded backlogOps afterAmend

            let encoded = encodeMessages afterBacklog
            let! injected = injectFn plan.Excluded encoded
            let! capsFiles = loadCaps ()
            return buildCaps injected capsFiles None
    }
