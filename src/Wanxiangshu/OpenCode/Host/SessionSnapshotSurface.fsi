namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.HostSession

module SessionSnapshotSurface =
    type ProjectedMessages =
        internal new: messages: SessionMessage list -> ProjectedMessages
        member internal Messages: SessionMessage list

    val projectMessages: rawMessages: obj -> ProjectedMessages
    val locateToolCall: callId: string -> handle: ProjectedMessages -> obj
    val toolPartStateAt: handle: ProjectedMessages -> messageIndex: int -> partIndex: int -> obj
