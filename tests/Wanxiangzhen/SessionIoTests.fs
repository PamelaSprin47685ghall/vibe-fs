module Wanxiangshu.Tests.Wanxiangzhen.SessionIoTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.Wanxiangzhen.SessionIo
open Wanxiangshu.Tests.Wanxiangzhen.AssertCompat

let entries () : (string * (unit -> unit)) list =
    [ ("getSession with session prop returns Ok",
       fun () ->
           let client = createObj [ "session", box (createObj []) ]

           match getSession client with
           | Ok _ -> checkBare true
           | Error _ -> checkBare false)

      ("getSession with null session returns Error",
       fun () ->
           let client = createObj [ "session", box null ]

           match getSession client with
           | Ok _ -> checkBare false
           | Error msg -> checkBare (msg.Contains "missing"))

      ("getSession without session prop returns Error",
       fun () ->
           let client = createObj []

           match getSession client with
           | Ok _ -> checkBare false
           | Error msg -> checkBare (msg.Contains "missing"))

      ("clientId returns sessionID value",
       fun () ->
           let input = createObj [ "sessionID", box "sid-1" ]
           equal "sid-1" (clientId input))

      ("clientId returns empty for missing sessionID",
       fun () ->
           let input = createObj []
           equal "" (clientId input))

      ("clientId returns empty for empty sessionID",
       fun () ->
           let input = createObj [ "sessionID", box "" ]
           equal "" (clientId input)) ]
