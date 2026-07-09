module Wanxiangshu.Tests.CapsFileCacheTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.CapsFileCache
open Wanxiangshu.Shell.RuntimeScope

let private uid = ref 0

let private uniqueId () =
    uid.Value <- uid.Value + 1
    string uid.Value

let getOrLoadCapsFilesCachesPerSession () =
    promise {
        let scope = create ()
        let sessionID = "caps-file-cache-" + string (uniqueId ())
        let directory = "."
        let! first = getOrLoadCapsFilesForScope scope sessionID directory
        let! second = getOrLoadCapsFilesForScope scope sessionID directory
        check "caps cache second load same list ref" (obj.ReferenceEquals(box first, box second))
        equal "caps cache list length stable" first.Length second.Length
    }

let getOrLoadCapsFilesIsolatesDistinctSessions () =
    promise {
        let scope = create ()
        let ticks = string (uniqueId ())
        let sessionA = "caps-cache-a-" + ticks
        let sessionB = "caps-cache-b-" + ticks
        let directory = "."
        let! listA = getOrLoadCapsFilesForScope scope sessionA directory
        let! listB = getOrLoadCapsFilesForScope scope sessionB directory
        check "caps cache distinct sessions different list ref" (not (obj.ReferenceEquals(box listA, box listB)))
        equal "caps cache distinct sessions same length" listA.Length listB.Length
    }

let getOrLoadCapsFilesIsolatesDistinctDirectories () =
    promise {
        let scope = create ()
        let sessionID = "caps-cache-dir-" + string (uniqueId ())
        let directoryA = "."
        let directoryB = "./caps-cache-miss-dir-" + sessionID
        let! first = getOrLoadCapsFilesForScope scope sessionID directoryA
        let! second = getOrLoadCapsFilesForScope scope sessionID directoryB

        check
            "caps cache same session different directory different list ref"
            (not (obj.ReferenceEquals(box first, box second)))
    }

let getOrLoadCapsFilesReusesAfterDirectoryRoundTrip () =
    promise {
        let scope = create ()
        let sessionID = "caps-cache-roundtrip-" + string (uniqueId ())
        let directoryA = "."
        let directoryB = "./caps-cache-roundtrip-miss-" + sessionID
        let! first = getOrLoadCapsFilesForScope scope sessionID directoryA
        let! _ = getOrLoadCapsFilesForScope scope sessionID directoryB
        let! third = getOrLoadCapsFilesForScope scope sessionID directoryA

        check
            "caps cache third load same list ref as first after dir detour"
            (obj.ReferenceEquals(box first, box third))
    }

let getOrLoadCapsFilesNormalizesDirectoryAlias () =
    promise {
        let scope = create ()
        let sessionID = "caps-cache-alias-" + string (uniqueId ())
        let! dot = getOrLoadCapsFilesForScope scope sessionID "."
        let! dotSlash = getOrLoadCapsFilesForScope scope sessionID "./"
        check "caps cache . and ./ same list ref" (obj.ReferenceEquals(box dot, box dotSlash))
        equal "caps cache alias same length" dot.Length dotSlash.Length
    }

let getOrLoadCapsFilesParallelMissSharesInflight () =
    promise {
        let scope = create ()
        let sessionID = "caps-cache-inflight-" + string (uniqueId ())
        let directory = "."

        let! pair =
            [| getOrLoadCapsFilesForScope scope sessionID directory
               getOrLoadCapsFilesForScope scope sessionID directory |]
            |> Promise.all

        check "caps cache parallel miss same list ref" (obj.ReferenceEquals(box pair.[0], box pair.[1]))
        equal "caps cache parallel miss same length" pair.[0].Length pair.[1].Length
    }

let run () =
    promise {
        do! getOrLoadCapsFilesCachesPerSession ()
        do! getOrLoadCapsFilesIsolatesDistinctSessions ()
        do! getOrLoadCapsFilesIsolatesDistinctDirectories ()
        do! getOrLoadCapsFilesReusesAfterDirectoryRoundTrip ()
        do! getOrLoadCapsFilesNormalizesDirectoryAlias ()
        do! getOrLoadCapsFilesParallelMissSharesInflight ()
    }
