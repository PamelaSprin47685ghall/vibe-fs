module VibeFs.Tests.OmpCapsTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Shell.OmpCaps

[<Import("createRequire", "node:module")>]
let private createRequire' : string -> (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta : obj = jsNative

let private requireFn : string -> obj = createRequire'(string importMeta?url)
let private pathModule : obj = requireFn "path"

let private join (a: string) (b: string) = unbox<string> (pathModule?join(a, b))

let buildCapsFromUppercaseFiles () = promise {
    let! root = mkdtempAsync "omp-caps-"
    do! writeFileAsync (join root "ARCH.md") "arch-content"
    let prd = join root "PRD"
    let fsAsync : obj = requireFn "fs"
    let promises = unbox<obj> (fsAsync?promises)
    do! unbox<JS.Promise<unit>> (promises?mkdir(prd))
    do! writeFileAsync (join prd "01.txt") "prd-content"
    try
        let! context = buildCapsContextAsync root
        check "caps ARCH tag" (context.Contains "<caps-context file=\"ARCH.md\">")
        check "caps arch body" (context.Contains "arch-content")
        check "caps PRD path" (context.Contains "PRD/01.txt")
    with e ->
        do! rmAsync root
        raise e
    do! rmAsync root
}

let stripHostDirContext () =
    let prompt =
        box
            [| "prefix"
               "<dir-context>\nSome directories may have their own rules.\n- AGENTS.md\n</dir-context>\nrest" |]
    let stripped = stripHostAgentsPrompt prompt
    equal "strip keeps prefix" "prefix" stripped.[0]
    equal "strip keeps rest" "rest" stripped.[1]

let appendCapsIdempotent () = promise {
    let! root = mkdtempAsync "omp-caps-idem-"
    do! writeFileAsync (join root "ARCH.md") "arch"
    try
        let! once = appendCapsContext (box [| "initial" |]) root
        let! twice = appendCapsContext (box once) root
        equal "appendCapsContext idempotent" once twice
    with e ->
        do! rmAsync root
        raise e
    do! rmAsync root
}

let capsSkipsExcludedDirs () = promise {
    let! root = mkdtempAsync "omp-caps-excl-"
    let leakDir = join (join root "PRD") "node_modules"
    let fsAsync : obj = requireFn "fs"
    let promises = unbox<obj> (fsAsync?promises)
    do! unbox<JS.Promise<unit>> (promises?mkdir(leakDir, {| recursive = true |}))
    do! writeFileAsync (join leakDir "leak.md") "should-not-appear"
    do! unbox<JS.Promise<unit>> (promises?mkdir(join root "PRD", {| recursive = true |}))
    do! writeFileAsync (join (join root "PRD") "real.md") "real-content"
    try
        let! context = buildCapsContextAsync root
        check "caps includes real" (context.Contains "real-content")
        check "caps excludes node_modules leak" (not (context.Contains "should-not-appear"))
    with e ->
        do! rmAsync root
        raise e
    do! rmAsync root
}

let capsRespectsFileCountBudget () = promise {
    let! root = mkdtempAsync "omp-caps-budget-"
    do! writeFileAsync (join root "ARCH.md") (String.replicate 2000 "a")
    let prd = join root "PRD"
    let fsAsync : obj = requireFn "fs"
    let promises = unbox<obj> (fsAsync?promises)
    do! unbox<JS.Promise<unit>> (promises?mkdir(prd))
    for i in 0 .. 49 do
        let dir = join prd ("dir" + string i)
        do! unbox<JS.Promise<unit>> (promises?mkdir(dir, {| recursive = true |}))
        for j in 0 .. 4 do
            do! writeFileAsync (join dir ("f" + string j + ".md")) (String.replicate 500 "x")
    try
        let! context = buildCapsContextAsync root
        let tagCount = context.Split("<caps-context ").Length - 1
        check ("caps tag count <= 200, got " + string tagCount) (tagCount <= 200)
    with e ->
        do! rmAsync root
        raise e
    do! rmAsync root
}