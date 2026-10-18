namespace Wanxiangshu.Persistence.EventStore

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Process

[<RequireQualifiedAccess>]
module GitRawSurface =
    let private ending value =
        match value with
        | EndEvidence.Confirmed(ProgramEnd.Exited code) -> createObj [ "kind" ==> "Exited"; "exitCode" ==> code ]
        | EndEvidence.Confirmed(ProgramEnd.Signaled signal) -> createObj [ "kind" ==> "Signaled"; "signal" ==> signal ]
        | EndEvidence.Unconfirmed -> createObj [ "kind" ==> "Unconfirmed" ]

    let private stop value =
        match value with
        | Some StopRequest.CancellationRequested -> createObj [ "kind" ==> "CancellationRequested" ]
        | Some(StopRequest.DeadlineStopRequested duration) ->
            createObj
                [ "kind" ==> "DeadlineStopRequested"
                  "durationMs" ==> duration.TotalMilliseconds ]
        | None -> null

    let private noExecution reason =
        match reason with
        | NoExecution.InvalidRequest text -> createObj [ "kind" ==> "InvalidRequest"; "reason" ==> text ]
        | NoExecution.CancelledBeforeStart -> createObj [ "kind" ==> "CancelledBeforeStart" ]
        | NoExecution.StartRejected text -> createObj [ "kind" ==> "StartRejected"; "reason" ==> text ]

    let private receipt result =
        match result with
        | GitRawResult.NotStarted reason -> createObj [ "kind" ==> "NotStarted"; "reason" ==> noExecution reason ]
        | GitRawResult.Observed observation ->
            createObj
                [ "kind" ==> "Observed"
                  "ending" ==> ending observation.End
                  "stopRequested" ==> stop observation.StopRequested
                  "output"
                  ==> createObj
                          [ "stdout" ==> observation.Output.Stdout
                            "stderr" ==> observation.Output.Stderr
                            "complete" ==> observation.Output.Complete ]
                  "fault" ==> (observation.Fault |> Option.map box |> Option.defaultValue null) ]

    let private readReceipt (raw: obj) =
        let common =
            createObj
                [ "kind" ==> raw?kind
                  "reason" ==> raw?reason
                  "ending" ==> raw?ending
                  "stopRequested" ==> raw?stopRequested
                  "fault" ==> raw?fault
                  "output" ==> null ]

        match ProcessResultWire.read common with
        | ProcessResult.NotStarted reason -> GitRawResult.NotStarted reason
        | ProcessResult.Observed observation ->
            let output = raw?output

            GitRawResult.Observed
                { End = observation.End
                  StopRequested = observation.StopRequested
                  Output =
                    { Stdout = output?stdout
                      Stderr = output?stderr
                      Complete = output?complete }
                  Fault = observation.Fault }

    let private complete (encode: 'value -> obj) (operation: Task<Result<'value, GitStorageFailure>>) =
        task {
            match! operation with
            | Ok value -> return createObj [ "ok" ==> true; "value" ==> encode value ]
            | Error failure ->
                return
                    createObj
                        [ "ok" ==> false
                          "error"
                          ==> createObj
                                  [ "operation" ==> failure.Operation
                                    "reason" ==> failure.Reason
                                    "description" ==> GitStorageFailure.describe failure
                                    "result" ==> receipt failure.Result ] ]
        }

    let private optional encode value =
        value |> Option.map encode |> Option.defaultValue null

    let private tree (entries: TreeEntry list) =
        entries
        |> List.map (fun entry ->
            createObj
                [ "mode" ==> entry.Mode
                  "name" ==> entry.Name
                  "oid" ==> GitObjectId.value entry.Oid ])
        |> List.toArray
        |> box

    let private readEntries (entries: obj array) =
        entries
        |> Array.map (fun entry ->
            { Mode = entry?mode
              Name = entry?name
              Oid = GitObjectId.create entry?oid })
        |> Array.toList

    let private expose (store: IGitRawStore) =
        createObj
            [ "writeBlob"
              ==> (fun bytes -> store.WriteBlob bytes |> complete (GitObjectId.value >> box))
              "writeTree"
              ==> (fun entries -> store.WriteTree(readEntries entries) |> complete (GitObjectId.value >> box))
              "readObject"
              ==> (fun oid -> store.ReadObject(GitObjectId.create oid) |> complete box)
              "readTree"
              ==> (fun oid -> store.ReadTree(GitObjectId.create oid) |> complete tree)
              "readRef"
              ==> (fun name -> store.ReadRef name |> complete (optional (GitObjectId.value >> box))) ]

    let create repoPath =
        ProcessGitRawStore.create repoPath |> expose

    let createWithRunner repoPath (run: obj -> Task<obj>) =
        let execute (request: GitRawRequest) =
            task {
                let input = request.Input |> Option.map box |> Option.defaultValue null

                let! result =
                    run (
                        createObj
                            [ "arguments" ==> List.toArray request.Arguments
                              "input" ==> input
                              "outputLimit" ==> request.OutputLimit ]
                    )

                return readReceipt result
            }

        ProcessGitRawStore.createWithRunner repoPath execute |> expose

    let run repoPath (hardLimitMs: float) (arguments: string[]) (input: byte[] option) (outputLimit: int) =
        task {
            let execute =
                ProcessGitRawStore.createRunner repoPath (TimeSpan.FromMilliseconds hardLimitMs)

            let! result =
                execute
                    { Arguments = Array.toList arguments
                      Input = input
                      OutputLimit = outputLimit }

            return receipt result
        }
