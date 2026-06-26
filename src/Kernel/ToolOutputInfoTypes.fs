module Wanxiangshu.Kernel.ToolOutputInfoTypes

[<RequireQualifiedAccess>]
type ToolOutputBodyRef =
    | SeeBelow
    | SeeBelowTruncated
    | NoChangeSincePreviousReadWrite

let seeBelow = "/See Below/"
let seeBelowTruncated = "/See Below, Truncated/"
let noChangeSincePreviousReadWrite = "/No Change Since Previous Read/Write/"

type InfoItem =
    | Hint of string
    | Syntax of string
    | Iterator of string
    | Status of string
    | ExitCode of int
    | Signal of string
    | TimeoutMs of int
    | BodyRef of ToolOutputBodyRef

type ToolOutputMessage = { info: InfoItem list; body: string }