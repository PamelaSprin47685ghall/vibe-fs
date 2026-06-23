module VibeFs.Mux.KnowledgeGraphRuntimeIO

open Fable.Core
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraphRuntimeState
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.KnowledgeGraphPortLock
open VibeFs.Shell.Dyn

let buildEntries (root: string) (drafts: KnowledgeGraphDraft list) : JS.Promise<Result<KnowledgeGraphEntry list, string>> =
    promise {
        let! files = readKnowledgeGraphFiles root
        let projection = projectLatestWins files
        let normalizedDrafts = normalizeDraftIds projection drafts
        let random = System.Random()
        let allocate = allocateRandomHexId (fun () -> random.Next(0, 65536))
        return applyDrafts allocate projection normalizedDrafts
    }

let extractTexts (item: obj) : string list =
    if Dyn.typeIs item "string" then [ string item ]
    else
        let texts = ResizeArray<string>()
        let content = Dyn.str item "content"
        if content <> "" then texts.Add(content)
        let text = Dyn.str item "text"
        if text <> "" then texts.Add(text)
        let parts = Dyn.get item "parts"
        if not (Dyn.isNullish parts) && Dyn.isArray parts then
            for p in (parts :?> obj array) do
                let partText = Dyn.str p "text"
                if partText <> "" then texts.Add(partText)
        List.ofSeq texts

let tryResolveJobContext (getChatHistory: (string -> JS.Promise<obj array>) option) (sessionID: string)
                        : JS.Promise<KnowledgeGraphJobContext option> =
    promise {
        if System.String.IsNullOrWhiteSpace sessionID then return None
        else
            match getChatHistory with
            | None -> return None
            | Some getHistory ->
                try
                    let! history = getHistory sessionID
                    let texts = history |> Array.toList |> List.collect extractTexts
                    return texts |> List.tryPick tryParseJobMarker
                with _ -> return None
    }

let submitForKind (root: string) (todayStr: string) (entries: KnowledgeGraphEntry list) (kind: KnowledgeGraphJobKind)
                 : JS.Promise<string> =
    promise {
        let! result = withKnowledgeGraphPortLock 30000L 1000 root (fun () ->
            promise {
                match kind with
                | AppendAfterWork ->
                    do! appendEntries root todayStr entries
                    return $"Appended {entries.Length} knowledge graph entries."
                | DailyRewrite date ->
                    do! rewriteDay root date entries
                    return $"Rewrote knowledge graph day {date}."
            })
        match result with
        | Error e -> return e
        | Ok msg -> return msg
    }
