namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts
open Wanxiangshu.Persistence.EventStore

module MagicTodoFactCodec =
    val payloadRefOfBlobRef: ref: BlobRef -> PayloadRef option
    val payloadRefOfBlobDigest: digest: BlobDigest -> PayloadRef option
    val payloadRefs: fact: MagicTodoFact -> PayloadRef list
    val encode: fact: MagicTodoFact -> string
    val tryDecode: json: string -> Result<MagicTodoFact, string>
