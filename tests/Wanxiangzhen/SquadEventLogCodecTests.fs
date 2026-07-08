module Wanxiangshu.Tests.Wanxiangzhen.SquadEventLogCodecTests

open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Shell.Wanxiangzhen.SquadEventWanCodec
open Wanxiangshu.Shell.EventLogCodec
open Wanxiangshu.Tests.Wanxiangzhen.AssertCompat

let entries () : (string * (unit -> unit)) list = [
    ("SquadEventLog.TasksCreated round-trip", fun () ->
        let tasks = [ ("a1", "t", "d", [ "x" ]) ]
        let we = squadEventToWanEvent "2025-01-01T00:00:00Z" (TasksCreated ("s1", tasks))
        match trySquadEventFromWanEvent we with
        | Some (TasksCreated (sid, decoded)) ->
            equal "s1" sid
            equal 1 decoded.Length
            let (tid, _, _, deps) = decoded.[0]
            equal "a1" tid
            equal [ "x" ] deps
        | _ -> check "" false)

    ("SquadEventLog.squad_created round-trip", fun () ->
        let we = squadEventToWanEvent "t" (SquadCreated ("s1", "req"))
        match trySquadEventFromWanEvent we with
        | Some (SquadCreated (sid, req)) ->
            equal "s1" sid
            equal "req" req
        | _ -> check "" false)
]