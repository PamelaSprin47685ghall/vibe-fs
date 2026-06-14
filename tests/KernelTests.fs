module VibeFs.Tests.KernelTests

open VibeFs.Tests.Assert
open VibeFs.Kernel.HeadTail
open VibeFs.Kernel.Dedup
open VibeFs.Kernel.Lru
open VibeFs.Kernel.IpAllowlist
open VibeFs.Kernel.MuxPolicy
open VibeFs.Kernel.HostKernel
open VibeFs.Kernel.ExcludedDirs

let headTail' () =
    let names r = r.stripped |> List.map (fun s -> s.name)
    let r1 = strip "echo a | tail -n 5"
    equal "single tail" "echo a" r1.script
    equal "tail names" [ "tail" ] (names r1)
    equal "head then tail" "cat f" (strip "cat f | head -n 10 | tail -n 3").script
    equal "head then tail names" [ "head"; "tail" ] (names (strip "cat f | head -n 10 | tail -n 3"))
    equal "non-head/tail kept" "cat f | grep -v noise" (strip "cat f | grep -v noise").script
    equal "bare head count" 10 (strip "echo a | head 10").stripped.Head.count
    equal "tail leading dash" "echo a" (strip "echo a | tail -5").script
    equal "semicolon terminator" "echo a; echo b" (strip "echo a | head 2; echo b").script
    equal "ampersand terminator" "cmd &" (strip "cmd | tail 3 &").script
    equal "newline terminator" "cmd\ncmd2" (strip "cmd | head 5\ncmd2").script
    equal "hash terminator" "cmd # keep me" (strip "cmd | head 5 # keep me").script
    let gs = strip "cat f | grep -v noise | sort -r"
    equal "grep/sort unchanged" "cat f | grep -v noise | sort -r" gs.script
    check "grep/sort none stripped" gs.stripped.IsEmpty
    equal "double-quoted ignored" "echo \"a | head 5\"" (strip "echo \"a | head 5\" | tail 2").script
    let nested = strip "cat f | head 10 | tail -5"
    equal "nested script" "cat f" nested.script
    equal "nested counts" [ ("head", 10); ("tail", 5) ] (nested.stripped |> List.map (fun s -> s.name, s.count))

let dedup' () =
    let r1 = deduplicate [] "hello"
    equal "first kept" "hello" r1.output
    let stable = "line of stable content\n" |> List.replicate 8 |> String.concat ""
    let appended = "new content\n" |> List.replicate 8 |> String.concat ""
    equal "long superset marker" dedupMarker (deduplicate [ stable ] (stable + appended)).output
    equal "short increment kept" (stable + "ok") (deduplicate [ stable ] (stable + "ok")).output

let lru' () =
    let mutable s = create 3 : LruStore<int>
    s <- set s "a" 1; s <- set s "b" 2; s <- set s "c" 3; s <- set s "d" 4
    equal "evicted a" None (peek s "a")
    let s2, v = get s "b"
    equal "get b=2" (Some 2) v
    let s3 = set s2 "e" 5
    equal "b promoted" (Some 2) (peek s3 "b")
    equal "c evicted" None (peek s3 "c")
    let s4, consumed = consume s3 "d"
    equal "consume d=4" (Some 4) consumed
    equal "d gone" None (peek s4 "d")

let ipAllowlist' () =
    check "loopback blocked" (match checkIpAllowlist "127.0.0.1" with BlockedIp _ -> true | _ -> false)
    check "10.x blocked" (match checkIpAllowlist "10.0.0.1" with BlockedIp _ -> true | _ -> false)
    check "192.168 blocked" (match checkIpAllowlist "192.168.1.1" with BlockedIp _ -> true | _ -> false)
    check "public allowed" (match checkIpAllowlist "8.8.8.8" with AllowlistedIp _ -> true | _ -> false)
    check "localhost refused" (not (validateHostname "localhost"))
    check "public host allowed" (validateHostname "example.com")

let ipStrict () =
    check "garbage IP blocked" (match checkIpAllowlist "not-an-ip" with BlockedIp _ -> true | _ -> false)
    check "empty IP blocked" (match checkIpAllowlist "" with BlockedIp _ -> true | _ -> false)
    check "valid public allowed" (match checkIpAllowlist "1.1.1.1" with AllowlistedIp _ -> true | _ -> false)

let muxPolicy' () =
    check "orchestrator policy" (getPluginToolPolicy (Some "orchestrator")).IsSome
    check "editor policy" (getPluginToolPolicy (Some "editor")).IsSome
    check "invalid role None" (getPluginToolPolicy (Some "nope")).IsNone

let hostKernel' () =
    let intent = formatEditorIntent "fix bug" [ "a.ts"; "b.ts" ]
    check "intent names files" (intent.Contains "a.ts" && intent.Contains "fix bug")
    let prompt = buildReveriePrompt [ { file = "x.fs"; content = Some "code" } ] "why?"
    check "reverie has question" (prompt.Contains "why?")

let excludedDirs' () =
    check "node_modules excluded" (isExcludedDir "node_modules")
    check ".git excluded" (isExcludedDir ".git")
    check "src not excluded" (not (isExcludedDir "src"))
    check "NODE_MODULES case-insensitive" (isExcludedDir "NODE_MODULES")
