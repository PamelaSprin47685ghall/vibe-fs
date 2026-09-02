namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Dispatch.OpenCode

module SessionSnapshotPort =
    val projectMessage: raw: obj -> SessionMessage option
    val projectMessages: rawMessages: obj array -> SessionMessage list

    type SdkSnapshotPort =
        new: client: obj * workspaceDirectory: string option -> SdkSnapshotPort
        interface ISessionSnapshotPort

    type HttpSnapshotPort =
        new: baseUrl: string -> HttpSnapshotPort
        interface ISessionSnapshotPort

    val create: input: obj -> ISessionSnapshotPort option
