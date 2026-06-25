module VibeFs.Tests.KnowledgeGraphToolsCodecTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Domain
open VibeFs.Shell.KnowledgeGraphToolsCodec

let decodeFetchEntityMissing () =
    let args = createObj []
    match decodeFetchEntity args with
    | Error (InvalidIntent ("knowledge_graph_fetch", "entity", _)) -> check "fetch entity required" true
    | _ -> check "fetch entity required" false

let decodeFetchEntityOk () =
    let args = createObj [ "entity", box "Fable转译" ]
    match decodeFetchEntity args with
    | Ok e -> check "fetch entity ok" (e = "Fable转译")
    | Error _ -> check "fetch entity ok" false

let decodeDraftEntriesArray () =
    let entry =
        createObj [ "entity", box [| "e1" |]; "fact", box "fact text" ]
    let entries = box [| entry |]
    match decodeDraftEntries entries with
    | Ok drafts ->
        check "entries count" (drafts.Length = 1)
        check "entries fact" (drafts.Head.fact = "fact text")
    | Error _ -> check "entries array" false

let decodeDraftEntriesNotArray () =
    match decodeDraftEntries (box "nope") with
    | Error (InvalidIntent ("return_bookkeeper", "entries", _)) -> check "entries must be array" true
    | _ -> check "entries must be array" false

let decodeReturnBookkeeperArgsOk () =
    let entry =
        createObj [ "entity", box [| "e1" |]; "fact", box "via args" ]
    let args = createObj [ "entries", box [| entry |] ]
    match decodeReturnBookkeeperArgs args with
    | Ok drafts -> check "return bookkeeper args fact" (drafts.Head.fact = "via args")
    | Error _ -> check "return bookkeeper args ok" false

let decodeReturnBookkeeperArgsMissingEntries () =
    let args = createObj []
    match decodeReturnBookkeeperArgs args with
    | Error (InvalidIntent ("return_bookkeeper", "entries", _)) -> check "return bookkeeper entries required" true
    | _ -> check "return bookkeeper entries required" false

let run () =
    decodeFetchEntityMissing ()
    decodeFetchEntityOk ()
    decodeDraftEntriesArray ()
    decodeDraftEntriesNotArray ()
    decodeReturnBookkeeperArgsOk ()
    decodeReturnBookkeeperArgsMissingEntries ()