module VibeFs.Shell.FileReadCache

let private store : VibeFs.Kernel.Lru.LruStore<string> ref = ref (VibeFs.Kernel.Lru.create 256)

let get (path: string) : string option =
    let next, value = VibeFs.Kernel.Lru.get store.Value path
    store.Value <- next
    value

let set (path: string) (content: string) : unit =
    store.Value <- VibeFs.Kernel.Lru.set store.Value path content

let invalidate (path: string) : unit =
    store.Value <- VibeFs.Kernel.Lru.delete store.Value path

let clear () : unit =
    store.Value <- VibeFs.Kernel.Lru.create 256
