module Wanxiangshu.Tests.Wanxiangzhen.SquadTaskTransitionTests

open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTaskTransition
open Wanxiangshu.Tests.Wanxiangzhen.AssertCompat

let entries () : (string * (unit -> unit)) list =
    [ ("Transition.Strict rejects illegal",
       fun () ->
           let t = create "t1" "t" "d" [] "now"

           match applyStatusOption Strict t Merged "later" with
           | None -> checkBare true
           | Some _ -> checkBare false)

      ("Transition.Strict allows legal",
       fun () ->
           let t = create "t1" "t" "d" [] "now"

           match applyStatusOption Strict t Running "later" with
           | Some t2 ->
               equal Running t2.Status
               equal "later" t2.UpdatedAt
           | None -> checkBare false)

      ("Transition.ReplayFact bypasses canTransition",
       fun () ->
           let t =
               { (create "t1" "t" "d" [] "now") with
                   Status = Pending }

           let t2 = applyStatus ReplayFact t Merged "later"
           equal Merged t2.Status)

      ("Transition.applyStatusOption ReplayFact always Some",
       fun () ->
           let t =
               { (create "t1" "t" "d" [] "now") with
                   Status = Merged }

           match applyStatusOption ReplayFact t Running "later" with
           | Some t2 -> equal Running t2.Status
           | None -> checkBare false) ]
