namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Process

/// PTY unified DSL backend for the ToolSurface fork tool.
/// Provides one-shot PTY sessions: fork(agent="pty", prompt="command")
/// creates and runs a shell command in the background.
module PtySurface =

    /// Map PtySignal string from tool args to the internal PtySignal enum.
    let ptySignalOfString (value: string) : PtySignal option =
        match value with
        | "TERM" -> Some PtySignal.Terminate
        | "KILL" -> Some PtySignal.Kill
        | "INT" -> Some PtySignal.Interrupt
        | _ -> None

    /// Run a shell command via Runner.execute and report the result as a string.
    let runCommand (commandText: string) (workspaceDirectory: string option) (ct: CancellationToken) =
        task {
            let cmd: Command =
                { FileName = "sh"
                  Arguments = [ "-lc"; commandText ]
                  WorkingDirectory = workspaceDirectory
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let estimate: ProcessEstimate =
                { EstimatedRuntime = RuntimeSeconds 30.0
                  EstimatedOutput = OutputBytes 200000L
                  EstimatedMemory = EstimatedMemory.Medium }

            let! result =
                Runner.execute
                    cmd
                    estimate
                    { WorkingDirectory = workspaceDirectory
                      DefaultTimeout = None }
                    ct

            match result with
            | Ok(RunnerOutcome.Completed(exitCode, stdout, stderr, _)) ->
                return sprintf "exit %d\nstdout:\n%s\nstderr:\n%s" exitCode stdout stderr
            | Ok(RunnerOutcome.Spooled(exitCode, path, totalBytes, chunkCount, _)) ->
                return sprintf "exit %d\nspool: %s\nbytes: %d\nchunks: %d" exitCode path totalBytes chunkCount
            | Ok(RunnerOutcome.OutputExceeded(bytesWritten, spoolPath)) ->
                return sprintf "output exceeded: %d bytes, spool: %s" bytesWritten (defaultArg spoolPath "none")
            | Error error -> return sprintf "error: %A" error
        }

    let private ptyAgents = Dictionary<string, DateTimeOffset>()

    let private ptyGate = obj ()

    /// Register a new PTY session id with a started-at timestamp.
    let registerPty (id: string) : unit =
        lock ptyGate (fun () -> ptyAgents.[id] <- DateTimeOffset.UtcNow)

    /// Remove a PTY session from the active set.
    let removePty (id: string) : unit =
        lock ptyGate (fun () -> ptyAgents.Remove id |> ignore)

    /// Get a snapshot of all active PTY sessions.
    let activePtys () : (string * DateTimeOffset) list =
        lock ptyGate (fun () -> ptyAgents |> Seq.map (fun kv -> kv.Key, kv.Value) |> Seq.toList)

    /// Create and launch a one-shot PTY session.
    /// The command runs in the background via runCommand; the PTY id
    /// is returned immediately so the model can track it through list().
    /// Waits for the command to complete, then removes the PTY from active set.
    let ptyFork (commandText: string) (workspaceDirectory: string option) : string =
        let id = "pty-" + Guid.NewGuid().ToString("N").Substring(0, 8)
        registerPty id

        let ct = CancellationToken.None
        let work = runCommand commandText workspaceDirectory ct

        let _ =
            task {
                try
                    let! _ = work
                    removePty id
                with _ ->
                    removePty id
            }

        id
