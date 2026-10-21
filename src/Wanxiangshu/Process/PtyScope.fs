namespace Wanxiangshu.Process

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

module private PtyScopeNative =
    [<ImportAll("node:fs")>]
    let files: obj = jsNative

    [<ImportAll("node:child_process")>]
    let processes: obj = jsNative

    [<ImportAll("node:os")>]
    let system: obj = jsNative

    [<Import("join", "node:path")>]
    let join (directory: string) (name: string) : string = jsNative

    [<Import("isAbsolute", "node:path")>]
    let isAbsolute (path: string) : bool = jsNative

    [<Import("randomUUID", "node:crypto")>]
    let identity () : string = jsNative

    [<Emit("process")>]
    let runtime: obj = jsNative

    [<Emit("import.meta.url")>]
    let moduleUrl: string = jsNative

    [<Import("fileURLToPath", "node:url")>]
    let filePath (_url: string) : string = jsNative

    [<Emit("setTimeout($0, $1)")>]
    let timer (_action: unit -> unit) (_milliseconds: int) : obj = jsNative

    [<Emit("clearTimeout($0)")>]
    let clearTimer (_timer: obj) : unit = jsNative

    [<Emit("$0.on($1, $2)")>]
    let on1 (_source: obj) (_name: string) (_callback: obj -> unit) : unit = jsNative

    [<Emit("$0.once($1, $2)")>]
    let once1 (_source: obj) (_name: string) (_callback: obj -> unit) : unit = jsNative

    [<Emit("""new Promise(resolve => $0.stdin.write($1 + '\n', error => resolve(!error)))""")>]
    let send (_child: obj) (_command: obj) : Task<bool> = jsNative

    [<Emit("$0")>]
    let function2 (_callback: 'a -> 'b -> 'c) : obj = jsNative

    [<Emit("$0 === null || $0 === undefined")>]
    let absent (_value: obj) : bool = jsNative

    [<Emit("$0.exitCode === null && $0.signalCode === null")>]
    let running (_child: obj) : bool = jsNative

    [<Emit("$0 instanceof Error ? $0.message : String($0)")>]
    let message (_error: obj) : string = jsNative

    [<Emit("structuredClone($0)")>]
    let copy (_value: obj) : obj = jsNative

    [<Emit("$0 && (($0.signal === null && Number.isInteger($0.code) && $0.code >= 0) || ($0.code === null && typeof $0.signal === 'string' && $0.signal.startsWith('SIG')))")>]
    let validExit (_value: obj) : bool = jsNative

    [<Emit("({...$0, cwd: $1, env: {...process.env, BUN_BE_BUN: '1'}})")>]
    let workerOptions (_options: obj) (_cwd: string) : obj = jsNative

    [<Emit("Object.hasOwn($0, 'originalBunMode') ? $0.originalBunMode : process.env.BUN_BE_BUN")>]
    let originalBunMode (_options: obj) : obj = jsNative

    [<Emit("""
      (() => {
        const key = Buffer.from('BUN_BE_BUN\0');
        const result = $1 === null || $1 === undefined
          ? $0.unsetenv(key)
          : $0.setenv(key, Buffer.from($1 + '\0'), 1);
        if (result !== 0) throw new Error('PTY target environment restoration failed');
      })()
    """)>]
    let restoreBunMode (_native: obj) (_original: obj) : unit = jsNative

    [<Emit("new Promise(resolve => setTimeout(resolve, $0))")>]
    let delay (_milliseconds: int) : Task<unit> = jsNative

    [<Emit("performance.now()")>]
    let now () : float = jsNative

    [<Emit("setInterval(() => {}, 60000)")>]
    let retainUnknownOwner () : unit = jsNative

    [<Emit("""
      (async () => {
        if (!process.versions.bun || !['darwin', 'linux'].includes(process.platform))
          throw new Error('PTY supervision requires Bun on POSIX');
        const { dlopen, ptr } = await import('bun:ffi');
        const library = dlopen(process.platform === 'darwin' ? '/usr/lib/libSystem.B.dylib' : 'libc.so.6', {
          getsid: { args: ['i32'], returns: 'i32' },
          setpgid: { args: ['i32', 'i32'], returns: 'i32' },
          tcsetpgrp: { args: ['i32', 'i32'], returns: 'i32' },
          ttyname: { args: ['i32'], returns: 'cstring' },
          signal: { args: ['i32', 'ptr'], returns: 'ptr' },
          setenv: { args: ['cstring', 'cstring', 'i32'], returns: 'i32' },
          unsetenv: { args: ['cstring'], returns: 'i32' },
          posix_spawnp: { args: ['ptr', 'cstring', 'ptr', 'ptr', 'ptr', 'ptr'], returns: 'i32' },
          waitpid: { args: ['i32', 'ptr', 'i32'], returns: 'i32' },
          execvp: { args: ['cstring', 'ptr'], returns: 'i32' }
        });
        return { ...library.symbols, ptr };
      })()
    """)>]
    let loadNative () : JS.Promise<obj> = jsNative

    [<Emit("""
      (async () => {
        const { ptr } = await import('bun:ffi');
        const strings = ['sh', $1 ? '-lc' : '-c', $2].map(value => Buffer.from(value + '\0'));
        const args = new BigUint64Array([...strings.map(value => BigInt(ptr(value))), 0n]);
        $0.execvp(strings[0], ptr(args));
        throw new Error('PTY target exec failed');
      })()
    """)>]
    let execute (_native: obj) (_login: bool) (_command: string) : Task<unit> = jsNative

    [<Emit("""
      (() => {
        const strings = [process.execPath, $1, '--wanxiangshu-pty', 'target', $2, '0', 'target'].map(value => Buffer.from(value + '\0'));
        const environment = Object.entries(process.env).map(([key, value]) => Buffer.from(key + '=' + value + '\0'));
        const args = new BigUint64Array([...strings.map(value => BigInt($0.ptr(value))), 0n]);
        const env = new BigUint64Array([...environment.map(value => BigInt($0.ptr(value))), 0n]);
        const pid = new Int32Array(1);
        const error = $0.posix_spawnp($0.ptr(pid), strings[0], null, null, $0.ptr(args), $0.ptr(env));
        if (error !== 0 || pid[0] <= 1) throw new Error('PTY target spawn failed: ' + error);
        return pid[0];
      })()
    """)>]
    let launchTarget (_native: obj) (_module: string) (_directory: string) : int = jsNative

    [<Emit("""
      (() => {
        const status = new Int32Array(1);
        const observed = $0.waitpid($1, $0.ptr(status), 1);
        if (observed === 0) return null;
        if (observed !== $1) throw new Error('PTY target exit observation failed');
        const signal = status[0] & 127;
        if (signal === 0) return { code: (status[0] >> 8) & 255, signal: null };
        const named = Object.entries($2).find(([name, value]) => value === signal);
        if (signal === 127 || !named) throw new Error('PTY target exit status is unknown');
        return { code: null, signal: named[0] };
      })()
    """)>]
    let targetStatus (_native: obj) (_pid: int) (_signals: obj) : obj = jsNative

    [<Emit("import('bun-pty').then(module => module.spawn)")>]
    let loadPty () : JS.Promise<obj> = jsNative

    let objectOf fields = createObj fields

    let require condition reason =
        if not condition then
            invalidOp reason

    let exists directory name : bool = files?existsSync (join directory name)

    let read directory name : obj =
        JS.JSON.parse (files?readFileSync (join directory name, "utf8"))

    let publish directory name value =
        let pending = join directory (name + ".pending")
        (files?writeFileSync (pending, JS.JSON.stringify value, objectOf [ "mode", box 384 ]): unit)
        (files?renameSync (pending, join directory name): unit)

    let receipt directory incarnation name fields =
        publish directory name (objectOf (("incarnation", box incarnation) :: fields))

    let remove directory =
        (files?rmSync (directory, objectOf [ "recursive", box true; "force", box true ]): unit)

    let createDirectory () : string =
        files?mkdtempSync (join (system?tmpdir ()) "wanxiangshu-pty-")

    let waitFor predicate =
        let deadline = now () + 5000.

        let rec wait () =
            task {
                if not (predicate ()) then
                    require (now () < deadline) "PTY physical acknowledgement timed out"
                    do! delay 5
                    return! wait ()
            }

        wait ()

    let signals () : obj = system?constants?signals

    let signalToken signal =
        match signal with
        | "SIGTERM"
        | "SIGKILL"
        | "SIGINT"
        | "SIGHUP"
        | "SIGQUIT"
        | "SIGUSR1"
        | "SIGUSR2" -> signal.Substring 3
        | _ -> invalidArg "signal" "Unsupported PTY signal"

    let ignoreSignals () =
        for name in
            [ "SIGTERM"
              "SIGINT"
              "SIGHUP"
              "SIGQUIT"
              "SIGUSR1"
              "SIGUSR2"
              "SIGTTIN"
              "SIGTTOU" ] do
            on1 runtime name ignore

    let launch role directory group endpoint options : obj =
        processes?spawn (
            runtime?execPath,
            [| filePath moduleUrl
               "--wanxiangshu-pty"
               role
               directory
               string group
               endpoint |],
            options
        )

    [<Emit("""
      (() => {
        if (!Number.isInteger($0) || $0 <= 1 || !Number.isInteger($1) || $1 <= 1 || !$2.trim()) return null;
        const fields = $2.trim().split('\n').map(line => line.trim().split(/\s+/));
        if (fields.some(row => row.length !== 3)) return null;
        const rows = fields.map(([pid, group, state]) => ({ pid: Number(pid), group: Number(group), state }));
        if (rows.some(row => !Number.isInteger(row.pid) || row.pid <= 1 || !Number.isInteger(row.group) || row.group <= 1)) return null;
        if (new Set(rows.map(row => row.pid)).size !== rows.length) return null;
        if (!rows.some(row => row.pid === $0 && row.group === $0 && !row.state.startsWith('Z'))) return null;
        return rows.filter(row => row.pid !== $0 && row.pid !== $1 && !row.state.startsWith('Z'));
      })()
    """)>]
    let decodeMembership (_owner: int) (_sampler: int) (_text: string) : obj array = jsNative

    [<Emit("""
      (() => {
        const sample = $0.spawnSync('/bin/ps', ['-t', $1, '-o', 'pid=,pgid=,stat='], { encoding: 'utf8' });
        if (sample.status !== 0) throw new Error('PTY membership observation failed');
        const rows = $3(process.pid, sample.pid, sample.stdout);
        if (rows === null) return null;
        const sessions = rows.map(row => $2.getsid(row.pid));
        if (sessions.includes(-1)) return null;
        if (sessions.some(session => session !== process.pid)) throw new Error('Foreign session on the PTY');
        return rows;
      })()
    """)>]
    let private observeMembers
        (_processes: obj)
        (_terminal: string)
        (_native: obj)
        (_decode: int -> int -> string -> obj array)
        : obj array =
        jsNative

    let members processes terminal native =
        observeMembers processes terminal native decodeMembership

module private PtyScopeWorker =
    module N = PtyScopeNative

    let private target native directory request =
        task {
            let signals = N.signals ()
            native?signal (signals?SIGTTOU, 1) |> ignore
            native?signal (signals?SIGTTIN, 1) |> ignore
            N.require (native?setpgid (0, 0) = 0) "PTY target group creation failed"
            N.receipt directory request?incarnation "target-ready" [ "pid", N.runtime?pid ]
            do! N.waitFor (fun () -> N.exists directory "execute")
            N.require (native?tcsetpgrp (0, N.runtime?pid) = 0) "PTY target foreground assignment failed"

            for name in
                [ "SIGHUP"
                  "SIGINT"
                  "SIGQUIT"
                  "SIGPIPE"
                  "SIGTERM"
                  "SIGTSTP"
                  "SIGTTIN"
                  "SIGTTOU" ] do
                native?signal (signals?(name), null) |> ignore

            N.restoreBunMode native request?bunMode
            do! N.execute native true request?command
        }

    let private anchorLoop =
        """trap '' TERM INT HUP QUIT USR1 USR2 TSTP TTIN TTOU
while IFS= read -r command
do
  case "$command" in
    release) exit 0 ;;
    TERM|KILL|INT|HUP|QUIT|USR1|USR2) kill -s "$command" 0 ;;
    *) exit 2 ;;
  esac
done
"""

    let private anchor native directory request group endpoint =
        task {
            N.require (native?getsid (0) = request?owner) "PTY anchor session mismatch"
            native?signal ((N.signals ())?SIGTSTP, 1) |> ignore
            let joined: bool = native?setpgid (0, group) = 0
            let kind = if joined then "Ready" else "Unavailable"

            N.receipt
                directory
                request?incarnation
                endpoint
                [ "pid", N.runtime?pid
                  "group", box group
                  "owner", request?owner
                  "anchor", box endpoint
                  "kind", box kind ]

            if joined then
                do! N.execute native false anchorLoop
        }

    let private acceptAnchor incarnation owner (anchors: Dictionary<int, obj>) group endpoint child result =
        if N.absent result || result?kind <> "Ready" || not (N.running child) then
            null
        else
            N.require
                (result?incarnation = incarnation
                 && result?pid = child?pid
                 && result?group = group
                 && result?owner = owner
                 && result?anchor = endpoint)
                "PTY anchor receipt mismatch"

            anchors.[group] <- child
            child

    let private createAnchor directory incarnation owner (anchors: Dictionary<int, obj>) group =
        task {
            let endpoint = "anchor-" + N.identity ()

            let child =
                N.launch
                    "anchor"
                    directory
                    group
                    endpoint
                    (N.objectOf [ "stdio", box [| "pipe"; "ignore"; "inherit" |] ])

            let failed detail =
                N.receipt directory incarnation "fault" [ "message", box detail ]

            N.once1 child "error" (N.message >> failed)
            N.on1 child?stdin "error" ignore
            do! N.waitFor (fun () -> N.exists directory endpoint || not (N.running child))

            let result =
                if N.exists directory endpoint then
                    N.read directory endpoint
                else
                    null

            let accepted = acceptAnchor incarnation owner anchors group endpoint child result

            if N.absent accepted then
                do! N.waitFor (fun () -> not (N.running child))

            return accepted
        }

    let private pin directory incarnation owner (anchors: Dictionary<int, obj>) group =
        match anchors.TryGetValue group with
        | true, child when N.running child -> Task.FromResult child
        | _ -> createAnchor directory incarnation owner anchors group

    let private sendToAnchor child command =
        if N.absent child then
            Task.FromResult false
        else
            let signal: string = command?signal
            N.send child (box (N.signalToken signal))

    let private sendSignal directory incarnation owner anchors (snapshot: obj array) command =
        task {
            // DSL-MUTABLE: algorithm-scratch — collect physical signal acknowledgements
            let mutable delivered = true

            let groups =
                snapshot |> Array.map (fun row -> unbox<int> row?group) |> Array.distinct

            N.require (not (Array.contains owner groups)) "PTY target entered the owner group"

            for group in groups do
                let! child = pin directory incarnation owner anchors group

                let! accepted = sendToAnchor child command

                delivered <- delivered && accepted

            return delivered
        }

    let private release child =
        task {
            if N.running child then
                let! _ = N.send child (box "release")
                return ()
        }

    let private releaseAnchors (anchors: Dictionary<int, obj>) =
        task {
            for child in anchors.Values do
                do! release child

            do! N.waitFor (fun () -> anchors.Values |> Seq.forall (N.running >> not))
        }

    let private owner native directory request =
        task {
            N.ignoreSignals ()
            native?signal ((N.signals ())?SIGTSTP, 1) |> ignore
            let owner: int = N.runtime?pid
            N.require (native?getsid (0) = owner) "PTY owner must be its session leader"
            request?owner <- owner
            N.publish directory "request" request

            let terminal =
                string (native?ttyname (0)) |> fun value -> value.Replace("/dev/", "")

            N.require (terminal <> "" && terminal <> "null") "PTY owner has no controlling terminal"
            let incarnation: string = request?incarnation
            let anchors = Dictionary<int, obj>()
            // DSL-MUTABLE: resource — retain the native target exit status
            let mutable targetExit: obj = null
            // DSL-MUTABLE: algorithm-scratch — number acknowledged signal requests
            let mutable sequence = 0
            // DSL-MUTABLE: cancellation — continue the requested scope termination
            let mutable stopping = false
            // DSL-MUTABLE: algorithm-scratch — identify the signal awaiting acknowledgement
            let mutable lastRequested = 0
            // DSL-MUTABLE: resource — retain that signal's request time
            let mutable requestedAt = 0.
            // DSL-MUTABLE: retirement admission fence — seal the confirmed empty scope
            let mutable ended = false

            let targetPid = N.launchTarget native (N.filePath N.moduleUrl) directory

            let observeTargetExit () =
                let status =
                    if N.absent targetExit then
                        N.targetStatus native targetPid (N.signals ())
                    else
                        null

                if not (N.absent status) then
                    targetExit <- status
                    N.receipt directory incarnation "target-exit" [ "code", status?code; "signal", status?signal ]

            do!
                N.waitFor (fun () ->
                    observeTargetExit ()
                    N.exists directory "target-ready" || not (N.absent targetExit))

            N.require (N.absent targetExit) "PTY target wrapper exited before readiness"
            let ready = N.read directory "target-ready"
            N.require (ready?incarnation = incarnation && ready?pid = targetPid) "PTY target receipt mismatch"
            let! initial = pin directory incarnation owner anchors targetPid
            N.require (not (N.absent initial)) "PTY target group anchor failed"
            N.receipt directory incarnation "started" [ "owner", box owner; "target", box targetPid ]
            N.publish directory "execute" (N.objectOf [])

            let armCommand number =
                if lastRequested <> number then
                    lastRequested <- number
                    requestedAt <- N.now ()

            let nextCommand () =
                let name = "signal-" + string (sequence + 1)

                let command =
                    if N.exists directory name then
                        N.read directory name
                    else
                        null

                let pending = not (N.absent command) && command?sequence > sequence

                if pending then
                    N.require (command?incarnation = incarnation) "PTY signal instance mismatch"
                    armCommand command?sequence
                    N.require (N.now () - requestedAt < 5000.) "PTY signal acknowledgement timed out"
                    command
                else
                    null

            let publishEnded confirmed =
                if confirmed then
                    N.receipt
                        directory
                        incarnation
                        "scope-ended"
                        [ "owner", box owner; "target", box targetPid; "targetExit", targetExit ]

            let finishIfEmpty (snapshot: obj array) =
                task {
                    let anchorPids =
                        anchors.Values
                        |> Seq.filter N.running
                        |> Seq.map (fun value -> unbox<int> value?pid)
                        |> Set.ofSeq

                    let onlyAnchors =
                        snapshot
                        |> Array.forall (fun row -> Set.contains (unbox<int> row?pid) anchorPids)

                    if N.absent targetExit || not onlyAnchors then
                        return false
                    else
                        do! releaseAnchors anchors
                        let final = N.members N.processes terminal native

                        let confirmed = not (N.absent (box final)) && final.Length = 0

                        publishEnded confirmed

                        return confirmed
                }

            let signalIfRequested command snapshot =
                task {
                    let acknowledge delivered =
                        if delivered then
                            sequence <- if N.absent command then sequence else command?sequence
                            N.receipt directory incarnation "signal-forwarded" [ "sequence", box sequence ]

                    let requested =
                        if stopping then
                            N.objectOf [ "signal", box "SIGKILL" ]
                        else
                            command

                    if stopping || not (N.absent command) then
                        stopping <- stopping || requested?signal = "SIGKILL"
                        let! delivered = sendSignal directory incarnation owner anchors snapshot requested

                        acknowledge delivered
                }

            let observeSnapshot command snapshot =
                task {
                    let! finished = finishIfEmpty snapshot

                    if not finished then
                        do! signalIfRequested command snapshot

                    return finished
                }

            let tick () =
                task {
                    N.require (not (N.exists directory "fault")) "PTY physical observation failed"
                    observeTargetExit ()
                    let command = nextCommand ()
                    let observing = stopping || not (N.absent command) || not (N.absent targetExit)

                    let snapshot =
                        if observing then
                            N.members N.processes terminal native
                        else
                            null

                    let! finished =
                        if N.absent (box snapshot) then
                            Task.FromResult false
                        else
                            observeSnapshot command snapshot

                    do! N.delay (if observing then 25 else 10)
                    return finished
                }

            while not ended do
                let! finished = tick ()
                ended <- finished

            N.runtime?exit 0
        }

    let private dispatch role directory group endpoint native request =
        task {
            match role with
            | "owner" -> do! owner native directory request
            | "target" -> do! target native directory request
            | "anchor" -> do! anchor native directory request group endpoint
            | _ -> invalidOp "Invalid PTY worker role"
        }

    let private fault role directory request (error: exn) =
        N.receipt directory request?incarnation "fault" [ "role", box role; "message", box error.Message ]

        if role = "owner" then
            N.retainUnknownOwner ()
        else
            N.runtime?exit 1

    let run role directory group endpoint =
        task {
            let request = N.read directory "request"

            try
                let! native = N.loadNative () |> unbox<Task<obj>>

                do! dispatch role directory group endpoint native request
            with error ->
                fault role directory request error
        }

module internal PtyScope =
    module N = PtyScopeNative

    let membershipRows owner sampler text : obj =
        box (N.decodeMembership owner sampler text)

    let resolveWorkingDirectory (cwd: string) : string =
        let directory =
            if String.IsNullOrEmpty cwd then
                unbox<string> (N.runtime?cwd ())
            else
                cwd

        N.require (N.isAbsolute directory) "PTY cwd must be an absolute path"
        let metadata: obj = N.files?statSync directory
        N.require (unbox<bool> (metadata?isDirectory ())) ("PTY cwd is not a directory: " + directory)
        directory

    let private openScope spawn command options =
        let workingDirectory = resolveWorkingDirectory options?cwd
        let directory = N.createDirectory ()
        let incarnation = N.identity ()
        let mode = N.originalBunMode options
        let originalMode = if N.absent mode then null else mode

        N.publish
            directory
            "request"
            (N.objectOf
                [ "incarnation", box incarnation
                  "command", box command
                  "bunMode", originalMode ])

        let arguments =
            [| N.filePath N.moduleUrl
               "--wanxiangshu-pty"
               "owner"
               directory
               "0"
               "owner" |]

        let term: obj =
            emitJsExpr (spawn, N.runtime?execPath, arguments, N.workerOptions options workingDirectory) "$0($1, $2, $3)"

        let exits = ResizeArray<obj -> unit>()
        let faults = ResizeArray<string -> unit>()
        // DSL-MUTABLE: resource — retain the first physical supervision fault
        let mutable failure: string option = None
        // DSL-MUTABLE: resource — retain the verified scope exit receipt
        let mutable completion: obj = null
        // DSL-MUTABLE: resource — retain the verified startup receipt
        let mutable startup: obj = null
        // DSL-MUTABLE: resource — retain the target's native exit status
        let mutable targetExit: obj = null
        // DSL-MUTABLE: algorithm-scratch — track acknowledged signal sequence
        let mutable forwarded = 0
        // DSL-MUTABLE: retirement admission fence — stop polling after owner exit
        let mutable stopped = false
        // DSL-MUTABLE: algorithm-scratch — number outgoing physical signal requests
        let mutable sequence = 0
        // DSL-MUTABLE: resource — own the physical supervision polling timer
        let mutable timer: obj = null
        let openedAt = N.now ()
        let pendingSignals = Queue<int * float>()
        // DSL-MUTABLE: resource — retain the scope termination observation deadline
        let mutable killRequestedAt: float option = None

        let deliverFault reason listener =
            try
                listener reason
            with _ ->
                ()

        let notifyFault reason =
            for listener in faults do
                deliverFault reason listener

        let fail reason =
            if failure.IsNone then
                failure <- Some reason
                notifyFault reason

        let readReceipt name =
            let result = N.read directory name
            N.require (result?incarnation = incarnation) "PTY receipt instance mismatch"
            result

        let readFault () =
            if N.exists directory "fault" then
                let detail = readReceipt "fault"
                fail ("PTY supervision is unknown: " + string detail?message)

        let readState () =
            if N.exists directory "started" then
                startup <- readReceipt "started"

            if N.exists directory "target-exit" then
                let status = readReceipt "target-exit"
                targetExit <- N.objectOf [ "code", status?code; "signal", status?signal ]

            if N.exists directory "signal-forwarded" then
                forwarded <- (readReceipt "signal-forwarded")?sequence

            while pendingSignals.Count > 0 && fst (pendingSignals.Peek()) <= forwarded do
                pendingSignals.Dequeue() |> ignore

            readFault ()

        let checkAcknowledgement () =
            let now = N.now ()

            if N.absent startup && now - openedAt >= 5000. then
                fail "PTY startup acknowledgement timed out"
            elif pendingSignals.Count > 0 && now - snd (pendingSignals.Peek()) >= 5000. then
                fail "PTY signal acknowledgement timed out"
            elif killRequestedAt |> Option.exists (fun requested -> now - requested >= 5000.) then
                fail "PTY scope termination observation timed out"

        let checkFault () =
            try
                readState ()
                checkAcknowledgement ()
            with error ->
                fail ("PTY supervision is unknown: " + error.Message)

        let rec poll () =
            checkFault ()

            if not stopped then
                timer <- N.timer poll 10

        let deliverExit listener =
            try
                listener (N.copy completion)
            with _ ->
                ()

        let notifyExit () =
            for listener in exits do
                deliverExit listener

        let complete event =
            stopped <- true
            N.clearTimer timer
            checkFault ()

            try
                N.require (failure.IsNone) "PTY supervisor failed before scope completion"
                let started = readReceipt "started"
                let ended = readReceipt "scope-ended"
                N.require (started?owner = term?pid && ended?owner = term?pid) "PTY owner receipt mismatch"
                N.require (started?target = ended?target) "PTY target receipt mismatch"
                N.require (event?exitCode = 0) "PTY owner exited without a successful scope receipt"
                N.require (N.validExit ended?targetExit) "PTY target exit receipt has no native status"
                startup <- started
                completion <- ended?targetExit
                targetExit <- completion

                notifyExit ()
                N.remove directory
            with error ->
                fail ("PTY scope exit is unknown: " + error.Message)

        term?onExit complete |> ignore
        timer <- N.timer poll 0

        let inspect () =
            let phase =
                if failure.IsSome then "Unknown"
                elif not (N.absent completion) then "Closed"
                elif not (N.absent targetExit) then "Draining"
                elif N.absent startup then "Starting"
                else "Running"

            let target = if N.absent startup then null else startup?target

            N.objectOf
                [ "incarnation", box incarnation
                  "phase", box phase
                  "owner", term?pid
                  "target", target
                  "exit", (if N.absent targetExit then null else N.copy targetExit)
                  "forwardedSequence", box forwarded
                  "failure", (failure |> Option.map box |> Option.defaultValue null) ]

        N.objectOf
            [ "pid", term?pid
              "inspect", box inspect
              "onData", term?onData
              "onExit",
              box (fun listener ->
                  exits.Add listener

                  if not (N.absent completion) then
                      deliverExit listener)
              "onFault",
              box (fun listener ->
                  faults.Add listener
                  failure |> Option.iter listener)
              "write", box (fun text -> term?write text)
              "resize", N.function2 (fun width height -> term?resize (width, height))
              "signal",
              box (fun signal ->
                  N.require (not stopped) "PTY owner is no longer reachable"
                  N.signalToken signal |> ignore
                  sequence <- sequence + 1

                  N.receipt
                      directory
                      incarnation
                      ("signal-" + string sequence)
                      [ "sequence", box sequence; "signal", box signal ]

                  let requestedAt = N.now ()
                  pendingSignals.Enqueue(sequence, requestedAt)

                  if signal = "SIGKILL" && killRequestedAt.IsNone then
                      killRequestedAt <- Some requestedAt) ]

    let load () : JS.Promise<obj> =
        task {
            let! _ = N.loadNative () |> unbox<Task<obj>>
            let! spawn = N.loadPty () |> unbox<Task<obj>>
            return N.function2 (fun command options -> openScope spawn command options)
        }
        |> unbox

    do
        let arguments: string array = N.runtime?argv

        if
            arguments.Length = 7
            && arguments.[1] = N.filePath N.moduleUrl
            && arguments.[2] = "--wanxiangshu-pty"
        then
            PtyScopeWorker.run arguments.[3] arguments.[4] (int arguments.[5]) arguments.[6]
            |> ignore
