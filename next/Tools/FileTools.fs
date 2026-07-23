namespace Wanxiangshu.Next.Tools

open System.Threading.Tasks
open Thoth.Json

module FileTools =

    let fileReadTool () : Tool =
        { Name = "read"
          Description = "Read file content from filesystem."
          SchemaJson = """{"type":"object","properties":{"filePath":{"type":"string"}},"required":["filePath"]}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    let filePath =
                        try
                            let decoder = Decode.field "filePath" Decode.string

                            match Decode.fromString decoder input.Payload with
                            | Ok p -> p
                            | Error _ ->
                                match Decode.Auto.fromString<string> input.Payload with
                                | Ok p -> p
                                | Error _ -> input.Payload
                        with _ ->
                            input.Payload

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

                    let parsedOpt =
                        try
                            let decoder =
                                Decode.object (fun get ->
                                    let path = get.Required.Field "filePath" Decode.string
                                    let c = get.Required.Field "content" Decode.string
                                    (path, c))

                            match Decode.fromString decoder input.Payload with
                            | Ok res -> Some res
                            | Error _ -> None
                        with _ ->
                            None

                    match parsedOpt with
                    | None ->
                        return
                            { Result = sprintf "Failed to parse JSON payload for write tool: %s" input.Payload
                              Truncated = false }
                    | Some(filePath, content) ->
                        NodeFs.writeFileSync (filePath, content, "utf8")
                        let stat = NodeFs.statSync filePath

                        let size =
                            if isNull stat || isNull stat?size then
                                content.Length
                            else
                                unbox<int> stat?size

                        return
                            { Result = sprintf "Wrote %s (%d bytes)" filePath size
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

                    let parsedOpt =
                        try
                            let decoder =
                                Decode.object (fun get ->
                                    let path = get.Required.Field "filePath" Decode.string
                                    let oldStr = get.Required.Field "oldString" Decode.string
                                    let newStr = get.Required.Field "newString" Decode.string
                                    (path, oldStr, newStr))

                            match Decode.fromString decoder input.Payload with
                            | Ok res -> Some res
                            | Error _ -> None
                        with _ ->
                            None

                    match parsedOpt with
                    | None ->
                        return
                            { Result = sprintf "Invalid edit payload: %s" input.Payload
                              Truncated = false }
                    | Some(filePath, oldString, newString) ->
                        if not (NodeFs.existsSync filePath) then
                            return
                                { Result = sprintf "File not found: %s" filePath
                                  Truncated = false }
                        else
                            let content = NodeFs.readFileSync (filePath, "utf8")

                            if not (content.Contains oldString) then
                                return
                                    { Result = sprintf "oldString not found in file %s" filePath
                                      Truncated = false }
                            else
                                NodeFs.writeFileSync (filePath, content.Replace(oldString, newString), "utf8")

                                return
                                    { Result = sprintf "Edited %s" filePath
                                      Truncated = false }
                } }
