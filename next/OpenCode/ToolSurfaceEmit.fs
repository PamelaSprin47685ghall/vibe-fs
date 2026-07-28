namespace Wanxiangshu.Next.OpenCode

#nowarn "3511"

open System
open Fable.Core
open Fable.Core.JsInterop

/// Shared JS interop primitives for the tool surface modules.
module ToolSurfaceEmit =

    [<Emit("$0.schema.string()")>]
    let stringSchema (tool: obj) : obj = jsNative

    [<Emit("$0.schema.string().describe($1)")>]
    let describedStringSchema (tool: obj) (description: string) : obj = jsNative

    [<Emit("$0.schema.enum($1)")>]
    let enumSchema (tool: obj) (values: string array) : obj = jsNative

    [<Emit("$0.schema.enum($1).optional()")>]
    let optionalEnumSchema (tool: obj) (values: string array) : obj = jsNative

    /// Creatable managed agents as enum, plus plain string for existing handle IDs.
    [<Emit("$0.schema.union([$0.schema.enum($1), $0.schema.string()])")>]
    let managedAgentOrHandleSchema (tool: obj) (values: string array) : obj = jsNative

    [<Emit("$0.schema.string().optional()")>]
    let optionalStringSchema (tool: obj) : obj = jsNative

    let looksLikeHandleId (value: string) =
        if String.IsNullOrWhiteSpace value then
            false
        else
            let trimmed = value.Trim()

            trimmed.Length = 6
            && trimmed
               |> Seq.forall (fun c -> (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))

    let forkResultPayload (agentId: string) (managed: ManagedAgent) =
        let peer = ManagedAgent.peer managed

        createObj
            [ "agentId", box agentId
              "agent", box managed.Name
              "role", box (ManagedAgent.roleName managed.Role)
              "tier", box (ManagedAgent.tierName managed.Tier)
              "fallbackPeer", box peer.Name ]

    let managedMetaPayload (managed: ManagedAgent) =
        let peer = ManagedAgent.peer managed

        [ "agent", box managed.Name
          "role", box (ManagedAgent.roleName managed.Role)
          "tier", box (ManagedAgent.tierName managed.Tier)
          "fallbackPeer", box peer.Name ]

    [<Emit("$0($1)")>]
    let applyTool (factory: obj) (definition: obj) : obj = jsNative

    [<Emit("(args, context) => $0(args)(context)")>]
    let uncurriedExecute (fn: obj) : obj = jsNative

    [<Emit("Math.random().toString(36).slice(2, 8)")>]
    let newAgentId () : string = jsNative

    [<Emit("JSON.stringify($0)")>]
    let stringify (value: obj) : string = jsNative

    /// Attach a one-shot callback to an AbortSignal-like object.  The callback
    /// is invoked immediately if the signal is already aborted.  Returns a
    /// detacher that removes the listener.  If the context carries no usable
    /// signal, returns a no-op detacher.
    let attachAbort (context: obj) (callback: unit -> unit) : (unit -> unit) =
        let signal =
            if isNull context then
                null
            else
                let a = context?("abort")

                if not (isNull a) then
                    a
                else
                    let b = context?("abortSignal")
                    if not (isNull b) then b else context?("signal")

        if isNull signal || isNull (signal?("addEventListener")) then
            fun () -> ()
        else
            let aborted = signal?("aborted")

            if not (isNull aborted) && unbox<bool> aborted then
                callback ()
                fun () -> ()
            else
                let cb = fun (_: obj) -> callback ()
                signal?addEventListener ("abort", cb, createObj [ "once", box true ])
                fun () -> signal?removeEventListener ("abort", cb)

    let contextString (ctx: obj) (name: string) =
        if isNull ctx || isNull ctx?(name) then
            None
        else
            let v = unbox<string> ctx?(name) in if String.IsNullOrWhiteSpace v then None else Some v

    let textArg (args: obj) (name: string) =
        if isNull args || isNull args?(name) then
            ""
        else
            unbox<string> args?(name)

    let optionalTextArg (args: obj) (name: string) =
        let value = textArg args name
        if String.IsNullOrWhiteSpace value then None else Some value
