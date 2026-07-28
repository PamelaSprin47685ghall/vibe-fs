namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// Host built-in retry reuses the same physical user message. chat.params sees
/// the final provider-bound model for every attempt; inject EffectiveModel from
/// DurableFallback so same-run provider requests are A→A→B→B.
module ChatParamsHook =

    let create (journal: AgentJournal option) (modelConfig: ModelResolver.ModelConfig option) : obj =
        box (fun (inputObj: obj) (outputObj: obj) ->
            if isNull inputObj then
                ()
            else
                let sessionId =
                    if isNull inputObj?sessionID then
                        ""
                    else
                        unbox<string> inputObj?sessionID

                if String.IsNullOrWhiteSpace sessionId then
                    ()
                else
                    match journal, modelConfig with
                    | None, _
                    | _, None -> ()
                    | Some j, Some cfg ->
                        let sid = SessionId.create sessionId
                        let projection = AgentJournal.snapshot j

                        match ModelResolver.resolveForSession cfg sid projection with
                        | None -> ()
                        | Some model ->
                            // Host Model object shape varies; overwrite common id fields.
                            if not (isNull inputObj?model) then
                                inputObj?model?providerID <- model.providerID
                                inputObj?model?modelID <- model.modelID
                                inputObj?model?id <- model.modelID

                            if not (isNull outputObj) && not (isNull outputObj?options) then
                                // Some hosts read provider model override from options.
                                outputObj?options?wanxiangshu_effective_provider <- model.providerID
                                outputObj?options?wanxiangshu_effective_model <- model.modelID)
