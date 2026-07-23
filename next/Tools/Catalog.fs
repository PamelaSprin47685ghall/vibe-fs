namespace Wanxiangshu.Next.Tools

open Fable.Core.JsInterop

module ToolCatalog =

    let extractSessionCommandPort (contextObj: obj) (defaultPort: SessionCommandPort) : SessionCommandPort =
        if isNull contextObj then
            defaultPort
        else
            try
                let c = contextObj
                if not (isNull c?session) then unbox<SessionCommandPort> c?session
                elif not (isNull c?sessionPort) then unbox<SessionCommandPort> c?sessionPort
                elif not (isNull c?port) then unbox<SessionCommandPort> c?port
                else defaultPort
            with _ ->
                defaultPort

    let all : Tool list =
        [ StaticTools.todowriteTool ()
          StaticTools.executorTool ()
          FileTools.fileReadTool ()
          FileTools.fileWriteTool ()
          FileTools.fileEditTool ()
          ReviewTools.submitReviewTool ()
          ReviewTools.returnReviewerTool () ]
