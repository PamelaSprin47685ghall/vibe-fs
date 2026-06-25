module VibeFs.Tests.OpencodeAgentConfigCodecTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Shell.Dyn
open VibeFs.Shell.OpencodeAgentConfigCodec

let decodeScalarsEmptyPromptAndMode () =
    let userAgent = createObj []
    let s = decodeUserAgentScalars userAgent
    check "empty prompt" (s.Prompt = "")
    check "empty mode" (s.Mode = "")
    check "permission none when absent" (s.Permission.IsNone)
    check "tools none when absent" (s.Tools.IsNone)
    check "mcps none when absent" (s.Mcps.IsNone)

let decodeScalarsPresentPromptModeAndObjects () =
    let perm = createObj [ "read", box "allow" ]
    let tools = createObj [ "write", box true ]
    let mcps = [| "mcp-a" |] :> obj
    let userAgent =
        createObj [
            "prompt", box "sys"
            "mode", box "primary"
            "permission", perm
            "tools", tools
            "mcps", mcps
        ]
    let s = decodeUserAgentScalars userAgent
    check "present prompt" (s.Prompt = "sys")
    check "present mode" (s.Mode = "primary")
    match s.Permission with
    | Some m -> check "permission map read" (Map.find "read" m = "allow")
    | None -> check "permission some" false
    match s.Tools with
    | Some m -> check "tools map write" (Map.find "write" m = true)
    | None -> check "tools some" false
    match s.Mcps with
    | Some arr -> check "mcps array" (arr.Length = 1 && arr.[0] = "mcp-a")
    | None -> check "mcps some" false

let decodeScalarsNullUserAgentUsesEmptyScalars () =
    let s = decodeUserAgentScalars null
    check "null prompt empty" (s.Prompt = "")
    check "null mode empty" (s.Mode = "")
    check "null permission none" (s.Permission.IsNone)
    check "null tools none" (s.Tools.IsNone)
    check "null mcps none" (s.Mcps.IsNone)

let decodeScalarsUndefinedUserAgentUsesEmptyScalars () =
    let s = decodeUserAgentScalars undefinedValue
    check "undefined prompt empty" (s.Prompt = "")
    check "undefined mode empty" (s.Mode = "")
    check "undefined permission none" (s.Permission.IsNone)
    check "undefined tools none" (s.Tools.IsNone)
    check "undefined mcps none" (s.Mcps.IsNone)

let run () =
    decodeScalarsEmptyPromptAndMode ()
    decodeScalarsPresentPromptModeAndObjects ()
    decodeScalarsNullUserAgentUsesEmptyScalars ()
    decodeScalarsUndefinedUserAgentUsesEmptyScalars ()