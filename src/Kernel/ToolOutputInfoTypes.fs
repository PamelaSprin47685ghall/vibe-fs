module Wanxiangshu.Kernel.ToolOutputInfoTypes

let noChangeStatus = "No Change Since Previous Read/Write"

type InfoItem =
    | Hint of string
    | Syntax of string
    | Iterator of string
    | Status of string
    | ExitCode of int

type ToolOutputMessage = { info: InfoItem list; body: string }
