module Wanxiangshu.Hosts.Opencode.SubagentIoCleanup

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.SubsessionEventStore
open Wanxiangshu.Runtime.SubsessionActorRegistry

open Wanxiangshu.Hosts.Opencode.SubagentTypes
open Wanxiangshu.Runtime.OpencodeClientCodec

let abortedPrefix = "(aborted)"
let noOutputText = "(no output)"

let abortAndUnregister
    (registry: ChildAgentRegistry)
    (client: obj)
    (directory: string)
    (childID: string)
    : JS.Promise<unit> =
    promise {
        match getSessionApiFromClient client with
        | Ok session ->
            try
                let! _ = invoke1 (box {| path = box {| id = childID |} |}) "abort" session
                ()
            with _ ->
                ()

            try
                let arg = box {| path = box {| id = childID |} |}
                let! _ = invoke1 arg "delete" session

                let sid = SessionId.create childID
                let eventStore = create directory
                do! eventStore.Append(sid, [ PhysicalSessionClosed sid ])

                SubsessionActorRegistry.ClearPoison directory childID

                SubsessionActorRegistry.Remove directory childID

                registry.UnregisterChildAgent(childID)
            with _ ->
                ()
        | Error _ -> ()
    }

let cleanupChildIfRequested
    (registry: ChildAgentRegistry)
    (cleanup: bool)
    (client: obj)
    (directory: string)
    (childID: string)
    : JS.Promise<unit> =
    promise {
        if cleanup then
            do! abortAndUnregister registry client directory childID
    }
