module Wanxiangshu.Tests.OmpContextTransformTests

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Hosts.Omp.MessageTransform

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Runtime.PromptHeader

[<Import("createRequire", "node:module")>]
let private createRequire': string -> (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta: obj = jsNative

let private requireFn: string -> obj = createRequire' (string importMeta?url)
let private pathModule: obj = requireFn "path"
let private join (a: string) (b: string) = unbox<string> (pathModule?join (a, b))

[<Emit("$0[0].parts[0].text")>]
let private firstEntryTextFromOut (entries: obj array) : string = jsNative

let mutable private originalReadFile: obj = null
let private mockedPaths = System.Collections.Generic.HashSet<string>()

let private installFsMock () =
    if isNull originalReadFile then
        let fsAsync: obj = requireFn "fs"
        let fsPromises = unbox<obj> (fsAsync?promises)
        originalReadFile <- fsPromises?readFile

        let proxy =
            emitJsExpr
                (originalReadFile, fsPromises, mockedPaths)
                "(function(path, opts) {
            var isMocked = false;
            var pathStr = String(path || '');
            mockedPaths.forEach(function(prefix) {
                if (pathStr.indexOf(prefix) >= 0) { isMocked = true; }
            });
            if (isMocked && pathStr.indexOf('polluted-cap') >= 0) {
                return Promise.resolve({});
            }
            return $0.apply($1, arguments);
        })"

        fsPromises?readFile <- box proxy

let capsSynthUserPrepended () =
    promise {
        let! root = mkdtempAsync "omp-caps-synth-"
        let reviewStore = createReviewStore ()

        let entries =
            [| createObj
                   [ "info", box (createObj [ "id", box "user-1"; "role", box "user" ])
                     "parts", box [| createObj [ "type", box "text"; "text", box "hello" ] |] ] |]

        let! out = transformEntriesAsync reviewStore root "sess-1" (box entries)
        check "prepends at least caps synth user" (out.Length >= entries.Length + 1)
        let firstInfo = Dyn.get out.[0] "info"
        check "caps user id prefix" ((Dyn.str firstInfo "id").StartsWith "caps-synth-user-")
        let firstText = firstEntryTextFromOut out
        check "has think prelude" (firstText.Contains "铁律")
        do! rmAsync root
    }

let capsReadToolsInContextTransform () =
    promise {
        let! root = mkdtempAsync "omp-caps-ctx-"
        do! writeFileAsync (join root "ARCH.md") "arch-in-context"
        let reviewStore = createReviewStore ()

        let entries =
            [| createObj
                   [ "info", box (createObj [ "id", box "user-1"; "role", box "user" ])
                     "parts", box [| createObj [ "type", box "text"; "text", box "hello" ] |] ] |]

        let! out = transformEntriesAsync reviewStore root "caps-sess" (box entries)

        try
            check "prepends user and assistant for caps" (out.Length >= entries.Length + 2)
            let mutable foundRead = false

            for entry in out do
                let parts = Dyn.get entry "parts"

                if Dyn.isArray parts then
                    for part in unbox<obj array> parts do
                        if Dyn.str part "type" = "tool" && Dyn.str part "tool" = "read" then
                            let state = Dyn.get part "state"
                            let outText = Dyn.str state "output"

                            if outText.Contains "arch-in-context" then
                                foundRead <- true

            check "caps file surfaced as read tool output" foundRead
        with e ->
            do! rmAsync root
            raise e

        do! rmAsync root
    }

let beforeAgentStartOmitsCapsXml () =
    promise {
        let! root = mkdtempAsync "omp-before-start-"
        do! writeFileAsync (join root "ARCH.md") "should-not-be-in-system"
        let! patch = beforeAgentStart root (box [| "line-one" |])

        try
            let sp = Dyn.get patch "systemPrompt"

            let arr =
                if Dyn.isArray sp then
                    unbox<string array> sp
                else
                    [| string sp |]

            let joined = String.concat "\n" arr
            check "system prompt has user line" (joined.Contains "line-one")
            check "system prompt omits caps-context xml" (not (joined.Contains "<caps-context"))
        with e ->
            do! rmAsync root
            raise e

        do! rmAsync root
    }

let reviewReplayIfStoreEmptyOnTransform () =
    promise {
        let! root = mkdtempAsync "omp-review-replay-"
        let reviewStore = createReviewStore ()
        let sessionId = "omp-review-if-empty"
        reviewStore.applyReviewTaskProjection (sessionId, Some "task A")

        let historyTaskB =
            frontMatterPrompt [ yamlField taskField "task B" ] "With-Review body from history"

        let entries =
            [| createObj
                   [ "info", box (createObj [ "id", box "user-hist"; "role", box "user" ])
                     "parts", box [| createObj [ "type", box "text"; "text", box historyTaskB ] |] ] |]

        let! _ = transformEntriesAsync reviewStore root sessionId (box entries)

        equal
            "review replay IfStoreEmpty: store task unchanged when already active"
            (Some "task A")
            (reviewStore.getReviewTask sessionId)

        do! rmAsync root
    }

let testInspectorCrashWithUndefinedCaps () =
    promise {
        let! root = mkdtempAsync "omp-caps-inject-crash-"
        let badFilePath = join root "polluted-cap.md"
        let goodFilePath = join root "valid-cap.md"
        do! writeFileAsync badFilePath "initial content"
        do! writeFileAsync goodFilePath "valid content"

        installFsMock ()
        mockedPaths.Add(root) |> ignore

        let reviewStore = createReviewStore ()

        let entries =
            [| createObj
                   [ "info", box (createObj [ "id", box "user-1"; "role", box "user"; "sessionID", box "sess-test" ])
                     "parts",
                     box
                         [| createObj
                                [ "type", box "text"
                                  "text", box "---\nobjective: hello objective\n---\nhello text" ] |] ] |]

        Wanxiangshu.Hosts.Omp.ChildSession.markChildSession Wanxiangshu.Hosts.Omp.ExecutorTools.ompScope "sess-test"

        Wanxiangshu.Hosts.Omp.ExecutorTools.ompScope.RegisterTempFiles(
            "sess-test\u0000hello objective",
            [ "valid-cap.md"; "polluted-cap.md" ]
        )

        try
            let! res = transformEntriesAsync reviewStore root "sess-test" entries
            mockedPaths.Remove(root) |> ignore

            let hasCapsUser =
                res
                |> Array.exists (fun entry ->
                    let info = Dyn.get entry "info"
                    let id = Dyn.str info "id"
                    id.StartsWith "caps-synth-user-")

            let hasCapsAssistant =
                res
                |> Array.exists (fun entry ->
                    let info = Dyn.get entry "info"
                    let id = Dyn.str info "id"
                    id.StartsWith "caps-synth-assistant-")

            check "contains caps user" hasCapsUser
            check "contains caps assistant" hasCapsAssistant

            let assistantEntry =
                res
                |> Array.find (fun entry ->
                    let info = Dyn.get entry "info"
                    let id = Dyn.str info "id"
                    id.StartsWith "caps-synth-assistant-")

            let parts = Dyn.get assistantEntry "parts" |> unbox<obj array>

            let hasGood =
                parts
                |> Array.exists (fun p ->
                    let state = Dyn.get p "state"

                    if not (Dyn.isNullish state) then
                        let input = Dyn.get state "input"

                        if not (Dyn.isNullish input) then
                            (Dyn.str input "filePath").Contains "valid-cap.md"
                        else
                            false
                    else
                        false)

            let hasBad =
                parts
                |> Array.exists (fun p ->
                    let state = Dyn.get p "state"

                    if not (Dyn.isNullish state) then
                        let input = Dyn.get state "input"

                        if not (Dyn.isNullish input) then
                            (Dyn.str input "filePath").Contains "polluted-cap.md"
                        else
                            false
                    else
                        false)

            check "has valid-cap tool part" hasGood
            check "does not have polluted-cap tool part" (not hasBad)

            Wanxiangshu.Hosts.Omp.ChildSession.unmarkChildSession
                Wanxiangshu.Hosts.Omp.ExecutorTools.ompScope
                "sess-test"

            do! rmAsync root
        with e ->
            mockedPaths.Remove(root) |> ignore

            Wanxiangshu.Hosts.Omp.ChildSession.unmarkChildSession
                Wanxiangshu.Hosts.Omp.ExecutorTools.ompScope
                "sess-test"

            do! rmAsync root
            raise e
    }
