module Wanxiangshu.Tests.Wanxiangzhen.EventLogParseTests

open Wanxiangshu.Kernel.Wanxiangzhen.EventLogParse
open Wanxiangshu.Tests.Wanxiangzhen.AssertCompat

let entries () : (string * (unit -> unit)) list =
    [ ("Parse.truncate on bad line",
       fun () ->
           let parse s = if s = "ok" then Some 1 else None
           equal [ 1 ] (parseLinesWithTruncate parse [ "ok"; "bad"; "ok" ]))

      ("Parse.skip empty lines",
       fun () ->
           let parse s = Some s
           equal [ "a"; "b" ] (parseLinesWithTruncate parse [ ""; "  "; "a"; "b" ])) ]
