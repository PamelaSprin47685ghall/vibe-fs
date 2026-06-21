module VibeFs.Mux.WikiTools

open System
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Wiki
open VibeFs.Kernel.WikiRuntimeState
open VibeFs.Kernel.ToolCatalog
open VibeFs.Mux.Wrappers
open VibeFs.Shell.WikiFiles
open VibeFs.Shell.WikiPortLock
open VibeFs.Shell.PromiseQueue

let private buildEntries (root: string) (drafts: WikiDraft list) : JS.Promise<WikiEntry list> =
    promise {
        let! files = readWikiFiles root
        let projection = projectLatestWins files
        let normalizedDrafts = normalizeDraftIds projection drafts
        let allocate (knownIds: Set<string>) : string =
            let random = System.Random()
            match Wiki.allocateRandomHexId (fun () -> random.Next(0, 65536)) knownIds with
            | Ok id -> id
            | Error message -> failwith message
        match applyDrafts allocate projection normalizedDrafts with
        | Ok entries -> return entries
        | Error error -> return raise (exn error)
    }

type MuxWikiRuntime() =
    let registeredJobs = System.Collections.Generic.Dictionary<string, WikiJobContext>()
    let writeQueue = SerialQueue()

    member _.FetchFromSessionSnapshot(_sessionID: string, directory: string, id: string) : JS.Promise<string> =
        promise {
            if System.String.IsNullOrWhiteSpace directory then
                return "No wiki directory provided."
            else
                let! projection = readProjection directory
                match fetchAnswer projection id with
                | Ok answer -> return answer
                | Error message -> return message
        }

    member _.RegisterJob(_sessionID: string, _ctx: WikiJobContext) : unit =
        registeredJobs.[_sessionID] <- _ctx

    member this.RegisterJobForTesting(sessionID: string, workspaceRoot: string, kindTag: string, payload: obj) : unit =
        if System.String.IsNullOrWhiteSpace workspaceRoot then
            failwith "Wiki job workspaceRoot must be a non-empty directory path."

        let payloadObj = payload

        let readRequiredField (fieldName: string) : string =
            let value = str payloadObj fieldName
            if value.Trim() = "" then failwith $"Wiki job payload missing required field '{fieldName}'"
            else value.Trim()

        let kind =
            let normalizedTag = kindTag.Trim().ToLowerInvariant()
            let builders =
                Map [
                    "append", fun () -> AppendAfterWork
                    "daily", fun () -> DailyRewrite(readRequiredField "date")
                    "weekly", fun () -> WeeklyRewrite(readRequiredField "through")
                ]
            match Map.tryFind normalizedTag builders with
            | Some build -> build ()
            | None -> failwith $"Unknown wiki job kind: {normalizedTag}"

        this.RegisterJob(sessionID, { workspaceRoot = workspaceRoot; kind = kind })

    member _.TakeJob(sessionID: string) : WikiJobContext option =
        match registeredJobs.TryGetValue sessionID with
        | true, ctx -> Some ctx
        | false, _ -> None

    member _.DeleteJob(sessionID: string) : unit =
        registeredJobs.Remove(sessionID) |> ignore

    member _.Submit(sessionID: string, _directory: string, drafts: WikiDraft list) : JS.Promise<string> =
        promise {
            match registeredJobs.TryGetValue sessionID with
            | false, _ -> return "No active wiki job for this session."
            | true, ctx ->
                let root = ctx.workspaceRoot
                let todayStr = System.DateTime.UtcNow.ToString("yyyy-MM-dd")
                return! writeQueue.Enqueue(fun () ->
                    promise {
                        let! entries = buildEntries root drafts
                        let kind = ctx.kind
                        return!
                            withWikiPortLock 30000L 1000 root (fun () ->
                                match kind with
                                | AppendAfterWork ->
                                    promise {
                                        do! appendEntries root todayStr entries
                                        registeredJobs.Remove(sessionID) |> ignore
                                        return $"Appended {entries.Length} wiki entries."
                                    }
                                | DailyRewrite date ->
                                    promise {
                                        do! rewriteDay root date entries
                                        registeredJobs.Remove(sessionID) |> ignore
                                        return $"Rewrote wiki day {date}."
                                    }
                                | WeeklyRewrite throughDate ->
                                    promise {
                                        do! rewriteSnapshot root throughDate entries
                                        do! deleteDayFilesThrough root throughDate
                                        registeredJobs.Remove(sessionID) |> ignore
                                        return $"Rewrote wiki snapshot through {throughDate}."
                                    })
                    })
        }

let private wikiDraftEntrySchema : obj =
    createObj
        [ "type", box "object"
          "properties",
          box
              (createObj
                  [ "id", box (createObj [ "type", box "string"; "description", box "Existing wiki entry id to update" ])
                    "q", box (createObj [ "type", box "string"; "description", box "Question" ])
                    "a", box (createObj [ "type", box "string"; "description", box "Answer" ]) ])
          "required", box [| "q"; "a" |]
          "additionalProperties", box false ]

let fetchWikiTool (wikiRuntime: MuxWikiRuntime) : ToolDefinition =
    { name = "fetch_wiki"
      description = description "fetch_wiki"
      parameters = mkSchema (createObj [ "id", box (strProp Params.fetchWikiId) ]) [| "id" |]
      execute =
          fun config args ->
              let sessionID = Dyn.str config "sessionID"
              let directory =
                  let current = Dyn.str config "directory"
                  if current = "" then defaultArg (strField config "cwd") "" else current
              wikiRuntime.FetchFromSessionSnapshot(sessionID, directory, Dyn.str args "id")
      condition = None }

let returnBookkeeperTool (wikiRuntime: MuxWikiRuntime) : ToolDefinition =
    { name = "return_bookkeeper"
      description = description "return_bookkeeper"
      parameters =
          mkSchema
              (createObj
                  [ "entries",
                    box
                        (createObj
                            [ "type", box "array"
                              "items", box wikiDraftEntrySchema
                              "description", box Params.submitWikiEntries ]) ])
              [| "entries" |]
      execute =
          fun config args ->
              let sessionID = Dyn.str config "sessionID"
              let directory =
                  let current = Dyn.str config "directory"
                  if current = "" then defaultArg (strField config "cwd") "" else current
              match parseDraftArray (Dyn.get args "entries") with
              | Error message -> resolveStr message
              | Ok drafts -> wikiRuntime.Submit(sessionID, directory, drafts)
      condition = None }
