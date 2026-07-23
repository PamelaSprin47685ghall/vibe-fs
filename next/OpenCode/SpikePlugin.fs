namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

type SpikePluginConfig =
    { Directory: string
      Port: IOpenCodePort option }

module SpikePlugin =

    let createSpikeHost (portOpt: IOpenCodePort option) =
        let eventPort = Events.DeterministicEventPort() :> IEventObservationPort
        let sessionPort = InjectedSessionPort(portOpt, eventPort) :> ISessionHostPort
        eventPort, sessionPort

    let handleTransform (rawOutObj: obj) =
        if not (isNull rawOutObj) && not (isNull rawOutObj?messages) then
            let rawMsgs = unbox<obj list> rawOutObj?messages
            let canonMsgs = Projection.projectMessages rawMsgs

            let capsMsg =
                createObj [ "role", box "system"; "text", box "[CAPS: coder, inspector, browser]" ]

            let transformed = Projection.preserveRawTail [ capsMsg ] rawMsgs
            rawOutObj?messages <- List.toArray transformed

    let initSpikePlugin (input: obj) : Task<obj> =
        task {
            let portOpt = OpenCodePort.create input
            let eventPort, sessionPort = createSpikeHost portOpt

            let hooks =
                {| projection = Projection.projectMessages
                   events = eventPort
                   sessions = sessionPort
                   ``chat.transform`` = fun (inObj: obj) (outObj: obj) -> handleTransform outObj
                   ``experimental.chat.messages.transform`` = fun (inObj: obj) (outObj: obj) -> handleTransform outObj
                   config = fun (config: obj) -> () |}

            return box hooks
        }
