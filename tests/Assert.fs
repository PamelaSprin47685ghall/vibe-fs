/// Shared assertion toolkit for the test suite.  A tiny problem should not pay
/// a framework tax — these mutable counters are the only cross-file state.
module Wanxiangshu.Tests.Assert

open Fable.Core
open Fable.Core.JsInterop

[<Emit("performance.now()")>]
let private now () : float = jsNative

/// Safely extract an error message from a caught exception object that may be
/// undefined/null or lack a `Message` property (JS interop boundary).
let private getErrorMessage (ex: obj) : string =
    if isNull ex then
        ""
    else
        let msg = ex?Message
        if isNull msg then "" else string msg

[<Import("appendFileSync", "node:fs")>]
let private appendFile (path: string) (content: string) (encoding: string) : unit = jsNative

let mutable passed = 0
let mutable failed = 0
let mutable silentEnabled = false
let setSilent (s: bool) : unit = silentEnabled <- s
let private failures = ResizeArray<string>()
let mutable private currentTestLabel = ""
let private timings = ResizeArray<string * float>()
let mutable private verboseEnabled = false
let mutable private verboseLogPath: string option = None
let mutable private verboseChecksLogged = 0

let clearFailuresForRun () : unit =
    passed <- 0
    failed <- 0
    failures.Clear()
    timings.Clear()
    verboseChecksLogged <- 0

/// Enable per-check log append to `path`; pass `None` to disable. Tests entry
/// (`tests/Tests.fs:runAll`) calls this after reading the verbose switch
/// (`--verbose` CLI flag or `VIBE_FS_TEST_VERBOSE=1` env var).
let setVerbose (pathOpt: string option) : unit =
    verboseLogPath <- pathOpt
    verboseEnabled <- Option.isSome pathOpt

let verboseCheckCount () : int = verboseChecksLogged

let private logCheck (kind: string) (label: string) (passedNow: bool) (detail: string option) =
    match verboseLogPath with
    | Some path when verboseEnabled ->
        let status = if passedNow then "OK" else "FAIL"
        let suffix = detail |> Option.map (sprintf " | %s") |> Option.defaultValue ""
        let line = sprintf "[%s] %s > %s: %s%s\n" status currentTestLabel kind label suffix
        appendFile path line "utf8"
        verboseChecksLogged <- verboseChecksLogged + 1
    | _ -> ()

let check (label: string) (condition: bool) : unit =
    logCheck "check" label condition (if condition then None else Some "condition=false")

    if condition then
        passed <- passed + 1
    else
        failed <- failed + 1
        failures.Add(sprintf "%s > %s" currentTestLabel label)

let chk = check

let checkBare (condition: bool) : unit = check "bare" condition

let isSome (o: 'a option) : unit = check "isSome" (Option.isSome o)

let isNone (o: 'a option) : unit = check "isNone" (Option.isNone o)

let recordException (msg: string) : unit =
    failed <- failed + 1
    failures.Add(sprintf "%s > %s" currentTestLabel msg)

let equal (label: string) (expected: 'a) (actual: 'a) : unit =
    let ok = actual = expected

    let detail =
        if ok then
            None
        else
            Some(sprintf "expected %A, got %A" expected actual)

    logCheck "equal" label ok detail

    if ok then
        passed <- passed + 1
    else
        failed <- failed + 1
        failures.Add(sprintf "%s > %s | expected %A, got %A" currentTestLabel label expected actual)

[<Emit("(() => {
    let start = Date.now();
    let err = new Error();
    let stack = err.stack || '';
    let callerInfo = 'unknown';
    let lines = stack.split('\\n');
    for (let i = 1; i < lines.length; i++) {
        if (!lines[i].includes('Assert') && !lines[i].includes('Promise') && !lines[i].includes('jsNative')) {
            let match = lines[i].match(/([^\\/\\(\\)]+\\.(?:js|fs)):(\\d+):(\\d+)/);
            if (match) {
                callerInfo = match[0];
            } else {
                callerInfo = lines[i].trim();
            }
            break;
        }
    }
    return Promise.race([
        $0,
        new Promise((_, reject) => {
            let t = setTimeout(() => {
                let elapsed = Date.now() - start;
                let msg = 'TIMEOUT: Timeout after ' + elapsed + 'ms (' + $1 + 'ms limit) at ' + callerInfo;
                console.error(msg);
                reject(new Error(msg));
            }, $1);
            $0.then(() => clearTimeout(t), () => clearTimeout(t));
        })
    ]).then(res => {
        let elapsed = Date.now() - start;
        if (elapsed > 100) {
            console.log('Resolved in ' + elapsed + 'ms: ' + callerInfo);
        }
        return res;
    });
})()")>]
let private raceWithTimeoutAndInfo (p: JS.Promise<'a>) (ms: int) : JS.Promise<'a> = jsNative

[<Emit("new Promise(resolve => setTimeout(resolve, $0))")>]
let private sleepJs (ms: int) : JS.Promise<unit> = jsNative

let sleep (ms: int) : JS.Promise<unit> = sleepJs ms

let withTimeoutCustom<'T> (ms: int) (p: JS.Promise<'T>) : JS.Promise<'T> = raceWithTimeoutAndInfo p ms

let withTimeoutL<'T> (label: string) (ms: int) (p: JS.Promise<'T>) : JS.Promise<'T> =
    promise {
        try
            return! raceWithTimeoutAndInfo p ms
        with ex ->
            printfn "TIMEOUT EXCEPTION caught at label '%s' (limit %dms): %s" label ms ex.Message
            return raise ex
    }

let withTimeout<'T> (p: JS.Promise<'T>) : JS.Promise<'T> = withTimeoutCustom 1000 p

/// Time a synchronous test body; catches exceptions so one throwing test does not abort the suite.
let timed (label: string) (f: unit -> unit) : unit =
    currentTestLabel <- label
    let start = now ()

    try
        f ()
        timings.Add(label, now () - start)
    with ex ->
        printfn "TEST %s THREW: %A" label ex
        failed <- failed + 1
        failures.Add(sprintf "%s > [THREW]" label)
        timings.Add(label, now () - start)

/// Per-spec async ceiling (integration sub-specs use this inside their own loops).
/// 铁律：单个测试 1s 封顶。挂起测试必须立刻暴露，绝不息。禁止调大。
let asyncSpecTimeoutMs = 15000

/// Suite-level async ceiling: one `*.run` that sequences many sub-specs must not
/// inherit the 1s per-spec budget (e.g. other Integration*.run suites).
/// 铁律：套件同样 1s 封顶。慢测试拆成更小子测试，禁止调大。
let asyncSuiteTimeoutMs = 15000

/// Time an asynchronous test body with a 1s hard timeout; return a unit promise.
/// Promise failure / timeout / throw are all converged into a single failure record.
let timedAsync (label: string) (f: unit -> JS.Promise<'a>) : JS.Promise<unit> =
    currentTestLabel <- label

    promise {
        let start = now ()

        try
            let! _ = raceWithTimeoutAndInfo (f ()) asyncSpecTimeoutMs
            timings.Add(label, now () - start)
        with ex ->
            let msg = getErrorMessage ex

            if msg.Contains "TIMEOUT" then
                failed <- failed + 1
                failures.Add(sprintf "%s > [TIMEOUT>%dms]" label asyncSpecTimeoutMs)
            else
                printfn "TEST ASYNC %s THREW: %A" label ex
                Fable.Core.JS.console.error (ex)
                failed <- failed + 1
                failures.Add(sprintf "%s > [THREW]" label)

            timings.Add(label, now () - start)
    }

let timedAsyncSuite (label: string) (f: unit -> JS.Promise<'a>) : JS.Promise<unit> =
    currentTestLabel <- label

    promise {
        let start = now ()

        try
            let! _ = raceWithTimeoutAndInfo (f ()) asyncSuiteTimeoutMs
            timings.Add(label, now () - start)
        with ex ->
            let msg = getErrorMessage ex

            if msg.Contains "TIMEOUT" then
                failed <- failed + 1
                failures.Add(sprintf "%s > [TIMEOUT>%dms]" label asyncSuiteTimeoutMs)
            else
                printfn "TEST SUITE %s THREW: %A" label ex
                failed <- failed + 1
                failures.Add(sprintf "%s > [THREW]" label)

            timings.Add(label, now () - start)
    }

/// Print the pass/fail summary and the slowest tests, return the failure count.
let summary () : int =
    if not silentEnabled then
        printfn "\n==== %d passed, %d failed ====" passed failed

        if failures.Count > 0 then
            printfn "FAILURES:"
            failures |> Seq.iteri (fun i f -> printfn "  %d. %s" (i + 1) f)

        if timings.Count > 0 then
            let total = timings |> Seq.sumBy snd
            printfn "\nTIMINGS (top 25 of %d, total %.0f ms):" timings.Count total

            timings
            |> Seq.sortByDescending snd
            |> Seq.truncate 25
            |> Seq.iteri (fun i (label, ms) -> printfn "  %2d. %7.1f ms  %s" (i + 1) ms label)

        match verboseLogPath with
        | Some path when verboseEnabled ->
            let footer =
                sprintf "# summary: %d passed, %d failed, %d checks logged\n" passed failed verboseChecksLogged

            appendFile path footer "utf8"
            printfn "\nverbose log: %s" path
        | _ -> ()
    else if failures.Count > 0 then
        printfn "\n==== %d passed, %d failed ====" passed failed
        printfn "FAILURES:"
        failures |> Seq.iteri (fun i f -> printfn "  %d. %s" (i + 1) f)

    failed
