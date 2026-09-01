namespace Wanxiangshu.Execution.Session

open System

type SessionStartedAtProjectionState = private SessionStartedAtProjectionState of DateTimeOffset

[<RequireQualifiedAccess>]
module SessionStartedAtProjection =
    val bind: startedAt: DateTimeOffset -> current: SessionStartedAtProjectionState option -> SessionStartedAtProjectionState
    val startedAt: state: SessionStartedAtProjectionState -> DateTimeOffset
