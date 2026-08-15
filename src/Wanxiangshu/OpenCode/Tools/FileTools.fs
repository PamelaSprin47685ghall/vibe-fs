namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Thoth.Json

module FileTools =

    let private decodeFilePathFromAuto (payload: string) =
        match Decode.Auto.fromString<string> payload with
        | Ok path -> path
        | Error _ -> payload

    let private decodeFilePathInner (payload: string) =
        match Decode.fromString (Decode.field "filePath" Decode.string) payload with
        | Ok path -> path
        | Error _ -> decodeFilePathFromAuto payload

    let private decodeFilePath (payload: string) =
        try
            decodeFilePathInner payload
        with _ ->
            payload

    let private decodeWritePayloadInner (payload: string) =
        let decoder =
            Decode.object (fun get ->
                let path = get.Required.Field "filePath" Decode.string
                let content = get.Required.Field "content" Decode.string
                (path, content))

        match Decode.fromString decoder payload with
        | Ok res -> Some res
        | Error _ -> None

    let private decodeWritePayload (payload: string) =
        try
            decodeWritePayloadInner payload
        with _ ->
            None

    let private decodeEditPayloadInner (payload: string) =
        let decoder =
            Decode.object (fun get ->
                let path = get.Required.Field "filePath" Decode.string
                let oldStr = get.Required.Field "oldString" Decode.string
                let newStr = get.Required.Field "newString" Decode.string
                (path, oldStr, newStr))

        match Decode.fromString decoder payload with
        | Ok res -> Some res
        | Error _ -> None

    let private decodeEditPayload (payload: string) =
        try
            decodeEditPayloadInner payload
        with _ ->
            None

    let private writeSize (filePath: string) (content: string) =
        let stat = NodeFs.statSync filePath

        if isNull stat || isNull stat?size then
            content.Length
        else
            unbox<int> stat?size

    let private replaceOrReportMissing (filePath: string) (content: string) (oldString: string) (newString: string) =
        if not (content.Contains oldString) then
            { Result = sprintf "oldString not found in file %s" filePath
              Truncated = false }
        else
            NodeFs.writeFileSync (filePath, content.Replace(oldString, newString), "utf8")

            { Result = sprintf "Edited %s" filePath
              Truncated = false }

    let private applyEdit (filePath: string) (oldString: string) (newString: string) =
        if not (NodeFs.existsSync filePath) then
            { Result = sprintf "File not found: %s" filePath
              Truncated = false }
        else
            replaceOrReportMissing filePath (NodeFs.readFileSync (filePath, "utf8")) oldString newString

    let fileReadTool () : Tool =
        { Name = "read"
          Description = "Read file content from filesystem."
          SchemaJson = """{"type":"object","properties":{"filePath":{"type":"string"}},"required":["filePath"]}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()
                    let filePath = decodeFilePath input.Payload

                    if not (NodeFs.existsSync filePath) then
                        return
                            { Result = sprintf "File not found: %s" filePath
                              Truncated = false }
                    else
                        let content = NodeFs.readFileSync (filePath, "utf8")
                        return { Result = content; Truncated = false }
                } }

    let fileWriteTool () : Tool =
        { Name = "write"
          Description = "Write file content to filesystem."
          SchemaJson =
            """{"type":"object","properties":{"filePath":{"type":"string"},"content":{"type":"string"}},"required":["filePath","content"]}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    match decodeWritePayload input.Payload with
                    | None ->
                        return
                            { Result = sprintf "Failed to parse JSON payload for write tool: %s" input.Payload
                              Truncated = false }
                    | Some(filePath, content) ->
                        NodeFs.writeFileSync (filePath, content, "utf8")

                        return
                            { Result = sprintf "Wrote %s (%d bytes)" filePath (writeSize filePath content)
                              Truncated = false }
                } }

    let fileEditTool () : Tool =
        { Name = "edit"
          Description = "Edit file content in filesystem using exact string replacement."
          SchemaJson =
            """{"type":"object","properties":{"filePath":{"type":"string"},"oldString":{"type":"string"},"newString":{"type":"string"}},"required":["filePath","oldString","newString"]}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    match decodeEditPayload input.Payload with
                    | None ->
                        return
                            { Result = sprintf "Invalid edit payload: %s" input.Payload
                              Truncated = false }
                    | Some(filePath, oldString, newString) ->
                        return applyEdit filePath oldString newString
                } }
