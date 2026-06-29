module Wanxiangshu.Tests.SembleReadInjectionTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.SembleMcp
open Wanxiangshu.Shell.SembleSearch

[<Import("mkdtempSync", "node:fs")>]
let private mkdtempSync (prefix: string) : string = jsNative

[<Import("writeFileSync", "node:fs")>]
let private writeFileSync (path: string) (content: string) : unit = jsNative

[<Import("rmSync", "node:fs")>]
let private rmSync (path: string) (opts: obj) : unit = jsNative

[<Import("join", "node:path")>]
let private pathJoin (a: string) (b: string) : string = jsNative

let private rmDir (path: string) : unit = rmSync path (createObj [ "recursive", box true; "force", box true ])

let private sampleResult : SembleResult =
    { filePath = "src/auth.py"; startLine = 127; endLine = 129
      content = "def save_pretrained(self, path: PathLike):\n    if not os.path.exists(path):\n        os.makedirs(path)"
      score = 0.95 }

let private sampleResult2 : SembleResult =
    { filePath = "src/util.py"; startLine = 10; endLine = 10; content = "let helper = () => ()"; score = 0.88 }

let readLinesForInjectionReadsRealFileWithCorrectTotalLines () =
    let tmpDir = mkdtempSync "/tmp/semble-test-"
    try
        let filePath = pathJoin tmpDir "auth.py"
        let lines = Array.init 150 (fun i -> $"line{i + 1}")
        writeFileSync filePath (String.concat "\n" lines)
        promise {
            let! slice = readLinesForInjection filePath 127
            check "read offset 127" (slice.offset = 127)
            check "read totalLines 150" (slice.totalLines = 150)
            check "read first raw line" (slice.raw.[0] = "line127")
            check "read more false" (not slice.more)
            check "read cut false" (not slice.cut)
            check "footer is end-of-file" (formatReadFooter slice = "(End of file - total 150 lines)")
        }
    finally
        rmDir tmpDir

let readLinesForInjectionShowsMoreWhenFileLongerThanLimit () =
    let tmpDir = mkdtempSync "/tmp/semble-test-"
    try
        let filePath = pathJoin tmpDir "long.py"
        let lines = Array.init 3000 (fun i -> $"line{i + 1}")
        writeFileSync filePath (String.concat "\n" lines)
        promise {
            let! slice = readLinesForInjection filePath 1
            check "long read offset 1" (slice.offset = 1)
            check "long read totalLines 3000" (slice.totalLines = 3000)
            check "long read more true" slice.more
            check "long read cut false" (not slice.cut)
            check "long read raw length 2000" (slice.raw.Length = 2000)
            check "long footer showing" (formatReadFooter slice = "(Showing lines 1-2000 of 3000. Use offset=2001 to continue.)")
        }
    finally
        rmDir tmpDir

let readLinesForInjectionCapsAt50KB () =
    let tmpDir = mkdtempSync "/tmp/semble-test-"
    try
        let filePath = pathJoin tmpDir "big.py"
        let bigLine = String.replicate 100 "x"
        let lines = Array.init 3000 (fun _ -> bigLine)
        writeFileSync filePath (String.concat "\n" lines)
        promise {
            let! slice = readLinesForInjection filePath 1
            check "big read cut true" slice.cut
            check "big read more true" slice.more
            check "big footer capped" ((formatReadFooter slice).StartsWith "(Output capped at 50 KB. Showing lines 1-")
        }
    finally
        rmDir tmpDir

let buildReadToolPartsProducesOnePartPerResult () =
    let tmpDir = mkdtempSync "/tmp/semble-test-"
    try
        let f1 = pathJoin tmpDir "auth.py"
        let f2 = pathJoin tmpDir "util.py"
        writeFileSync f1 (String.concat "\n" (Array.init 200 (fun i -> $"line{i + 1}")))
        writeFileSync f2 (String.concat "\n" (Array.init 50 (fun i -> $"u{i + 1}")))
        let r1 = { sampleResult with filePath = f1 }
        let r2 = { sampleResult2 with filePath = f2 }
        promise {
            let! parts = buildReadToolParts "msg-1" "session-1" [ r1; r2 ]
            equal "part count" 2 parts.Length
        }
    finally
        rmDir tmpDir

let buildReadToolPartsStructureMatchesCaps () =
    let tmpDir = mkdtempSync "/tmp/semble-test-"
    try
        let filePath = pathJoin tmpDir "auth.py"
        writeFileSync filePath (String.concat "\n" (Array.init 200 (fun i -> $"line{i + 1}")))
        let r = { sampleResult with filePath = filePath }
        promise {
            let! parts = buildReadToolParts "msg-1" "session-1" [ r ]
            let p = parts.[0]
            equal "type" "tool" (str p "type")
            equal "tool" "read" (str p "tool")
            check "callID semble prefix" ((str p "callID").StartsWith "semble-call-")
            equal "messageID" "msg-1" (str p "messageID")
            check "id starts with prt_" ((str p "id").StartsWith "prt_")
            let state = get p "state"
            equal "state status" "completed" (str state "status")
            let input = get state "input"
            equal "input filePath" filePath (str input "filePath")
            equal "input offset" 127 (getValue<int> input "offset")
            equal "input limit" 2000 (getValue<int> input "limit")
            check "output has path" ((str state "output").Contains "<path>")
            check "output has 127:" ((str state "output").Contains "127: line127")
            let metadata = get state "metadata"
            check "metadata has preview" (has metadata "preview")
            check "metadata has truncated" (has metadata "truncated")
            check "metadata has loaded" (has metadata "loaded")
            check "metadata has display" (has metadata "display")
            let time = get state "time"
            check "time start > 0" ((getValue<int> time "start") > 0)
            check "time end >= start" ((getValue<int> time "end") >= (getValue<int> time "start"))
            equal "title" ("Read " + filePath) (str state "title")
        }
    finally
        rmDir tmpDir

let buildReadToolPartsCallIDsUnique () =
    let tmpDir = mkdtempSync "/tmp/semble-test-"
    try
        let f1 = pathJoin tmpDir "auth.py"
        let f2 = pathJoin tmpDir "util.py"
        writeFileSync f1 "a\nb\nc"
        writeFileSync f2 "x\ny\nz"
        let r1 = { sampleResult with filePath = f1; startLine = 1 }
        let r2 = { sampleResult2 with filePath = f2; startLine = 1 }
        promise {
            let! parts = buildReadToolParts "msg-1" "session-1" [ r1; r2 ]
            let ids = parts |> Array.map (fun p -> str p "callID") |> Set.ofArray
            equal "unique callIDs" 2 ids.Count
        }
    finally
        rmDir tmpDir

let buildReadToolPartsReadsRealFileWithCorrectTotalLines () =
    let tmpDir = mkdtempSync "/tmp/semble-test-"
    try
        let filePath = pathJoin tmpDir "auth.py"
        writeFileSync filePath (String.concat "\n" (Array.init 150 (fun i -> $"line{i + 1}")))
        let r = { sampleResult with filePath = filePath }
        promise {
            let! parts = buildReadToolParts "msg-1" "session-1" [ r ]
            let p = parts.[0]
            let state = get p "state"
            let output = str state "output"
            check "output reads real line 127" (output.Contains "127: line127")
            check "output footer shows total 150" (output.Contains "(End of file - total 150 lines)")
            check "output not showing chunk 3" (not (output.Contains "(End of file - total 3 lines)"))
        }
    finally
        rmDir tmpDir

let run () =
    promise {
        do! readLinesForInjectionReadsRealFileWithCorrectTotalLines ()
        do! readLinesForInjectionShowsMoreWhenFileLongerThanLimit ()
        do! readLinesForInjectionCapsAt50KB ()
        do! buildReadToolPartsProducesOnePartPerResult ()
        do! buildReadToolPartsStructureMatchesCaps ()
        do! buildReadToolPartsCallIDsUnique ()
        do! buildReadToolPartsReadsRealFileWithCorrectTotalLines ()
    }
