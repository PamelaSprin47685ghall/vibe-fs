namespace Wanxiangshu.Next.Tests

open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Agent

type private Assert = global.Xunit.Assert
type private FactAttribute = global.Xunit.FactAttribute

module ProgramsTests =

    let private dummyFilePort () : FilePort =
        { Read = fun path -> Task.FromResult(Ok(sprintf "content of %s" path))
          Write = fun _ _ -> Task.FromResult(Ok())
          Edit = fun _ _ _ -> Task.FromResult(Ok()) }

    let private dummySearchPort () : SearchPort =
        { Glob = fun pat -> Task.FromResult(Ok [ sprintf "file_%s.txt" pat ])
          Grep = fun pat -> Task.FromResult(Ok [ sprintf "match_%s" pat ]) }

    let private dummyBrowserPort () : BrowserPort =
        { Read = fun url -> Task.FromResult(Ok(sprintf "rendered %s" url))
          NetworkFetch = fun url -> Task.FromResult(Ok(sprintf "response from %s" url)) }

    let private dummyManagerPort () : ManagerPort =
        { Fork = fun id -> Task.FromResult(Ok(sprintf "forked_%s" id))
          Join = fun _ -> Task.FromResult(Ok())
          List = fun () -> Task.FromResult(Ok [ "m1"; "m2" ]) }

    let private dummyRunnerPort (exitCode: int) (stdout: string) (stderr: string) : RunnerPort =
        fun req -> Task.FromResult(Ok(RunnerOutcome.Completed(exitCode, stdout, stderr, false)))

    [<Fact>]
    let ``Coder capability has file operations and one-shot Inspector factory`` () =
        task {
            let filePort = dummyFilePort ()
            let runnerPort = dummyRunnerPort 0 "ok" ""

            match Programs.createCoderCapability Role.Coder filePort with
            | Error err -> failwithf "Expected Ok capability, got %A" err
            | Ok coderCap ->
                let! readRes = coderCap.Read "src/main.fs"
                Assert.Equal(Ok "content of src/main.fs", readRes)

                let! writeRes = coderCap.Write "src/main.fs" "new content"
                Assert.Equal(Ok(), writeRes)

                let! editRes = coderCap.Edit "src/main.fs" "old" "new"
                Assert.Equal(Ok(), editRes)

                match coderCap.CreateInspector runnerPort with
                | Error err -> failwithf "Expected inspector creation success, got %A" err
                | Ok inspector ->
                    let cmd =
                        { FileName = "echo"
                          Arguments = [ "hello" ]
                          WorkingDirectory = None
                          Environment = None
                          Stdin = None
                          Deadline = None
                          PtyOptions = None }

                    let! run1 = inspector.RunCommand cmd

                    match run1 with
                    | Ok(RunnerOutcome.Completed(code, out, _, _)) ->
                        Assert.Equal(0, code)
                        Assert.Equal("ok", out)
                    | _ -> failwithf "Expected completed outcome, got %A" run1

                    let! run2 = inspector.RunCommand cmd

                    match run2 with
                    | Error(RunnerError.ExecutionFailed msg) -> Assert.True(msg.Contains("already used"))
                    | _ -> failwithf "Expected already used error on second call, got %A" run2
        }

    [<Fact>]
    let ``Inspector capability executes command and returns typed result`` () =
        task {
            let runnerPort = dummyRunnerPort 0 "output" "warnings"

            let cmd =
                { FileName = "ls"
                  Arguments = [ "-l" ]
                  WorkingDirectory = None
                  Environment = None
                  Stdin = None
                  Deadline = None
                  PtyOptions = None }

            let req = CommandRequest.ofCommand cmd
            let! directRun = Programs.runInspector req runnerPort

            match directRun with
            | Ok(RunnerOutcome.Completed(code, out, err, _)) ->
                Assert.Equal(0, code)
                Assert.Equal("output", out)
                Assert.Equal("warnings", err)
            | _ -> failwithf "Expected completed outcome, got %A" directRun

            match Programs.createInspector Role.Inspector runnerPort with
            | Error err -> failwithf "Expected inspector capability creation, got %A" err
            | Ok inspector ->
                let! runRes = inspector.Run req

                match runRes with
                | Ok(RunnerOutcome.Completed(code, out, err, _)) ->
                    Assert.Equal(0, code)
                    Assert.Equal("output", out)
                    Assert.Equal("warnings", err)
                | _ -> failwithf "Expected completed outcome, got %A" runRes
        }

    [<Fact>]
    let ``Browser capability exposes read/network only and prevents writes`` () =
        task {
            let browserPort = dummyBrowserPort ()

            match Programs.createBrowserCapability Role.Browser browserPort with
            | Error err -> failwithf "Expected browser capability, got %A" err
            | Ok browserCap ->
                let! readRes = browserCap.Read "https://example.com"
                Assert.Equal(Ok "rendered https://example.com", readRes)

                let! fetchRes = browserCap.NetworkFetch "https://api.example.com/data"
                Assert.Equal(Ok "response from https://api.example.com/data", fetchRes)

            let filePort = dummyFilePort ()

            match Programs.createCoderCapability Role.Browser filePort with
            | Error(ProgramError.PermissionDenied(Role.Browser, perm)) ->
                Assert.True(
                    perm = ToolPermission.Write
                    || perm = ToolPermission.Edit
                    || perm = ToolPermission.Inspector
                )
            | res -> failwithf "Expected permission denied for browser write, got %A" res
        }

    [<Fact>]
    let ``Reviewer capability exposes structured ReviewVerdict DU`` () =
        let filePort = dummyFilePort ()
        let searchPort = dummySearchPort ()

        match Programs.createReviewerCapability Role.Reviewer filePort searchPort with
        | Error err -> failwithf "Expected reviewer capability, got %A" err
        | Ok reviewerCap ->
            let v1 = ReviewVerdict.Approved "LGTM"
            let v2 = ReviewVerdict.Rejected "Security vulnerability"
            let v3 = ReviewVerdict.NeedsRevision [ "Add unit test"; "Fix typos" ]

            Assert.Equal(Ok v1, reviewerCap.SubmitVerdict v1)
            Assert.Equal(Ok v2, reviewerCap.SubmitVerdict v2)
            Assert.Equal(Ok v3, reviewerCap.SubmitVerdict v3)

    [<Fact>]
    let ``Manager role has no file operations or exec capability`` () =
        let filePort = dummyFilePort ()
        let runnerPort = dummyRunnerPort 0 "" ""

        match Programs.createCoderCapability Role.Manager filePort with
        | Error(ProgramError.PermissionDenied(Role.Manager, _)) -> ()
        | res -> failwithf "Expected permission denied when creating Coder for Manager, got %A" res

        match Programs.createInspector Role.Manager runnerPort with
        | Error(ProgramError.PermissionDenied(Role.Manager, ToolPermission.Exec)) -> ()
        | res -> failwithf "Expected permission denied when creating Inspector for Manager, got %A" res

        let managerPort = dummyManagerPort ()

        match Programs.createManagerCapability Role.Manager managerPort with
        | Ok managerCap ->
            task {
                let! forkRes = managerCap.Fork "sub1"
                Assert.Equal(Ok "forked_sub1", forkRes)

                let! listRes = managerCap.List()
                Assert.Equal(Ok [ "m1"; "m2" ], listRes)
            }
            |> ignore
        | Error err -> failwithf "Expected manager capability success, got %A" err
