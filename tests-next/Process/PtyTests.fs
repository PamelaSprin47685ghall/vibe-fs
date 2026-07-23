namespace Wanxiangshu.Next.Tests.ProcessTests

open System
open System.Threading
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Process

module PtyTests =

    [<Fact>]
    let Pty_spawn_write_read_kill_cycle () =
        task {
            let sessionId = "pty_test_session_1"
            let cmd: Command =
                { FileName = "true"
                  Arguments = []
                  WorkingDirectory = None
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = Some { Cols = 80; Rows = 24 } }

            let! spawnRes = Pty.pty_spawn sessionId cmd None CancellationToken.None
            match spawnRes with
            | Ok handle ->
                Assert.True(handle.IsPty)
                let! readRes = Pty.pty_read sessionId
                match readRes with
                | Ok (stdoutText, _) -> Assert.NotNull(stdoutText)
                | Error e -> Assert.True(false, sprintf "Read failed: %A" e)

                do! Pty.pty_kill sessionId
                do! Pty.pty_kill sessionId
            | Error err -> Assert.True(false, sprintf "Spawn failed: %A" err)
        }
