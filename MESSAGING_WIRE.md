# Messaging wire: intentional dual-host fork

Kernel `Message<'T>` is shared; host JSON shape is not. Do not merge wire codecs without a coordinated host protocol change.

## Opencode envelope

- Top-level: `{ info, parts }`.
- Metadata lives under **`info`**: `id`, `sessionID`, `role`, `agent`, `isError`, `toolName`, `details`, `time`, synthetic `model`.
- Tool parts: `type: "tool"`, `tool`, `callID`, `state` (`status` / `output` / `error` / `input`).
- Decode: `src/Opencode/MessagingCodec.fs` (`decodeMessage` reads `msg.info` + `msg.parts`).

## Mux flat message

- Top-level: `id`, `role`, `agent`, `parts` (no `info` wrapper).
- `sessionID` is supplied by the caller when decoding a message array.
- Tool parts: `type: "dynamic-tool"`, `toolName`, `toolCallId`, `state` (e.g. `output-available`), structured or string `output`.
- Role wire: `tool-result` (Mux) vs `toolResult` (Opencode `info.role`).
- Decode: `src/Mux/MessagingCodec.fs` (`decodeMessage sessionID`).

## Shared Shell (parts + encode)

- **`Shell.MessagingPartCodec`**: text parts, part arrays, Opencode tool-state box, Mux dynamic-tool state, tool output/error extraction.
- **`Shell.MessagingEncodeHelpers.replacePartsOnRawMessage`**: shallow `parts` replacement on an existing host object when typed projection mutates parts but identity should stay native.

Architecture gate **`dualHostMessagingCodecUsesEncodeHelpers`**: both `Opencode/MessagingCodec.fs` and `Mux/MessagingCodec.fs` open `MessagingEncodeHelpers`, call `replacePartsOnRawMessage`, and must not inline `Dyn.withKey rawMsg "parts"`.

## What stays forked

- `info` vs flat scalars, tool part type names (`tool` vs `dynamic-tool`), field names (`callID` vs `toolCallId`), role strings, error/details defaults, synthetic message construction.

Merging into one wire codec would fake equivalence and break replay/dedup contracts. Extend shared part logic in Shell; keep per-host `MessagingCodec` until hosts align.