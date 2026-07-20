module Wanxiangshu.Runtime.EventStoreStateSync

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.EventStoreIo
open Wanxiangshu.Runtime.EventStoreStateInit
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventLogIo

type EventStoreState with
    member internal this.ProcessLines(lines: string[]) : bool =
        let mutable ok = true

        for line in lines do
            if ok && line.Trim() <> "" then
                match tryParseEventLine line with
                | Some e ->
                    this.Cache.FoldWan e
                    this.EventCountRead <- this.EventCountRead + 1
                | None -> ok <- false

        ok

    member internal this.SyncNewEvents() : JS.Promise<unit> =
        promise {
            let! stats = statAsync this.EventFilePath
            let size = this.SizeOf stats

            if size < this.LastKnownSize then
                do! this.RepairAndReplay()
            elif this.PartialLineBuffer <> "" then
                do! this.RepairAndReplay()
            elif size > this.LastReadByteOffset then
                let offset = this.LastReadByteOffset
                let length = int (size - offset)
                let! newText = readChunkAsync this.EventFilePath (float offset) length
                let combinedText = this.PartialLineBuffer + newText

                if not (combinedText.EndsWith("\n")) then
                    do! this.RepairAndReplay()
                else
                    let lines = combinedText.Split('\n')
                    let completeLines = lines.[0 .. lines.Length - 2]
                    let parseSuccess = this.ProcessLines completeLines

                    if not parseSuccess then
                        do! this.RepairAndReplay()
                    else
                        this.PartialLineBuffer <- ""
                        this.LastReadByteOffset <- size
                        this.LastKnownSize <- size
        }

    member internal this.EnsureSynced() : JS.Promise<unit> =
        promise {
            do! this.EnsureInitialized()

            try
                let! stats = statAsync this.EventFilePath
                let size = this.SizeOf stats

                if size <> this.LastKnownSize || this.PartialLineBuffer <> "" then
                    do! withWorkspaceLock this.EventFilePath (fun () -> this.SyncNewEvents())
            with ex ->
                if isMissingPathError (box ex) then
                    if this.WorkspaceRoot = "" then
                        this.ClearForMissingFile()
                    else
                        let! rootExists = fileExists this.WorkspaceRoot

                        if rootExists then
                            let! rootStats = statAsync this.WorkspaceRoot

                            if unbox<bool> (rootStats?isDirectory ()) then
                                this.ClearForMissingFile()
                            else
                                raise (exn "ENOTDIR: workspace root is not a directory")
                        else
                            this.ClearForMissingFile()
                else
                    raise ex
        }
