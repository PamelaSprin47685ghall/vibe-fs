namespace Wanxiangshu.Process

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

module private PtyRecoveryNative =
    [<ImportAll("node:fs")>]
    let files: obj = jsNative

    [<ImportAll("node:net")>]
    let sockets: obj = jsNative

    [<ImportAll("node:crypto")>]
    let crypto: obj = jsNative

    [<ImportAll("node:child_process")>]
    let processes: obj = jsNative

    [<ImportAll("node:os")>]
    let system: obj = jsNative

    [<Import("join", "node:path")>]
    let join (directory: string) (name: string) : string = jsNative

    [<Import("dirname", "node:path")>]
    let parent (path: string) : string = jsNative

    [<Import("fileURLToPath", "node:url")>]
    let filePath (_url: string) : string = jsNative

    [<Emit("import.meta.url")>]
    let moduleUrl: string = jsNative

    [<Emit("process")>]
    let runtime: obj = jsNative

    [<Emit("performance.now()")>]
    let now () : float = jsNative

    [<Emit("new Promise(resolve => setTimeout(resolve, $0))")>]
    let delay (_milliseconds: int) : Task<unit> = jsNative

    [<Emit("setTimeout($0, $1)")>]
    let timer (_action: unit -> unit) (_milliseconds: int) : obj = jsNative

    [<Emit("clearTimeout($0)")>]
    let clearTimer (_timer: obj) : unit = jsNative

    [<Emit("$0 === null || $0 === undefined")>]
    let absent (_value: obj) : bool = jsNative

    [<Emit("$0.on($1, $2)")>]
    let on (_source: obj) (_event: string) (_callback: obj -> unit) : unit = jsNative

    [<Emit("$0 instanceof Error ? $0.message : String($0)")>]
    let message (_error: obj) : string = jsNative

    [<Emit("""
      (() => {
        if ($0.destroyed || !$0.writable || $0.writableLength > 4194304) {
          $2('PTY control connection is unavailable or stalled');
          return;
        }
        $0.write(JSON.stringify($1) + '\n', error => { if (error) $2(String(error.message || error)); });
      })()
    """)>]
    let send (_socket: obj) (_frame: obj) (_failed: string -> unit) : unit = jsNative

    [<Emit("$0 !== null && typeof $0 === 'object' && (($0.signal === null && Number.isInteger($0.code) && $0.code >= 0) || ($0.code === null && typeof $0.signal === 'string' && $0.signal.startsWith('SIG')))")>]
    let validExit (_value: obj) : bool = jsNative

    [<Emit("$0 === undefined ? null : $0")>]
    let originalBunMode (_mode: obj) : obj = jsNative

    [<Emit("$0")>]
    let function2 (_callback: 'a -> 'b -> 'c) : obj = jsNative

    [<Emit("$0.randomBytes(32).toString('hex')")>]
    let randomToken (_provider: obj) : string = jsNative

    [<Emit("$0.createHmac('sha256', $1).update(JSON.stringify($2)).digest('hex')")>]
    let sign (_provider: obj) (_secret: string) (_value: obj) : string = jsNative

    [<Emit("$0.end()")>]
    let endSocket (_socket: obj) : unit = jsNative

    [<Emit("$0.end($1)")>]
    let finishSocket (_socket: obj) (_finished: unit -> unit) : unit = jsNative

    let objectOf fields = createObj fields

    let require condition reason =
        if not condition then
            invalidOp reason

    let token () = randomToken crypto

    let digest (secret: string) (value: obj) : string = sign crypto secret value

    let exists directory name : bool = files?existsSync (join directory name)

    let private unlinkEndpoint (request: obj) =
        if files?existsSync request?endpoint then
            (files?unlinkSync request?endpoint: unit)

    let removeEndpoint (request: obj) =
        try
            unlinkEndpoint request
            (files?rmdirSync (parent request?endpoint): unit)
        with _ ->
            ()

    let closeSocket socket =
        if not (absent socket) then
            socket?destroy () |> ignore

    let private deliverOne listener value =
        try
            listener value
        with _ ->
            ()

    let deliver (listeners: ResizeArray<'a -> unit>) value =
        for listener in listeners do
            deliverOne listener value

    let optional (value: obj) : 'a option =
        if absent value then None else Some(unbox value)

    let read directory name : obj =
        JS.JSON.parse (files?readFileSync (join directory name, "utf8"))

    let writeFile path flags value =
        let descriptor: obj = files?openSync (path, flags, 384)

        try
            (files?writeFileSync (descriptor, JS.JSON.stringify value): unit)
            (files?fsyncSync descriptor: unit)
        finally
            (files?closeSync descriptor: unit)

    let publish directory name value =
        let pending = join directory (name + "." + token () + ".pending")
        writeFile pending "wx" value
        (files?renameSync (pending, join directory name): unit)
        let descriptor: obj = files?openSync (directory, "r")

        try
            (files?fsyncSync descriptor: unit)
        finally
            (files?closeSync descriptor: unit)

    let receipt directory (request: obj) value =
        let payload =
            objectOf
                [ "version", box 1
                  "key", request?key
                  "incarnation", request?incarnation
                  "result", value ]

        publish directory "terminal" (objectOf [ "receipt", payload; "proof", box (digest request?secret payload) ])

    let terminal directory (request: obj) : obj option =
        if not (exists directory "terminal") then
            None
        else
            let stored = read directory "terminal"
            let payload: obj = stored?receipt
            require (not (absent payload)) "PTY terminal receipt is missing"
            require (stored?proof = digest request?secret payload) "PTY terminal proof mismatch"

            require
                (payload?version = 1
                 && payload?key = request?key
                 && payload?incarnation = request?incarnation)
                "PTY terminal instance mismatch"

            let result: obj = payload?result

            require
                (result?kind = "NotStarted" || (result?kind = "Ended" && validExit result?exit))
                "PTY terminal has no exact exit evidence"

            Some result

    let request directory key incarnation =
        let value = read directory "request"
        require (value?version = 1 && value?key = key) "PTY recovery key mismatch"
        require (not (String.IsNullOrWhiteSpace value?incarnation)) "PTY recovery incarnation is missing"

        incarnation
        |> Option.iter (fun expected -> require (value?incarnation = expected) "PTY recovery incarnation mismatch")

        value

    let frames socket receive failed =
        // DSL-MUTABLE: resource — retain incomplete socket frame text
        let mutable pending = ""
        socket?setEncoding ("utf8") |> ignore

        let parse chunk =
            pending <- pending + unbox<string> chunk
            require (pending.Length <= 4194304) "PTY control frame exceeds its limit"
            // DSL-MUTABLE: algorithm-scratch — scan complete frame delimiters
            let mutable delimiter = pending.IndexOf('\n')

            while delimiter >= 0 do
                let line = pending.Substring(0, delimiter)
                pending <- pending.Substring(delimiter + 1)
                receive (JS.JSON.parse line)
                delimiter <- pending.IndexOf('\n')

        let accept chunk =
            try
                parse chunk
            with error ->
                failed error.Message

        on socket "data" accept
        on socket "error" (message >> failed)

    let launch directory request =
        let executable =
            if absent runtime?versions?bun then
                box "bun"
            else
                runtime?execPath

        let environment = emitJsExpr () "({...process.env, BUN_BE_BUN: '1'})"

        let child: obj =
            processes?spawn (
                executable,
                [| filePath moduleUrl; "--wanxiangshu-pty-owner"; directory |],
                objectOf [ "detached", box true; "stdio", box "ignore"; "env", environment ]
            )

        on child "error" (fun error ->
            try
                receipt directory request (objectOf [ "kind", box "NotStarted"; "reason", box (message error) ])
            finally
                removeEndpoint request)

        child?unref () |> ignore

module private PtyRecoveryWorker =
    module N = PtyRecoveryNative

    let run directory =
        task {
            let request = N.read directory "request"

            let _ =
                (N.files?mkdirSync (N.join directory "claim", N.objectOf [ "mode", box 448 ]): unit)

            // DSL-MUTABLE: resource — own the authenticated control socket
            let mutable controller: obj = null
            // DSL-MUTABLE: resource — own the native PTY handle
            let mutable terminal: obj = null
            // DSL-MUTABLE: single-flight — admit the creator once
            let mutable creatorSeen = false
            // DSL-MUTABLE: retirement admission fence — seal the physical owner
            let mutable settled = false
            // DSL-MUTABLE: cancellation — retain the explicit stop request
            let mutable stopped = false
            // DSL-MUTABLE: resource — retain the authenticated controller mode
            let mutable attachedMode = ""
            // DSL-MUTABLE: resource — own the native state polling timer
            let mutable stateTimer: obj = null
            // DSL-MUTABLE: single-flight — publish startup acknowledgement once
            let mutable readySent = false
            // DSL-MUTABLE: resource — retain exact native exit evidence
            let mutable ended: obj = null
            let server: obj = N.sockets?createServer ()

            let send socket frame =
                N.send socket frame (fun _ -> socket?destroy () |> ignore)

            let notify frame =
                if not (N.absent controller) then
                    send controller frame

            let exitOwnerSoon () =
                N.timer
                    (fun () ->
                        N.removeEndpoint request
                        N.runtime?exit 0)
                    0
                |> ignore

            let closeOwner () =
                settled <- true
                N.clearTimer stateTimer
                server?close () |> ignore
                N.timer exitOwnerSoon 5000 |> ignore

                if N.absent controller then
                    exitOwnerSoon ()
                else
                    N.on controller "close" (fun _ -> exitOwnerSoon ())
                    N.finishSocket controller exitOwnerSoon

            let completeOwner result =
                notify (N.objectOf [ "kind", box "terminal"; "result", result ])
                closeOwner ()

            let endOwner result =
                N.receipt directory request result
                completeOwner result

            let fail reason =
                let reported =
                    try
                        N.publish
                            directory
                            "fault"
                            (N.objectOf [ "incarnation", request?incarnation; "reason", box reason ])

                        reason
                    with error ->
                        reason + "; PTY fault publication failed: " + error.Message

                notify (N.objectOf [ "kind", box "fault"; "reason", box reported ])

            let hasEndedReceipt () =
                try
                    N.terminal directory request
                    |> Option.exists (fun result ->
                        result?kind = "Ended"
                        && result?exit?code = ended?exit?code
                        && result?exit?signal = ended?exit?signal
                        && result?stopped = ended?stopped)
                with _ ->
                    false

            let publishEnded () =
                let published =
                    try
                        N.receipt directory request ended
                        true
                    with error ->
                        fail error.Message
                        false

                if published then
                    completeOwner ended
                elif hasEndedReceipt () then
                    closeOwner ()
                elif not (N.absent controller) then
                    N.endSocket controller

            let! loaded =
                task {
                    try
                        let! spawn = PtyScope.load () |> unbox<Task<obj>>
                        return Some spawn
                    with error ->
                        endOwner (N.objectOf [ "kind", box "NotStarted"; "reason", box error.Message ])
                        return None
                }

            let notifyStarted (state: obj) =
                if not readySent && (state?phase = "Running" || state?phase = "Draining") then
                    readySent <- true
                    notify (N.objectOf [ "kind", box "started"; "state", state ])

            let observeState () =
                if not (N.absent ended) && hasEndedReceipt () then
                    closeOwner ()

                if N.absent ended && not (N.absent terminal) then
                    notifyStarted (terminal?inspect ())

            let rec observe () =
                let interval = if N.absent ended then 10 else 100

                if not settled then
                    observeState ()
                    stateTimer <- N.timer observe interval

            let rememberExit (exit: obj) =
                if N.absent ended then
                    ended <-
                        N.objectOf
                            [ "kind", box "Ended"
                              "exit", N.objectOf [ "code", exit?code; "signal", exit?signal ]
                              "stopped", box stopped ]

            let start spawn =
                let options =
                    N.objectOf
                        [ "cwd", request?cwd
                          "name", box "xterm-256color"
                          "cols", box 80
                          "rows", box 24
                          "originalBunMode", request?bunMode ]

                terminal <- emitJsExpr (spawn, request?command, options) "$0($1, $2)"

                terminal?onData (fun data -> notify (N.objectOf [ "kind", box "data"; "data", data ]))
                |> ignore

                terminal?onFault fail |> ignore

                terminal?onExit (fun exit ->
                    if N.validExit exit then
                        rememberExit exit
                        publishEnded ()
                    else
                        fail "PTY owner has no native exit status")
                |> ignore

                observe ()

            let sendStarted socket (state: obj) =
                if state?phase = "Running" || state?phase = "Draining" then
                    send socket (N.objectOf [ "kind", box "started"; "state", state ])

            let sendState socket =
                if N.exists directory "fault" then
                    send socket (N.objectOf [ "kind", box "fault"; "reason", (N.read directory "fault")?reason ])
                elif not (N.absent terminal) then
                    sendStarted socket (terminal?inspect ())

            let attach socket (frame: obj) =
                N.require (not settled) "PTY owner is already sealed"
                N.require (N.absent controller) "PTY owner still has a live control connection"

                N.require
                    (frame?key = request?key
                     && frame?incarnation = request?incarnation
                     && frame?secret = request?secret)
                    "PTY owner credentials do not match"

                let mode: string = frame?mode
                N.require (mode = "creator" || mode = "recover") "Invalid PTY attachment mode"

                N.require
                    ((mode = "creator" && not creatorSeen) || (mode = "recover" && creatorSeen))
                    "PTY creator admission is unavailable"

                controller <- socket
                attachedMode <- mode

                if mode = "creator" then
                    creatorSeen <- true
                    loaded |> Option.iter start

                if mode = "recover" && not (N.absent ended) then
                    publishEnded ()
                else
                    send socket (N.objectOf [ "kind", box "attached" ])
                    sendState socket

            let applyControl (frame: obj) =
                match unbox<string> frame?kind with
                | "signal" ->
                    let signal: string = frame?signal

                    N.require
                        (attachedMode = "creator" || List.contains signal [ "SIGTERM"; "SIGKILL" ])
                        "PTY recovery permits termination signals only"

                    stopped <- stopped || List.contains signal [ "SIGTERM"; "SIGKILL"; "SIGINT" ]
                    terminal?signal signal |> ignore
                | "write" ->
                    N.require (attachedMode = "creator") "PTY recovery cannot replay input"
                    terminal?write frame?text |> ignore
                | "resize" ->
                    N.require (attachedMode = "creator") "PTY recovery cannot resume terminal interaction"
                    terminal?resize (frame?width, frame?height) |> ignore
                | "inspect" -> ()
                | _ -> invalidOp "Invalid PTY control action"

            let command socket (frame: obj) =
                N.require (not socket?destroyed && socket?writable) "PTY control connection is closed"

                if frame?kind = "attach" then
                    attach socket frame
                else
                    N.require (obj.ReferenceEquals(controller, socket)) "PTY control connection is not admitted"
                    N.require (not settled && not (N.absent terminal)) "PTY physical owner is unavailable"
                    N.require (N.absent ended) "PTY physical scope has ended and awaits terminal publication"
                    applyControl frame
                    send socket (N.objectOf [ "kind", box "ack"; "sequence", frame?sequence ])

            let detach socket =
                if obj.ReferenceEquals(controller, socket) then
                    controller <- null

            let accept socket =
                let rejected reason =
                    detach socket
                    send socket (N.objectOf [ "kind", box "rejected"; "reason", box reason ])
                    N.endSocket socket

                N.frames
                    socket
                    (fun frame ->
                        try
                            command socket frame
                        with error ->
                            rejected error.Message)
                    rejected

                N.on socket "close" (fun _ -> detach socket)

            N.on server "connection" accept
            N.on server "error" (N.message >> fail)

            let sealUnstarted () =
                if not creatorSeen && not settled then
                    endOwner (
                        N.objectOf [ "kind", box "NotStarted"; "reason", box "Creator never admitted the command" ]
                    )

            let listen _ =
                server?listen (
                    request?endpoint,
                    fun () ->
                        N.publish
                            directory
                            "available"
                            (N.objectOf [ "incarnation", request?incarnation; "owner", N.runtime?pid ])
                )
                |> ignore

                N.timer sealUnstarted 5000 |> ignore

            loaded |> Option.iter listen
        }

module internal PtyRecovery =
    module N = PtyRecoveryNative

    let private createClient directory (request: obj) mode =
        let exits = ResizeArray<obj -> unit>()
        let unstarted = ResizeArray<string -> unit>()
        let faults = ResizeArray<string -> unit>()
        let data = ResizeArray<string -> unit>()
        let pending = Queue<int * float * obj>()
        // DSL-MUTABLE: resource — own the PTY control connection
        let mutable socket: obj = null
        // DSL-MUTABLE: resource — retain the authenticated attach acknowledgement
        let mutable attached = false
        // DSL-MUTABLE: resource — retain the native startup acknowledgement
        let mutable started = false
        // DSL-MUTABLE: resource — retain the first control connection fault
        let mutable failure: string option = None
        // DSL-MUTABLE: resource — retain the verified durable exit receipt
        let mutable completion: obj = null
        // DSL-MUTABLE: algorithm-scratch — number pending control requests
        let mutable sequence = 0
        // DSL-MUTABLE: algorithm-scratch — trim acknowledged control requests
        let mutable acknowledged = 0
        // DSL-MUTABLE: resource — own the receipt polling timer
        let mutable timer: obj = null
        let openedAt = N.now ()

        let fail reason =
            if failure.IsNone && N.absent completion then
                failure <- Some reason
                N.closeSocket socket
                N.deliver faults reason

        let acceptsTerminal (result: obj) =
            mode = "recover" || result?kind = "Ended"

        let completeStored result =
            let stored =
                N.terminal directory request
                |> Option.defaultWith (fun () -> invalidOp "PTY terminal is not durable")

            N.require (JS.JSON.stringify stored = JS.JSON.stringify result) "PTY terminal acknowledgement mismatch"
            N.clearTimer timer

            completion <- result

            if stored?kind = "NotStarted" then
                failure <- None

            N.closeSocket socket

            if stored?kind = "NotStarted" && mode = "creator" then
                N.deliver unstarted stored?reason
            else
                N.deliver exits result

        let complete result =
            if N.absent completion && (failure.IsNone || result?kind = "NotStarted") then
                completeStored result

        let flushPending () =
            for _, _, frame in pending.ToArray() do
                N.send socket frame fail

        let flush () =
            if attached then
                flushPending ()

        let receive (frame: obj) =
            match unbox<string> frame?kind with
            | "attached" ->
                attached <- true
                flush ()
            | "started" -> started <- true
            | "data" -> N.deliver data frame?data
            | "ack" -> acknowledged <- max acknowledged frame?sequence
            | "terminal" -> complete frame?result
            | "rejected"
            | "fault" -> fail frame?reason
            | _ -> fail "Invalid PTY owner response"

        let trimAcknowledged () =
            while pending.Count > 0
                  && (let number, _, _ = pending.Peek() in number <= acknowledged) do
                pending.Dequeue() |> ignore

        let readTerminal () =
            if N.absent completion then
                N.terminal directory request |> Option.iter complete

        let readNotStarted () =
            if mode = "creator" && N.absent completion then
                N.terminal directory request
                |> Option.filter (fun result -> result?kind = "NotStarted")
                |> Option.iter complete

        let requireTerminal () =
            if N.absent completion then
                fail "PTY owner connection was lost"

        let readDisconnectedTerminal () =
            if mode = "recover" then
                readTerminal ()

        let disconnected _ =
            try
                readDisconnectedTerminal ()
                requireTerminal ()
            with error ->
                fail error.Message

        let checkStartup () =
            if not started && N.now () - openedAt >= 5000. then
                fail "PTY owner startup acknowledgement timed out"

        let checkDeadline requestedAt =
            if N.now () - requestedAt >= 5000. then
                fail "PTY owner control acknowledgement timed out"

        let checkPending () =
            if pending.Count > 0 then
                let _, requestedAt, _ = pending.Peek()
                checkDeadline requestedAt

        let readPolledTerminal () =
            if mode = "recover" || N.absent socket then
                readTerminal ()
            else
                readNotStarted ()

        let rec poll () =
            try
                trimAcknowledged ()
                readPolledTerminal ()
                checkStartup ()
                checkPending ()
            with error ->
                fail error.Message

            if N.absent completion && failure.IsNone then
                timer <- N.timer poll 10

        let awaitEndpoint () =
            task {
                let deadline = N.now () + 5000.

                while not (N.exists directory "available") && not (N.exists directory "terminal") do
                    N.require (N.now () < deadline) "PTY owner endpoint was not confirmed"
                    do! N.delay 10
            }

        let openSocket () =
            match N.terminal directory request with
            | Some result -> complete result
            | None ->
                socket <- N.sockets?createConnection (request?endpoint)
                N.frames socket receive fail

                N.on socket "close" disconnected

                N.on socket "connect" (fun _ ->
                    N.send
                        socket
                        (N.objectOf
                            [ "kind", box "attach"
                              "mode", box mode
                              "key", request?key
                              "incarnation", request?incarnation
                              "secret", request?secret ])
                        fail)

        let connect () =
            task {
                try
                    do! awaitEndpoint ()
                    openSocket ()
                with error ->
                    fail error.Message
            }

        let enqueue kind fields =
            N.require (failure.IsNone && N.absent completion) "PTY owner control is unavailable"
            sequence <- sequence + 1
            let frame = N.objectOf (("kind", box kind) :: ("sequence", box sequence) :: fields)
            pending.Enqueue(sequence, N.now (), frame)

            if attached then
                N.send socket frame fail

            failure |> Option.iter invalidOp

        let signal value =
            readNotStarted ()

            if N.absent completion || completion?kind <> "NotStarted" then
                enqueue "signal" [ "signal", box value ]

        connect () |> ignore
        timer <- N.timer poll 10

        N.objectOf
            [ "onExit",
              box (fun listener ->
                  exits.Add listener

                  if not (N.absent completion) && acceptsTerminal completion then
                      listener completion)
              "onNotStarted",
              box (fun listener ->
                  unstarted.Add listener

                  if mode = "creator" && not (N.absent completion) && completion?kind = "NotStarted" then
                      listener completion?reason)
              "onFault",
              box (fun listener ->
                  faults.Add listener
                  failure |> Option.iter listener)
              "onData", box (fun listener -> data.Add listener)
              "write", box (fun text -> enqueue "write" [ "text", box text ])
              "resize", N.function2 (fun width height -> enqueue "resize" [ "width", box width; "height", box height ])
              "signal", box signal
              "inspect",
              box (fun () ->
                  N.objectOf
                      [ "incarnation", request?incarnation
                        "attached", box attached
                        "started", box started
                        "completion", completion
                        "failure", failure |> Option.map box |> Option.defaultValue null ])
              "dispose",
              box (fun () ->
                  N.clearTimer timer

                  if not (N.absent socket) then
                      socket?destroy () |> ignore) ]

    let private prepare directory request =
        try
            N.writeFile (N.join directory "request") "wx" request
        with error ->
            N.removeEndpoint request
            raise error

    let start (binding: PtyRecoveryBinding) (command: string) (cwd: string) =
        N.require (not (String.IsNullOrWhiteSpace binding.Key)) "PTY recovery key is required"
        N.require (not (String.IsNullOrWhiteSpace binding.IncarnationId)) "PTY recovery incarnation is required"
        (N.files?mkdirSync (binding.Directory, N.objectOf [ "recursive", box true; "mode", box 448 ]): unit)

        let endpointDirectory: string =
            N.files?mkdtempSync (N.join (N.system?tmpdir ()) "wxs-pty-")

        let request =
            N.objectOf
                [ "version", box 1
                  "key", box binding.Key
                  "incarnation", box binding.IncarnationId
                  "secret", box (N.token ())
                  "endpoint", box (N.join endpointDirectory "control")
                  "command", box command
                  "cwd",
                  (if String.IsNullOrEmpty cwd then
                       N.runtime?cwd ()
                   else
                       box cwd)
                  "bunMode", N.originalBunMode N.runtime?env?BUN_BE_BUN ]

        prepare binding.Directory request
        N.launch binding.Directory request
        createClient binding.Directory request "creator"

    let private resultOf (request: obj) (result: obj) =
        let incarnation: string = request?incarnation

        if result?kind = "NotStarted" then
            PtyRecoveryExit.NotStarted(incarnation, result?reason)
        else
            let status: obj = result?exit

            PtyRecoveryExit.Ended(
                incarnation,
                unbox<bool> result?stopped,
                N.optional status?code,
                N.optional status?signal
            )

    let private awaitExit (client: obj) (completion: Task<obj>) graceMs =
        task {
            client?signal "SIGTERM" |> ignore
            let! first = NodeTiming.raceExit (completion :> Task) (max 0 graceMs)

            if not first then
                client?signal "SIGKILL" |> ignore

            let! last = NodeTiming.raceExit (completion :> Task) 5000
            N.require last "PTY recovered scope exit is unknown"
            return! completion
        }

    let private drainClient directory request graceMs =
        task {
            let client = createClient directory request "recover"
            let completion = TaskCompletionSource<obj>()
            client?onExit (fun result -> completion.SetResult result) |> ignore

            client?onFault (fun reason -> completion.SetException(InvalidOperationException reason))
            |> ignore

            try
                let! result = awaitExit client completion.Task graceMs
                return resultOf request result
            finally
                client?dispose () |> ignore
        }

    let drain (directory: string) (key: string) (incarnationId: string option) (graceMs: int) =
        task {
            let request = N.request directory key incarnationId

            match N.terminal directory request with
            | Some result -> return resultOf request result
            | None -> return! drainClient directory request graceMs
        }

    do
        let arguments: string array = N.runtime?argv

        if
            arguments.Length = 4
            && arguments.[1] = N.filePath N.moduleUrl
            && arguments.[2] = "--wanxiangshu-pty-owner"
        then
            PtyRecoveryWorker.run arguments.[3] |> ignore
