module Wanxiangshu.Hosts.Mux.DelegateTimeout

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Runtime.SubagentSpawn

module Dyn = Wanxiangshu.Runtime.Dyn

type DelegateOutcome =
    | Report of string
    | TimedOut

let delegateWithTimeout
    (delegateToSubAgent: obj -> obj -> string -> string -> string -> obj option -> JS.Promise<string>)
    (deps: obj)
    (config: obj)
    (agentId: string)
    (prompt: string)
    (title: string)
    (options: obj option)
    (timeoutMs: int)
    : JS.Promise<DelegateOutcome> =
    promise {
        let controller = AbortController()
        let signal = controller.signal
        let configWithSignal = Dyn.withKey config "abortSignal" signal

        let workPromise: JS.Promise<DelegateOutcome> =
            promise {
                let! report = delegateToSubAgent deps configWithSignal agentId prompt title options
                return Report report
            }

        let timeoutPromise: JS.Promise<DelegateOutcome> =
            promise {
                do! Promise.sleep timeoutMs
                controller.abort ()
                return TimedOut
            }

        try
            let! winner = Promise.race [| workPromise; timeoutPromise |]
            return winner
        with err ->
            match translateJsError err with
            | MessageAborted -> return TimedOut
            | _ -> return! Promise.reject err
    }
