namespace Wanxiangshu.Repository.Knowledge.Casebook

/// JS-native provider fetch boundary. The Host schema, Casebook index, replay,
/// and Bookkeeper remain inside the owner; callers pass only plain Host values
/// and the opaque EventStore capability.
module CasebookFetchSurface =

    val contract: toolModule: obj -> workspaceRoot: string -> store: obj -> obj
