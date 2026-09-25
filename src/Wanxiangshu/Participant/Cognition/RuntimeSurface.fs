namespace Wanxiangshu.Participant.Cognition

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// JS-native boundary for the cognitive owner's ownership and persistence rules.
///
/// The canvas's home is the physical session and its recovery is a durable read, so
/// both are observable here without a running tool: a test constructs a runtime
/// around a journal-shaped port and observes session isolation, cross-incumbency
/// identity, boot recovery, fail-closed recovery, and the per-owner serial domain.
/// The surface exposes none of the journal's own types: a blob reference crosses as
/// text and a projection as plain JSON, exactly the shapes the JS side supplied.
[<RequireQualifiedAccess>]
module RuntimeSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    [<Emit("$0.then($1)")>]
    let private jsThen (promise: obj) (onFulfilled: Func<obj, unit>) : obj = jsNative

    [<Emit("$0.catch($1)")>]
    let private jsCatch (promise: obj) (onRejected: Func<obj, unit>) : obj = jsNative

    /// A JS promise as a Task. A rejection is carried as a refusal value rather than
    /// an exception, so the port contract stays a Result at every call site and a
    /// broken journal never escapes as a crash.
    let private awaitJs (promise: JS.Promise<obj>) : Task<obj> =
        let completion = TaskCompletionSource<obj>()

        jsThen promise (Func<obj, unit>(fun value -> completion.SetResult value))
        |> ignore

        jsCatch promise (Func<obj, unit>(fun error -> completion.SetResult(box {| ok = false; error = string error |})))
        |> ignore

        completion.Task

    let private ownerOfJs (owner: obj) : CognitiveOwner.T =
        CognitiveOwner.create (SessionId.create (text owner?sessionId)) (text owner?incumbencyId)

    let private todosOfJs (rows: obj) : (string * TodoStatus * TodoPriority) list =
        if isNull rows then
            []
        else
            unbox<obj array> rows
            |> Array.toList
            |> List.map (fun row ->
                (text row?content, WorkspaceSurface.statusOfJs row?status, WorkspaceSurface.priorityOfJs row?priority))

    let private commitToJs (commit: AssumePhaseCommitted) : obj =
        box
            {| ownerKey = commit.OwnerKey
               sessionId = SessionId.value commit.SessionId
               incumbencyId = commit.IncumbencyId
               toolCallId = ToolCallId.value commit.ToolCallId
               ordinal = float commit.Ordinal
               inputDigest = commit.InputDigest
               predecessorOrdinal =
                commit.PredecessorOrdinal
                |> Option.map (fun ordinal -> box (float ordinal))
                |> Option.toObj
               snapshotRef = BlobRef.value commit.SnapshotRef
               snapshotDigest = BlobDigest.value commit.SnapshotDigest
               rendererVersion = commit.RendererVersion |}

    let private decodeProjection (value: obj) : CognitiveProjection =
        { SnapshotRef =
            if isNull value?snapshotRef then
                None
            else
                Some(BlobRef.create (text value?snapshotRef))
          SnapshotDigest =
            if isNull value?snapshotDigest then
                None
            else
                Some(BlobDigest.create (text value?snapshotDigest))
          Todos = []
          Ordinal = int64 (unbox<float> value?ordinal)
          LastToolCallId =
            if isNull value?lastToolCallId then
                None
            else
                Some(ToolCallId.create (text value?lastToolCallId))
          LastInputDigest =
            if isNull value?lastInputDigest then
                None
            else
                Some(text value?lastInputDigest)
          LastSnapshotRef =
            if isNull value?snapshotRef then
                None
            else
                Some(BlobRef.create (text value?snapshotRef))
          LastSnapshotDigest =
            if isNull value?snapshotDigest then
                None
            else
                Some(BlobDigest.create (text value?snapshotDigest)) }

    /// A runtime over a journal-shaped port. The port is four plain JS functions of
    /// the shapes the journal already exposes: write one blob, append one fact, read
    /// one projection, read one blob.
    let CognitiveRuntime_create (port: obj) : obj =
        let writeBlob (content: string) : Task<Result<BlobRef * BlobDigest, string>> =
            task {
                let! result = awaitJs ((unbox<Func<string, JS.Promise<obj>>> port?writeBlob).Invoke content)

                if result?ok then
                    return Ok(BlobRef.create (text result?snapshotRef), BlobDigest.create (text result?snapshotDigest))
                else
                    return Error(text result?error)
            }

        let appendCommit (commit: AssumePhaseCommitted) : Task<Result<unit, string>> =
            task {
                let! result = awaitJs ((unbox<Func<obj, JS.Promise<obj>>> port?appendCommit).Invoke(commitToJs commit))

                if result?ok then
                    return Ok()
                else
                    return Error(text result?error)
            }

        let readProjection (ownerKey: string) : CognitiveProjection option =
            let value = (unbox<Func<string, obj>> port?readProjection).Invoke ownerKey

            if isNull value then None else Some(decodeProjection value)

        let readBlob (blobRef: BlobRef) : Task<Result<string, string>> =
            task {
                let! result =
                    awaitJs (
                        (unbox<Func<string, JS.Promise<obj>>> port?readBlob)
                            .Invoke(BlobRef.value blobRef)
                    )

                if result?ok then
                    return Ok(text result?value)
                else
                    return Error(text result?error)
            }

        CognitiveRuntime(
            { WriteBlob = writeBlob
              AppendCommit = appendCommit
              ReadProjection = readProjection
              ReadBlob = readBlob }
        )

    [<Emit("Object.assign({}, JSON.parse($0), $1)")>]
    let private mergedCanvas (current: string) (patch: obj) : obj = jsNative

    /// Commit one phase for one owner. The call carries either a whole replacement
    /// canvas or a patch merged over the current one — the standing for the
    /// one-output transform the tool injects, so a test observes the serial domain
    /// through it without touching jq.
    let CognitiveRuntime_commit (runtime: obj) (owner: obj) (call: obj) : Task<obj> =
        task {
            let replacement =
                if isNull call?canvasJson then
                    None
                else
                    Some(text call?canvasJson)

            let patch = if isNull call?merge then None else Some call?merge

            let transform (current: string) : Task<Result<string, string>> =
                task {
                    match replacement, patch with
                    | Some canvasJson, _ -> return Ok canvasJson
                    | None, Some patch -> return Ok(CanvasCodec.toJson (mergedCanvas current patch))
                    | None, None -> return Ok current
                }

            let! outcome, committedCanvas =
                (unbox<CognitiveRuntime> runtime).RunPhase
                    (ownerOfJs owner)
                    (ToolCallId.create (text call?toolCallId))
                    (text call?inputDigest)
                    (todosOfJs call?todos)
                    transform

            match outcome with
            | CommitOutcome.Committed ordinal ->
                return
                    box
                        {| ok = true
                           state = "committed"
                           ordinal = float ordinal
                           canvasJson = committedCanvas |}
            | CommitOutcome.Replayed ordinal ->
                return
                    box
                        {| ok = true
                           state = "replayed"
                           ordinal = float ordinal
                           canvasJson = committedCanvas |}
            | CommitOutcome.Rejected reason -> return box {| ok = false; error = reason |}
        }

    /// The owner's current canvas as this process holds it, or the refusal a failed
    /// recovery produces. A refusal names the owner and the reason; it never answers
    /// with an empty canvas.
    let CognitiveRuntime_currentCanvas (runtime: obj) (owner: obj) : Task<obj> =
        task {
            match! (unbox<CognitiveRuntime> runtime).CurrentCanvas(ownerOfJs owner) with
            | Error reason -> return box {| ok = false; error = reason |}
            | Ok snapshot ->
                return
                    box
                        {| ok = true
                           canvasJson = snapshot.CanvasJson
                           todos =
                            snapshot.Todos
                            |> List.map (fun row ->
                                box
                                    {| content = row.Content
                                       status = TodoStatus.wire row.Status
                                       priority = TodoPriority.wire row.Priority |})
                            |> List.toArray |}
        }

    /// The owner key one physical session carries. Same session, same canvas: the
    /// incumbency never enters the key.
    let CognitiveOwner_key (owner: obj) : string = CognitiveOwner.key (ownerOfJs owner)
