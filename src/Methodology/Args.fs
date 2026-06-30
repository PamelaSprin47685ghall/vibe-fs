module Wanxiangshu.Methodology.Args

open System
open Wanxiangshu.Shell.Dyn

type MethodologyArgs =
    { methodology: string
      intent: string
      background: string
      note: string }

let parse (args: obj) : Result<MethodologyArgs, string> =
    if isNullish args then Error "missing tool arguments"
    else
        let errors = ResizeArray()
        let methodology = if isNullish (get args "methodology") then "" else string (get args "methodology") |> fun s -> s.Trim()
        let intent = if isNullish (get args "intent") then "" else string (get args "intent") |> fun s -> s.Trim()
        let background = if isNullish (get args "background") then "" else string (get args "background") |> fun s -> s.Trim()
        let note = if isNullish (get args "note") then "" else string (get args "note") |> fun s -> s.Trim()
        if methodology = "" then errors.Add("methodology is required")
        if intent = "" then errors.Add("intent is required")
        if background = "" then errors.Add("background is required")
        if note = "" then errors.Add("note is required")
        if errors.Count > 0 then Error(String.concat "; " (errors |> Seq.toList))
        else Ok { methodology = methodology; intent = intent; background = background; note = note }
