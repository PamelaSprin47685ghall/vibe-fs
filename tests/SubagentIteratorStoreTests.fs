module Wanxiangshu.Tests.SubagentIteratorStoreTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.SubagentIteratorStore

let testStoreAndConsume () =
    let store = createSubagentIteratorStore 3

    let item =
        { childID = "session-1"
          agent = "coder"
          host = Wanxiangshu.Kernel.HostTools.Opencode }

    let id = storeSubagentIterator store "global" item
    check "id prefix is correct" (id.StartsWith "sci_s:")

    let consumed = consumeSubagentIterator store id
    equal "consumed item is correct" (Some item) consumed

let run () = testStoreAndConsume ()
