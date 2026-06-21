module VibeFs.Tests.IntegrationEditPlusSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Opencode.EditPlusState
open VibeFs.Opencode.Plugin
open VibeFs.Opencode.PluginPlus
open VibeFs.Opencode.PluginMimo
open VibeFs.Opencode.PluginMimoPlus
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace

[<Import("createRequire", "node:module")>]
let private createRequire' : string -> (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta : obj = jsNative

let private requireFn : string -> obj = createRequire'(string importMeta?url)
let private fsAsync : obj = requireFn("fs")?promises

let private toolNames (pluginObj: obj) : string[] =
    let tool = get pluginObj "tool"
    unbox<string[]> (Fable.Core.JS.Constructors.Object.keys(tool))

let private hasTool (pluginObj: obj) (name: string) : bool =
    toolNames pluginObj |> Array.contains name

let entryPointShapeSpec () = promise {
    let! ws = mkdtempAsync "editplus-shape-"
    let! p = VibeFs.Opencode.Plugin.plugin (box {| directory = ws |})
    let! pp = VibeFs.Opencode.PluginPlus.plugin (box {| directory = ws |})
    let! pm = VibeFs.Opencode.PluginMimo.plugin (box {| directory = ws |})
    let! pmp = VibeFs.Opencode.PluginMimoPlus.plugin (box {| directory = ws |})
    check "Plugin has no editplus read" (not (hasTool p "read"))
    check "Plugin has no editplus edit" (not (hasTool p "edit"))
    check "PluginPlus has editplus read" (hasTool pp "read")
    check "PluginPlus has editplus edit" (hasTool pp "edit")
    check "PluginMimo has no editplus read" (not (hasTool pm "read"))
    check "PluginMimo has no editplus edit" (not (hasTool pm "edit"))
    check "PluginMimoPlus has editplus read" (hasTool pmp "read")
    check "PluginMimoPlus has editplus edit" (hasTool pmp "edit")
    do! rmAsync ws
}

let tagEncodingSpec () =
    check "numToTag 1 = A" (numToTag 1 = "A")
    check "numToTag 26 = Z" (numToTag 26 = "Z")
    check "numToTag 27 = a" (numToTag 27 = "a")
    check "numToTag 52 = z" (numToTag 52 = "z")
    check "numToTag 53 = AA" (numToTag 53 = "AA")
    check "tagToNum A = 1" (tagToNum "A" = 1)
    check "tagToNum Z = 26" (tagToNum "Z" = 26)
    check "tagToNum a = 27" (tagToNum "a" = 27)
    check "tagToNum z = 52" (tagToNum "z" = 52)
    check "tagToNum AA = 53" (tagToNum "AA" = 53)
    check "tagToNum invalid = -1" (tagToNum "1" = -1)
    check "numToTag roundtrip" (numToTag 100 |> tagToNum = 100)

let splitLinesSpec () =
    let lf = splitLines "a\nb\n"
    check "LF lines count" (lf.Length = 2)
    check "LF keeps newline" (lf.[0] = "a\n")
    let crlf = splitLines "a\r\nb\r\n"
    check "CRLF lines count" (crlf.Length = 2)
    check "CRLF keeps CRLF" (crlf.[0] = "a\r\n")
    let cr = splitLines "a\rb\r"
    check "CR lines count" (cr.Length = 2)
    check "CR keeps CR" (cr.[0] = "a\r")
    let noEnd = splitLines "abc"
    check "no trailing newline" (noEnd.Length = 1 && noEnd.[0] = "abc")

let readToolSpec () = promise {
    let! ws = mkdtempAsync "editplus-read-"
    let filePath = ws + "/test.txt"
    do! writeFileAsync filePath "hello\nworld\n"
    let! p = VibeFs.Opencode.PluginPlus.plugin (box {| directory = ws |})
    let readTool = get (get p "tool") "read"
    let! output = (get readTool "execute") $ (createObj [ "path", box filePath ], createObj [ "directory", box ws; "sessionID", box "s1"; "abort", box null ]) |> unbox<JS.Promise<string>>
    check "read output contains tag A" (output.Contains("A|hello"))
    check "read output contains tag B" (output.Contains("B|world"))
    check "read output has trailing empty tag C" (output.Contains("C|"))
    do! rmAsync ws
}

let readRangeSpec () = promise {
    let! ws = mkdtempAsync "editplus-range-"
    let filePath = ws + "/test.txt"
    do! writeFileAsync filePath "line1\nline2\nline3\nline4\nline5\n"
    let! p = VibeFs.Opencode.PluginPlus.plugin (box {| directory = ws |})
    let readTool = get (get p "tool") "read"
    let! _ = (get readTool "execute") $ (createObj [ "path", box filePath ], createObj [ "directory", box ws; "sessionID", box "s1"; "abort", box null ]) |> unbox<JS.Promise<string>>
    let! ranged = (get readTool "execute") $ (createObj [ "path", box filePath; "begin_", box "B"; "begin", box "B"; "endExclusive", box "D" ], createObj [ "directory", box ws; "sessionID", box "s1"; "abort", box null ]) |> unbox<JS.Promise<string>>
    check "range read contains line2" (ranged.Contains("line2"))
    check "range read contains line3" (ranged.Contains("line3"))
    check "range read excludes line4" (not (ranged.Contains("line4")))
    do! rmAsync ws
}

let editReplaceSpec () = promise {
    let! ws = mkdtempAsync "editplus-replace-"
    let filePath = ws + "/test.txt"
    do! writeFileAsync filePath "alpha\nbeta\ngamma\n"
    let! p = VibeFs.Opencode.PluginPlus.plugin (box {| directory = ws |})
    let readTool = get (get p "tool") "read"
    let editTool = get (get p "tool") "edit"
    let! _ = (get readTool "execute") $ (createObj [ "path", box filePath ], createObj [ "directory", box ws; "sessionID", box "s1"; "abort", box null ]) |> unbox<JS.Promise<string>>
    let! result = (get editTool "execute") $ (createObj [ "begin_", box "B"; "endExclusive", box "C"; "content", box "BETA\n" ], createObj [ "directory", box ws; "sessionID", box "s1"; "abort", box null ]) |> unbox<JS.Promise<string>>
    check "edit replace success" (result.Contains("Edited"))
    let! content = fsAsync?readFile(filePath, "utf-8") |> unbox<JS.Promise<string>>
    check "file has BETA" (content.Contains("BETA"))
    check "file lost beta" (not (content.Contains("beta")))
    check "file keeps gamma" (content.Contains("gamma"))
    do! rmAsync ws
}

let editInsertSpec () = promise {
    let! ws = mkdtempAsync "editplus-insert-"
    let filePath = ws + "/test.txt"
    do! writeFileAsync filePath "first\nsecond\n"
    let! p = VibeFs.Opencode.PluginPlus.plugin (box {| directory = ws |})
    let readTool = get (get p "tool") "read"
    let editTool = get (get p "tool") "edit"
    let! _ = (get readTool "execute") $ (createObj [ "path", box filePath ], createObj [ "directory", box ws; "sessionID", box "s1"; "abort", box null ]) |> unbox<JS.Promise<string>>
    let! result = (get editTool "execute") $ (createObj [ "begin_", box "A"; "endExclusive", box "A"; "content", box "INSERTED\n" ], createObj [ "directory", box ws; "sessionID", box "s1"; "abort", box null ]) |> unbox<JS.Promise<string>>
    check "edit insert success" (result.Contains("Edited"))
    let! content = fsAsync?readFile(filePath, "utf-8") |> unbox<JS.Promise<string>>
    check "file has INSERTED first" (content.StartsWith("INSERTED\n"))
    do! rmAsync ws
}

let editDeleteSpec () = promise {
    let! ws = mkdtempAsync "editplus-delete-"
    let filePath = ws + "/test.txt"
    do! writeFileAsync filePath "keep1\ndelete\nkeep2\n"
    let! p = VibeFs.Opencode.PluginPlus.plugin (box {| directory = ws |})
    let readTool = get (get p "tool") "read"
    let editTool = get (get p "tool") "edit"
    let! _ = (get readTool "execute") $ (createObj [ "path", box filePath ], createObj [ "directory", box ws; "sessionID", box "s1"; "abort", box null ]) |> unbox<JS.Promise<string>>
    let! result = (get editTool "execute") $ (createObj [ "begin_", box "B"; "endExclusive", box "C"; "content", box "" ], createObj [ "directory", box ws; "sessionID", box "s1"; "abort", box null ]) |> unbox<JS.Promise<string>>
    check "edit delete success" (result.Contains("Edited"))
    let! content = fsAsync?readFile(filePath, "utf-8") |> unbox<JS.Promise<string>>
    check "file lost delete line" (not (content.Contains("delete")))
    check "file keeps keep1" (content.Contains("keep1"))
    check "file keeps keep2" (content.Contains("keep2"))
    do! rmAsync ws
}

let replaySpec () = promise {
    let! ws = mkdtempAsync "editplus-replay-"
    let filePath = ws + "/test.txt"
    do! writeFileAsync filePath "alpha\nbeta\ngamma\n"
    let! p = VibeFs.Opencode.PluginPlus.plugin (box {| directory = ws |})
    let readTool = get (get p "tool") "read"
    let editTool = get (get p "tool") "edit"
    let! readOutput = (get readTool "execute") $ (createObj [ "path", box filePath ], createObj [ "directory", box ws; "sessionID", box "s1"; "abort", box null ]) |> unbox<JS.Promise<string>>
    let! editOutput = (get editTool "execute") $ (createObj [ "begin_", box "B"; "endExclusive", box "C"; "content", box "BETA\n" ], createObj [ "directory", box ws; "sessionID", box "s1"; "abort", box null ]) |> unbox<JS.Promise<string>>
    let sessionID = "s1"
    let makeToolMessage msgId toolName input output =
        box (createObj [
            "info", box (createObj [
                "id", box msgId
                "sessionID", box sessionID
                "role", box "toolResult"
                "time", box (createObj [ "created", box 0 ])
                "agent", box "manager"
                "model", box (createObj [ "providerID", box ""; "modelID", box "" ])
            ])
            "parts", box [|
                box (createObj [
                    "type", box "tool"
                    "tool", box toolName
                    "callID", box (msgId + "-call")
                    "state", box (createObj [
                        "status", box "completed"
                        "input", box input
                        "output", box output
                    ])
                ])
            |]
        ])
    let readInput = createObj [ "path", box filePath ]
    let editInput = createObj [ "begin", box "B"; "endExclusive", box "C"; "content", box "BETA\n" ]
    let messages = [|
        makeToolMessage "msg-read" "read" readInput readOutput
        makeToolMessage "msg-edit" "edit" editInput editOutput
    |]
    let! p2 = VibeFs.Opencode.PluginPlus.plugin (box {| directory = ws |})
    let transform = get p2 "experimental.chat.messages.transform"
    let output = createObj [ "messages", box messages ]
    do! transform $ (createObj [ "agent", box "browser"; "sessionID", box sessionID ], output) |> unbox<JS.Promise<unit>>
    let readTool2 = get (get p2 "tool") "read"
    let! reRead = (get readTool2 "execute") $ (createObj [ "path", box filePath ], createObj [ "directory", box ws; "sessionID", box sessionID; "abort", box null ]) |> unbox<JS.Promise<string>>
    check "replay: re-read succeeds after replay" (reRead.Contains("alpha") || reRead.Contains("BETA"))
    do! rmAsync ws
}

let run () : JS.Promise<unit> =
    promise {
        let syncSpecs : (string * (unit -> unit)) list = [
            "tagEncoding", tagEncodingSpec
            "splitLines", splitLinesSpec
        ]
        let asyncSpecs : (string * (unit -> JS.Promise<unit>)) list = [
            "entryPointShape", entryPointShapeSpec
            "readTool", readToolSpec
            "readRange", readRangeSpec
            "editReplace", editReplaceSpec
            "editInsert", editInsertSpec
            "editDelete", editDeleteSpec
            "replay", replaySpec
        ]
        for (label, spec) in syncSpecs do
            let _ = timed label spec in ()
        for (label, spec) in asyncSpecs do
            do! timedAsync label spec
    }
