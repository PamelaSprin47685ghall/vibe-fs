module Wanxiangshu.Tests.ExecutorStripTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ExecutorStrip

let noPipe () =
    let r = strip "cat file"
    equal "script unchanged" "cat file" r.script
    check "stripped empty" (List.isEmpty r.stripped)

let stripHeadN () =
    let r = strip "cat file | head -n 5"
    equal "script stripped to cat file" "cat file" r.script
    equal "one stripped pipe" 1 r.stripped.Length
    equal "pipe name head" "head" r.stripped.[0].name
    equal "pipe count 5" 5 r.stripped.[0].count

let stripTailN () =
    let r = strip "cat file | tail -n 10"
    equal "script stripped to cat file" "cat file" r.script
    equal "one stripped pipe" 1 r.stripped.Length
    equal "pipe name tail" "tail" r.stripped.[0].name
    equal "pipe count 10" 10 r.stripped.[0].count

let stripHeadWithoutFlag () =
    let r = strip "cat file | head 5"
    equal "script stripped to cat file" "cat file" r.script
    equal "one stripped pipe" 1 r.stripped.Length
    equal "pipe name head" "head" r.stripped.[0].name
    equal "pipe count 5" 5 r.stripped.[0].count

let unsupportedCommandUnchanged () =
    let r = strip "cat file | grep 5"
    equal "script unchanged for unsupported" "cat file | grep 5" r.script
    check "stripped empty" (List.isEmpty r.stripped)

let missingCountUnchanged () =
    let r = strip "cat file | head -n x"
    equal "script unchanged for missing count" "cat file | head -n x" r.script
    check "stripped empty" (List.isEmpty r.stripped)

let quotedPipePreserved () =
    let r = strip "echo \"|\" | head -n 5"
    equal "script has quoted pipe" "echo \"|\"" r.script
    equal "one stripped pipe" 1 r.stripped.Length
    equal "pipe name head" "head" r.stripped.[0].name
    equal "pipe count 5" 5 r.stripped.[0].count

let commentAfterPipe () =
    let r = strip "cat file | head -n 5 # comment"
    equal "script keeps comment" "cat file # comment" r.script
    equal "one stripped pipe" 1 r.stripped.Length
    equal "pipe name head" "head" r.stripped.[0].name
    equal "pipe count 5" 5 r.stripped.[0].count

let multiplePipesRecursive () =
    let r = strip "cat file | head -n 5 | tail -n 2"
    equal "script stripped to cat file" "cat file" r.script
    equal "two stripped pipes" 2 r.stripped.Length
    equal "first pipe name head" "head" r.stripped.[0].name
    equal "first pipe count 5" 5 r.stripped.[0].count
    equal "second pipe name tail" "tail" r.stripped.[1].name
    equal "second pipe count 2" 2 r.stripped.[1].count

let pipeAtEndNotStripped () =
    let r = strip "cat file |"
    equal "pipe at end kept" "cat file |" r.script
    check "stripped empty" (List.isEmpty r.stripped)

let run () : unit =
    noPipe ()
    stripHeadN ()
    stripTailN ()
    stripHeadWithoutFlag ()
    unsupportedCommandUnchanged ()
    missingCountUnchanged ()
    quotedPipePreserved ()
    commentAfterPipe ()
    multiplePipesRecursive ()
    pipeAtEndNotStripped ()
