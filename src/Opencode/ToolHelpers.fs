module VibeFs.Opencode.ToolHelpers

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Domain

/// Format a DomainError into a human-readable tool output string.
let formatDomainError (context: string) (error: DomainError) : string =
    match error with
    | UpstreamRefused reason -> $"{context} failed: {reason}"
    | UpstreamTimeout seconds -> $"{context} timed out after {seconds}s"
    | UnknownJsError message -> $"{context} failed: {message}"
    | SystemPanic message -> $"{context} failed: {message}"
    | MessageAborted -> $"{context} aborted"
    | SessionBusy -> $"{context} blocked: session busy"
    | TaskWaitBackgrounded -> $"{context} moved to background"
    | ExecutorExecutableMissing executable -> $"{context} failed: {executable} not found"
    | ParseError(location, detail) -> $"{context} failed: parse error in {location}: {detail}"
    | ToolNotPermitted(agent, tool) -> $"{context} failed: {tool} not permitted for {agent}"
    | InvalidIntent(tool, field, detail) -> $"{context} failed: invalid {field} for {tool}: {detail}"

/// Helpers for reading optional fields off host objects.
let optStr (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some(string v)
