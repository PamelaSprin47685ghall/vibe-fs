module Wanxiangshu.Tests.SembleInjectionDedupSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Dedup
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.ReadDedupOpenCode
open Wanxiangshu.Shell.SembleSearchTypes

let private sampleContent =
    "def save_pretrained(self, path: PathLike):\n    if not os.path.exists(path):\n        os.makedirs(path)"

let private sampleStartLine = 127

let private makeRealReadPart (output: string) (path: string) =
    box (
        createObj
            [ "type", box "tool"
              "tool", box "read"
              "callID", box "real-call-1"
              "state",
              box (
                  createObj
                      [ "status", box "completed"
                        "input", box (createObj [ "filePath", box path ])
                        "output", box output ]
              ) ]
    )

let private makeSembleReadPart (output: string) (path: string) =
    box (
        createObj
            [ "type", box "tool"
              "tool", box "read"
              "callID", box "semble-call-abc"
              "state",
              box (
                  createObj
                      [ "status", box "completed"
                        "input", box (createObj [ "filePath", box path ])
                        "output", box output ]
              ) ]
    )

let private assistantWithParts (parts: obj array) =
    box (
        createObj
            [ "info", box (createObj [ "id", box "msg-1"; "role", box "assistant" ])
              "parts", box parts ]
    )

let dedupCollapsesSembleReadAfterRealRead () =
    let sharedOutput =
        formatReadOutput "src/auth.py" sampleContent sampleStartLine None

    let realPart = makeRealReadPart sharedOutput "src/auth.py"
    let semblePart = makeSembleReadPart sharedOutput "src/auth.py"
    let messages = [| assistantWithParts [| realPart; semblePart |] |]
    deduplicateOpencodeReadPartsInPlace messages
    let parts = get messages.[0] "parts" :?> obj array
    let finalState = get parts.[1] "state"
    check "semble read collapsed to no-change envelope" (isNoChangeOutput (string (get finalState "output")))

let dedupCollapsesRealReadAfterSembleRead () =
    let sharedOutput =
        formatReadOutput "src/auth.py" sampleContent sampleStartLine None

    let semblePart = makeSembleReadPart sharedOutput "src/auth.py"
    let realPart = makeRealReadPart sharedOutput "src/auth.py"
    let messages = [| assistantWithParts [| semblePart; realPart |] |]
    deduplicateOpencodeReadPartsInPlace messages
    let parts = get messages.[0] "parts" :?> obj array
    let finalState = get parts.[1] "state"
    check "real read after semble collapsed" (isNoChangeOutput (string (get finalState "output")))

let dedupKeepsSembleReadForDifferentOffset () =
    let offsetOne = formatReadOutput "src/auth.py" sampleContent sampleStartLine None
    let offsetTwo = formatReadOutput "src/auth.py" "totally different content" 250 None
    let p1 = makeSembleReadPart offsetOne "src/auth.py"
    let p2 = makeSembleReadPart offsetTwo "src/auth.py"
    let messages = [| assistantWithParts [| p1; p2 |] |]
    deduplicateOpencodeReadPartsInPlace messages
    let parts = get messages.[0] "parts" :?> obj array
    check "first offset preserved" (not (isNoChangeOutput (string (get (get parts.[0] "state") "output"))))
    check "second offset preserved" (not (isNoChangeOutput (string (get (get parts.[1] "state") "output"))))

let run () =
    dedupCollapsesSembleReadAfterRealRead ()
    dedupCollapsesRealReadAfterSembleRead ()
    dedupKeepsSembleReadForDifferentOffset ()