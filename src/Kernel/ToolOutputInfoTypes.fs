module Wanxiangshu.Kernel.ToolOutputInfoTypes

let noChangeStatus = "No Change Since Previous Read/Write"

type ToolOutputMessage =
    { body: string option
      hint: string option
      syntax: string option
      iterator: string option
      status: string option
      exitCode: int option }
