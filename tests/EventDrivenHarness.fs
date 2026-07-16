/// Event-driven test primitives.
module Wanxiangshu.Tests.EventDrivenHarness

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Hosts.Opencode.Plugin
open Wanxiangshu.Runtime.Dyn

let yieldMicrotask () : JS.Promise<unit> = Promise.lift ()

let rec drainMicrotasks (n: int) : JS.Promise<unit> =
    promise {
        if n <= 0 then
            ()
        else
            do! yieldMicrotask ()
            do! drainMicrotasks (n - 1)
    }

type EventBus<'e>() =
    let mutable nextSeq = 0UL
    let store = ResizeArray<uint64 * 'e>()

    member _.emit(e: 'e) : uint64 =
        let s = nextSeq
        nextSeq <- nextSeq + 1UL
        store.Add(s, e)
        s

    member _.snapshot() : (uint64 * 'e) list = store |> Seq.toList
    member _.count() : int = store.Count
    member _.items() : 'e list = store |> Seq.map snd |> Seq.toList

    member _.clear() =
        nextSeq <- 0UL
        store.Clear()

type NudgeKind =
    | LoopNudge
    | TodoNudge
    | ToolResultNudge
    | OtherNudge

type NudgeEvent =
    { kind: NudgeKind
      text: string
      sessionID: string }

type PromptEvent = { sessionID: string; body: obj }

type OpencodeHarness(pluginObj: obj, promptBus: EventBus<PromptEvent>) =
    member _.plugin = pluginObj
    member _.promptBus = promptBus

    member this.inject (eventType: string) (properties: obj) : JS.Promise<unit> =
        let eh = get pluginObj "event"

        eh
        $ (createObj [ "event", box (createObj [ "type", box eventType; "properties", box properties ]) ])
        |> unbox<JS.Promise<unit>>

    member this.injectEvent(eventObj: obj) : JS.Promise<unit> =
        let eh = get pluginObj "event"
        eh $ eventObj |> unbox<JS.Promise<unit>>

let mkOpencodePromptClient (promptBus: EventBus<PromptEvent>) (messagesRef: obj array ref) : obj =
    createObj
        [ "session",
          box (
              createObj
                  [ "todo", box (System.Func<unit, JS.Promise<obj>>(fun () -> promise { return box {| data = [||] |} }))
                    "messages",
                    box (
                        System.Func<unit, JS.Promise<obj>>(fun () ->
                            promise { return box {| data = messagesRef.Value |} })
                    )
                    "prompt",
                    box (
                        System.Func<obj, JS.Promise<unit>>(fun arg ->
                            let sessionID = str (get arg "path") "id"
                            let body = get arg "body"
                            promptBus.emit { sessionID = sessionID; body = body } |> ignore
                            Promise.lift ())
                    ) ]
          ) ]

let mkOpencodeHarness (workspaceDir: string) (messagesRef: obj array ref) : JS.Promise<OpencodeHarness> =
    promise {
        let bus = EventBus<PromptEvent>()
        let client = mkOpencodePromptClient bus messagesRef

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = client |}
            )

        return OpencodeHarness(p, bus)
    }

type MuxNudgeHelpers(nudgeBus: EventBus<NudgeEvent>, todos: string list, historyRef: obj array ref, sessionID: string) =
    member _.getTodos() : JS.Promise<obj> =
        promise { return box (todos |> List.toArray) }

    member _.nudge (_ws: obj) (msg: obj) : JS.Promise<bool> =
        let text = string msg

        let kind =
            if text.Contains "loop" then LoopNudge
            elif text.Contains "todo" then TodoNudge
            elif text.Contains "tool" then ToolResultNudge
            else OtherNudge

        nudgeBus.emit
            { kind = kind
              text = text
              sessionID = sessionID }
        |> ignore

        historyRef.Value <-
            Array.append
                (historyRef.Value)
                [| box
                       {| id = sprintf "%s-nudge-%d" sessionID (nudgeBus.count ())
                          role = "user"
                          parts =
                           [| box
                                  {| ``type`` = "text"
                                     text = text
                                     state = "done" |} |] |} |]

        promise { return true }

    member self.asHelpersObj() : obj =
        createObj
            [ "getTodos", box (System.Func<obj, JS.Promise<obj>>(fun _ -> self.getTodos ()))
              "nudge", box (System.Func<obj, obj, JS.Promise<bool>>(fun ws msg -> self.nudge ws msg)) ]

type MuxHarness(reg: obj, nudgeBus: EventBus<NudgeEvent>, sessionID: string) =
    member _.reg = reg
    member _.nudges = nudgeBus
    member _.hooks = get reg "eventHook"

    member this.streamEnd(parts: obj) : JS.Promise<unit> =
        let hook = this.hooks

        let ev =
            createObj
                [ "type", box "stream-end"
                  "workspaceId", box sessionID
                  "properties", box (createObj [ "parts", box parts ]) ]

        let helpers = MuxNudgeHelpers(nudgeBus, [], ref [||], sessionID).asHelpersObj ()
        hook $ (ev, helpers) |> unbox<JS.Promise<unit>>

    member this.streamEndWith (historyRef: obj array ref) (todos: string list) (parts: obj) : JS.Promise<unit> =
        let hook = this.hooks

        let ev =
            createObj
                [ "type", box "stream-end"
                  "workspaceId", box sessionID
                  "properties", box (createObj [ "parts", box parts ]) ]

        let helpers =
            MuxNudgeHelpers(nudgeBus, todos, historyRef, sessionID).asHelpersObj ()

        hook $ (ev, helpers) |> unbox<JS.Promise<unit>>

let assertEventSequence<'e> (label: string) (bus: EventBus<'e>) (expected: ('e -> bool) list) : unit =
    let actual = bus.items ()

    if expected.Length > actual.Length then
        let detail =
            sprintf "not enough events: expected %d, got %d" expected.Length actual.Length

        check (label + " | " + detail) false
    else
        let mutable ok = true
        let mutable detail = ""

        for i in 0 .. expected.Length - 1 do
            if not (expected.[i] actual.[i]) then
                ok <- false
                detail <- sprintf "event[%d] mismatch" i

        if not ok then
            detail <- sprintf "expected first %d events of %d: %s" expected.Length actual.Length detail

        if ok then
            check label true
        else
            check (label + " | " + detail) false

let driveAndAssert<'e> (label: string) (bus: EventBus<'e>) (n: int) (expected: ('e -> bool) list) : JS.Promise<unit> =
    promise {
        do! drainMicrotasks n
        assertEventSequence label bus expected
    }

let loopNudgeTextP (expected: string) (ev: NudgeEvent) : bool =
    ev.kind = LoopNudge && ev.text = expected

let anyLoopNudgeP (_ev: NudgeEvent) : bool = true

let snapshotLength (bus: EventBus<'e>) : int = bus.count ()
