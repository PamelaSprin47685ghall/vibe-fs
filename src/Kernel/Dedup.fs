module Wanxiangshu.Kernel.Dedup

open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Kernel.ToolOutputInfoTypes

type DedupedOutput =
    { output: string
      seenOutputs: string list }

let isNoChangeOutput (output: string) : bool =
    match tryParse output with
    | Some msg ->
        msg.info
        |> List.exists (function
            | InfoItem.Status s when s = noChangeStatus -> true
            | _ -> false)
    | None -> false

/// If `output` matches a previously-seen entry (verbatim / substring, or
/// shares the same `readFingerprint`) replace with the no-change envelope;
/// otherwise record it. Fingerprinting lets Semble-injected reads
/// (`CapsFormat.formatReadOutput`) dedup against real `Shell.FileSys.read`
/// outputs even though their wrappers/footers differ.
let deduplicate (seenOutputs: string list) (output: string) : DedupedOutput =
    if output.Length = 0 then
        { output = output
          seenOutputs = seenOutputs }
    else
        let fpOut = readFingerprint output

        let matches (seen: string) =
            match fpOut with
            | Some fp ->
                fp = seen || (seen.Length >= output.Length && (output.Length <= 2048 || seen.Length <= 2048) && seen.Contains output)
            | None ->
                seen.Length >= output.Length && (output.Length <= 2048 || seen.Length <= 2048) && seen.Contains output

        if List.exists matches seenOutputs then
            { output = noChangeEnvelope ()
              seenOutputs = seenOutputs }
        elif fpOut.IsSome then
            { output = output
              seenOutputs = fpOut.Value :: seenOutputs }
        else
            { output = output
              seenOutputs = output :: seenOutputs }
