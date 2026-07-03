module Wanxiangshu.Tests.SubagentDispatcherTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostAdapter
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.SubagentDispatcher

let fakeAdapter (response: SubagentResponse) : IHostAdapter =
    { new IHostAdapter with
        member _.WorkspaceRoot = "/tmp/test"
        member _.SessionId = "test-session"
        member _.SpawnSubagent(_) = Promise.lift response }

let sampleCoderArgs =
    box {|
        intents = box [|
            box {| objective = "fix bug"
                   background = "there is a bug"
                   targets = box [| box {| file = "src/Code.fs"; guide = "fix the bug" |} |]
                   do_not_touch = [||] |}
        |]
        tdd = "red" |}

let dispatchReturnsSuccessText () = promise {
    let adapter = fakeAdapter (Success "report")
    let! result = dispatch Opencode adapter "coder" sampleCoderArgs
    equal "success returns text" "report" result
}

let dispatchReturnsFailureMessage () = promise {
    let err = FileSystemFault("test.fs", "ENOENT", "not found")
    let adapter = fakeAdapter (Failure err)
    let! result = dispatch Opencode adapter "coder" sampleCoderArgs
    check "failure contains 'file system fault'" (result.Contains "file system fault")
}

let dispatchReturnsAbortMessage () = promise {
    let adapter = fakeAdapter Aborted
    let! result = dispatch Opencode adapter "coder" sampleCoderArgs
    check "abort contains 'aborted'" (result.Contains "aborted")
}

let run () = promise {
    do! dispatchReturnsSuccessText ()
    do! dispatchReturnsFailureMessage ()
    do! dispatchReturnsAbortMessage ()
}
