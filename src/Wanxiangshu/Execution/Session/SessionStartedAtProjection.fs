namespace Wanxiangshu.Execution.Session

open System

type SessionStartedAtProjectionState = private SessionStartedAtProjectionState of DateTimeOffset

[<RequireQualifiedAccess>]
module SessionStartedAtProjection =

    let bind startedAt current =
        match current with
        | Some existing -> existing
        | None -> SessionStartedAtProjectionState startedAt

    let startedAt (SessionStartedAtProjectionState value) = value
