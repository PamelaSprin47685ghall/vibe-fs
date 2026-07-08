module Wanxiangshu.Tests.ExecutorTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.ExecutorSpawn
open Wanxiangshu.Kernel.Domain

[<Global("process")>]
let private nodeProcess: obj = jsNative

/// Maximum allowed captured output size: 2 MiB.
/// Any process whose stdout+stderr together exceed this must be truncated
/// by the executor; unbounded accumulation is the bug we are probing.
let outputByteLimit = 2 * 1024 * 1024

/// One chunk = 1 MiB of ASCII.  Replicating it 100 times -> 100 MiB of raw
/// output.  The executor should never accumulate more than `outputByteLimit`.
let hugeOutputScript: string =
    "var n=100;var chunk='x'.repeat(1048576);var i=0;
     function write(){
         while(i<n){
             var ok=process.stdout.write(chunk);
             i++;
             if(!ok) return;
         }
         process.exit(0);
     }
     process.stdout.on('drain',write);
     write();"

/// Timeout for the whole spawn-and-capture pipeline: sufficient to process
/// a few megabytes under normal conditions, far below what the unbounded
/// accumulation would reach before OOM on a slow machine.
let timeoutMs = 8000

let cwdVal () : string = unbox (nodeProcess?cwd ())

let mutable runIdCounter = 0

let nextSessionId () =
    runIdCounter <- runIdCounter + 1
    "executor-test-" + string runIdCounter

let isExitedZero (outcome: RunOutcome) : bool =
    match outcome with
    | Exited(0, _, _) -> true
    | _ -> false

let infiniteStdoutBounded () =
    promise {
        // Arrange: spawn a child that dumps ~100 MiB of stdout.
        let sid = nextSessionId ()
        let workDir = cwdVal ()
        let! outcome = spawnAndRun "node" [| "-e"; hugeOutputScript |] workDir (Some timeoutMs) (Some sid) None

        // Act: measure captured stdout / stderr size.
        let capturedOut, capturedErr =
            match outcome with
            | Exited(_, o, e)
            | TimedOut(o, e)
            | Signaled(_, o, e) -> (o, e)
            | SpawnFailed _ -> ("", "")

        let totalBytes =
            (if isNull capturedOut then 0 else capturedOut.Length)
            + (if isNull capturedErr then 0 else capturedErr.Length)

        // Assert 1: captured output must not exceed the byte limit.
        check
            ("infiniteStdoutBounded.totalBytes <= "
             + string outputByteLimit
             + " (got "
             + string totalBytes
             + ")")
            (totalBytes <= outputByteLimit)

        // Assert 2: the process must not return a clean exit.
        // A clean Exited(0, ...) means the child finished under its own
        // control before consumption pressure applied -- the executor had no
        // chance to demonstrate truncation.
        check "infiniteStdoutBounded.outcome is not Exited(0,...)" (not (isExitedZero outcome))
    }

let scriptErr: string =
    "var n=100;var chunk='y'.repeat(1048576);var i=0;
     function write(){
         while(i<n){
             var ok=process.stderr.write(chunk);
             i++;
             if(!ok) return;
         }
         process.exit(0);
     }
     process.stderr.on('drain',write);
     write();"

let infiniteStderrBounded () =
    promise {
        // Arrange: spawn a child that dumps ~100 MiB of stderr.
        // The same accumulator flaw applies symmetrically.
        let sid = nextSessionId ()
        let workDir = cwdVal ()
        let! outcome = spawnAndRun "node" [| "-e"; scriptErr |] workDir (Some timeoutMs) (Some sid) None

        let capturedErr =
            match outcome with
            | Exited(_, _, e)
            | TimedOut(_, e)
            | Signaled(_, _, e) -> if isNull e then "" else e
            | SpawnFailed _ -> ""

        check
            ("infiniteStderrBounded.stderr.Length <= "
             + string outputByteLimit
             + " (got "
             + string capturedErr.Length
             + ")")
            (capturedErr.Length <= outputByteLimit)

        check "infiniteStderrBounded.outcome is not Exited(0,...)" (not (isExitedZero outcome))
    }

let smallOutputUnchanged () =
    promise {
        // Arrange: spawn a child that emits a known, small payload.
        let sid = nextSessionId ()
        let workDir = cwdVal ()
        let payload = "hello-world-payload"
        let scriptOk = "process.stdout.write('" + payload + "'); process.exit(0);"
        let! outcome = spawnAndRun "node" [| "-e"; scriptOk |] workDir (Some timeoutMs) (Some sid) None

        // Assert: Exited(0) and stdout exactly equals payload.
        match outcome with
        | Exited(0, o, _) ->
            let actual = if isNull o then "" else o.Trim()
            equal "smallOutputUnchanged.stdout" payload actual
            check "smallOutputUnchanged.exitZero" true
        | _ -> check "smallOutputUnchanged.expectedExit0" false
    }

let run () =
    promise {
        do! infiniteStdoutBounded ()
        do! infiniteStderrBounded ()
        do! smallOutputUnchanged ()
    }
