module Wanxiangshu.Kernel.Dedup

open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Kernel.ToolOutputInfoTypes

/// DedupState invariants:
/// - `rawOutputs` is capped at 100 entries to bound memory and linear scan cost.
///   When the cap is reached, the oldest entry (tail) is dropped on insertion.
/// - `fingerprints` is unbounded but each entry is a short hash string.
/// - Both fields grow monotonically until `rawOutputs` hits the cap.
type DedupState =
    { fingerprints: Set<string>
      rawOutputs: string list }

let emptyState : DedupState =
    { fingerprints = Set.empty
      rawOutputs = [] }

/// Max rawOutputs kept per dedup state. Bounds substring scan cost to O(100) per check.
let maxRawOutputs = 100

/// Drop the last element of a list in a single pass: tail-recursive accumulator
/// + one final `List.rev`. Avoids `List.rev |> List.tail |> List.rev` (O(2N)).
let private dropLast (list: 'T list) =
    let rec loop acc l =
        match l with
        | [] -> []
        | [ _ ] -> List.rev acc
        | x :: xs -> loop (x :: acc) xs
    loop [] list

type DedupedOutput =
    { output: string
      state: DedupState }

let isNoChangeOutput (output: string) : bool =
    match tryParse output with
    | Some msg ->
        msg.info
        |> List.exists (function
            | InfoItem.Status s when s = noChangeStatus -> true
            | _ -> false)
    | None -> false

let deduplicate (state: DedupState) (output: string) : DedupedOutput =
    if output.Length = 0 then
        { output = output
          state = state }
    else
        let fpOut = readFingerprint output

        let fpMatch =
            match fpOut with
            | Some fp -> Set.contains fp state.fingerprints
            | None -> false

        let substrMatch =
            let matches (seen: string) =
                match fpOut with
                | Some fp ->
                    fp = seen ||
                    (seen.Length >= output.Length &&
                     (output.Length <= 2048 || seen.Length <= 2048) &&
                     seen.Contains output)
                | None ->
                    seen.Length >= output.Length &&
                    (output.Length <= 2048 || seen.Length <= 2048) &&
                    seen.Contains output
            List.exists matches state.rawOutputs

        let isDuplicate = fpMatch || substrMatch

        if isDuplicate then
            { output = noChangeEnvelope ()
              state = state }
        else
            let nextState =
                match fpOut with
                | Some fp ->
                    let rawOutputs' =
                        if state.rawOutputs.Length >= maxRawOutputs then
                            fp :: dropLast state.rawOutputs
                        else
                            fp :: state.rawOutputs
                    { fingerprints = Set.add fp state.fingerprints
                      rawOutputs = rawOutputs' }
                | None ->
                    let rawOutputs' =
                        if state.rawOutputs.Length >= maxRawOutputs then
                            output :: dropLast state.rawOutputs
                        else
                            output :: state.rawOutputs
                    { fingerprints = state.fingerprints
                      rawOutputs = rawOutputs' }
            { output = output
              state = nextState }
