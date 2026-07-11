module Wanxiangshu.Tests.ToolExecutionStatusTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ToolExecutionStatusModule

let roundtripCompleted () =
    let status = fromString "completed"
    equal "Completed enum" ToolExecutionStatus.Completed status
    equal "completed string" "completed" (toString status)

let roundtripError () =
    let status = fromString "ERROR"
    equal "Error enum" ToolExecutionStatus.Error status
    equal "error string" "error" (toString status)

let roundtripUnknown () =
    let status = fromString "custom_status"
    equal "Unknown enum" (ToolExecutionStatus.Unknown "custom_status") status
    equal "custom string" "custom_status" (toString status)

let run () =
    roundtripCompleted ()
    roundtripError ()
    roundtripUnknown ()
