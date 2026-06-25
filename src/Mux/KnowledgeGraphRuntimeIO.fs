module VibeFs.Mux.KnowledgeGraphRuntimeIO

open Fable.Core
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Kernel.KnowledgeGraphRuntimeState
open VibeFs.Shell.KnowledgeGraphStorage
open VibeFs.Shell.KnowledgeGraphSubmit
open VibeFs.Shell.Dyn

let buildEntries = VibeFs.Shell.KnowledgeGraphSubmit.buildEntriesFromDrafts

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
    VibeFs.Shell.KnowledgeGraphSubmit.submitEntriesForKind defaultPortLockTimeoutMs defaultPortLockRetryDelayMs root todayStr entries kind
