module VibeFs.Mux.CapsFileRead

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Shell.CapsShell

/// The current timestamp as milliseconds since the Unix epoch.
/// Keep it as a string so JS callers can inject large millisecond values
/// without losing precision to F#'s 32-bit `int`.
let mutable private timestampSource = fun () -> box (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())

let private timestampMs () : int64 =
    match System.Int64.TryParse(string (timestampSource ())) with
    | true, value -> value
    | false, _ -> 0L

let private nowToken (timestamp: int64) : string = string timestamp
let private isoTime (timestamp: int64) : string =
    System.DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.ToString("O")

/// Replace the timestamp source for deterministic tests.
let setTimestampSource (source: unit -> obj) : unit = timestampSource <- source

/// One synthesised `read` tool-result entry, so the host sees caps files as if
/// the agent had already opened them.
type CapsFileReadEntry =
    { path: string
      callId: string
      input: {| path: string |}
      output: {| success: bool; file_size: int; modifiedTime: string; lines_read: int; content: string |} }

let private withLineNumbers (content: string) : string =
    content.Split('\n') |> Array.mapi (fun i line -> $"{i + 1}\t{line}") |> String.concat "\n"

/// Discover caps files and format each as a prefetched read result.
let buildCapsFileReadData (projectRoot: string) : JS.Promise<CapsFileReadEntry[]> =
    async {
        let! files = findCapsFiles projectRoot |> Async.AwaitPromise
        if List.isEmpty files then return [||]
        else
            let timestamp = timestampMs ()
            let token = nowToken timestamp
            let modified = isoTime timestamp
            return
                files
                |> Array.ofList
                |> Array.mapi (fun index f ->
                    { path = f.label
                      callId = $"caps-fr-{token}-{index}"
                      input = {| path = f.label |}
                      output = {| success = true
                                  file_size = f.content.Length
                                  modifiedTime = modified
                                  lines_read = f.content.Split('\n').Length
                                  content = withLineNumbers f.content |} })
    }
    |> Async.StartAsPromise
