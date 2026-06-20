module VibeFs.Opencode.WikiTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Wiki
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.WikiRuntime

let private resolveStr (s: string) : JS.Promise<string> = async { return s } |> Async.StartAsPromise

let fetchWikiTool (wikiRuntime: WikiRuntime) (ctx: obj) : obj =
    define fetchWiki
        (box {| id = strReq "Wiki id from the session snapshot" |})
        (fun args context ->
            let sessionID = Dyn.str context "sessionID"
            let directory =
                let current = Dyn.str context "directory"
                if current = "" then Dyn.str ctx "directory" else current
            wikiRuntime.FetchFromSessionSnapshot(sessionID, directory, Dyn.str args "id"))

let submitWikiTool (wikiRuntime: WikiRuntime) : obj =
    define submitWiki
        (box {| entries = wikiDraftEntriesReq "Wiki draft entries" |})
        (fun args context ->
            match parseDraftArray (Dyn.get args "entries") with
            | Error message -> resolveStr message
            | Ok drafts -> wikiRuntime.Submit(Dyn.str context "sessionID", drafts))
