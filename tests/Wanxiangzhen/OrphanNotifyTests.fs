module Wanxiangshu.Tests.Wanxiangzhen.OrphanNotifyTests

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Runtime.Wanxiangzhen.OrphanNotify
open Wanxiangshu.Tests.Wanxiangzhen.AssertCompat

let entries () : (string * (unit -> unit)) list =
    [ ("idempotencyKey is order-independent and stable",
       fun () ->
           let a = idempotencyKey [ "squad-b"; "squad-a" ]
           let b = idempotencyKey [ "squad-a"; "squad-b"; "squad-a" ]
           equal "wanxiangzhen:orphan_no_pid:squad-a,squad-b" a
           equal a b)

      ("warningText sorts orphan ids",
       fun () ->
           let t = warningText [ "squad-b"; "squad-a" ]
           checkBare (t.Contains "squad-a, squad-b")
           checkBare (t.Contains "Orphan running tasks"))

      ("warningSentEvent carries idempotencyKey and warning",
       fun () ->
           let ev = warningSentEvent "sid" "t0" "key-1" "WARN"
           equal kindWarningSent ev.Kind
           equal "sid" ev.Session
           equal "key-1" (Map.find "idempotencyKey" ev.Payload)
           equal "WARN" (Map.find "warning" ev.Payload))

      ("promptFailedEvent carries idempotencyKey error text",
       fun () ->
           let ev = promptFailedEvent "sid" "t0" "key-1" "WARN" "boom"
           equal kindPromptFailed ev.Kind
           equal "key-1" (Map.find "idempotencyKey" ev.Payload)
           equal "boom" (Map.find "error" ev.Payload)
           equal "WARN" (Map.find "text" ev.Payload))

      ("recoverSentKeys prefers idempotencyKey over legacy warning text",
       fun () ->
           let events =
               [ { V = 1
                   Session = "sid"
                   Kind = kindWarningSent
                   At = "t"
                   Payload = Map [ "idempotencyKey", "k1"; "warning", "old-text" ] }
                 { V = 1
                   Session = "sid"
                   Kind = kindWarningSent
                   At = "t"
                   Payload = Map [ "warning", "legacy-only" ] }
                 { V = 1
                   Session = "other"
                   Kind = kindWarningSent
                   At = "t"
                   Payload = Map [ "idempotencyKey", "other-key" ] }
                 { V = 1
                   Session = "sid"
                   Kind = kindPromptFailed
                   At = "t"
                   Payload = Map [ "idempotencyKey", "failed-key" ] } ]

           let keys = recoverSentKeys "sid" events
           checkBare (keys.Contains "k1")
           checkBare (keys.Contains "legacy-only")
           checkBare (not (keys.Contains "other-key"))
           checkBare (not (keys.Contains "failed-key")) ) ]
