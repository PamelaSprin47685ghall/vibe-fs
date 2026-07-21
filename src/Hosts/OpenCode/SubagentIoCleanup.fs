module Wanxiangshu.Hosts.Opencode.SubagentIoCleanup

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.SubsessionEventStore
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime.SubsessionPendingEvidence

open Wanxiangshu.Hosts.Opencode.SubagentTypes
open Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterOps
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.Messaging.OpencodeSessionEventCodec

let abortedPrefix = "(aborted)"
let noOutputText = "(no output)"

let forgetSubagent
    (registry: ChildAgentRegistry)
    (directory: string)
    (childID: string)
    : unit =
    SubsessionPendingEvidence.ForgetSession childID
    SubsessionActorRegistry.Remove directory childID
    registry.UnregisterChildAgent(childID)

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
                do! SubsessionHostAdapterOps.deleteSession client directory childID

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
    (_client: obj)
    (directory: string)
    (childID: string)
    : JS.Promise<unit> =
    promise {
        if cleanup then
            forgetSubagent registry directory childID
    }
