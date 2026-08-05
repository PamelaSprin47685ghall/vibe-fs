namespace Wanxiangshu.Domain

open System

/// Closed TDD phase for Coder work: named `coder` tool (required) and Manager
/// `fork` optional `tdd` (prompt requires it when the target role is Coder).
type TddPhase =
    | Red
    | Green

/// Wire codec + child-assignment text for Coder TDD phases.
[<RequireQualifiedAccess>]
module TddPhase =

    let wireName =
        function
        | Red -> "red"
        | Green -> "green"

    /// Fail-closed wire parse. Exact lowercase only; no default to green.
    let parseTddPhase (raw: string) : Result<TddPhase, string> =
        match if isNull raw then "" else raw.Trim() with
        | "red" -> Ok Red
        | "green" -> Ok Green
        | "" -> Error "missing required argument: tdd"
        | other -> Error(sprintf "UnknownTddPhase %s" other)

    /// RED child assignment constraint (injected into the forked Coder prompt).
    let RedAssignment =
        "TDD phase: RED. Add or update a behavior-level regression test that fails for the requested missing behavior. Do not implement the production fix. Do not weaken existing assertions. Only modify fixture/support production code when the test cannot be expressed otherwise, and keep such changes minimal."

    /// GREEN child assignment constraint (injected into the forked Coder prompt).
    let GreenAssignment =
        "TDD phase: GREEN. Implement the smallest production change that makes the previously established failing test pass. Do not delete, skip, loosen, or rewrite the test merely to obtain green. Do not add unrelated behavior."

    let assignmentText =
        function
        | Red -> RedAssignment
        | Green -> GreenAssignment

    /// Child assignment = phase constraint first, then the caller's prompt body.
    let composeAssignment (phase: TddPhase) (prompt: string) : string =
        let body = if isNull prompt then "" else prompt.Trim()
        let phaseText = assignmentText phase

        if body = "" then
            phaseText
        else
            sprintf "%s\n\n%s" phaseText body
