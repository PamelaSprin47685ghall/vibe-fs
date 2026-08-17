/**
 * provider-wire.js — OpenAI HTTP wire body → the registered provider projection surface.
 *
 * OpenAI bodies and the owner surface use different JS-native wire shapes. This
 * adapter owns only that format translation; projection, semantic reduction,
 * rendering, and prefix decisions remain the registered owner operations.
 */

import { createHash } from 'node:crypto';
import { canonicalJson } from '../../../../../dist/OpenCode/Codec/CanonicalJsonSurface.js';
import * as ProjectionSurface from '../../../../../dist/Participant/Provider/Projection/Surface.js';

const sha256Hex = (value) => createHash('sha256').update(String(value)).digest('hex');

const wirePart = (kind, payload) => ({ kind, ...payload });

const mediaPart = (part) => {
  const url = part?.image_url?.url ?? part?.url ?? null;
  if (typeof url !== 'string' || url === '') return null;
  const mediaType = part?.image_url?.detail ?? part?.mime ?? part?.mediaType ?? null;
  return wirePart('media', { mediaType, contentDigest: sha256Hex(url) });
};

/** `content` is either text or typed chunks from the OpenAI body. */
const contentParts = (content) => {
  if (typeof content === 'string') {
    return content === '' ? [] : [wirePart('text', { text: content })];
  }
  if (!Array.isArray(content)) return [];

  return content
    .map((chunk) => {
      if (typeof chunk === 'string') return chunk === '' ? null : wirePart('text', { text: chunk });
      switch (chunk?.type) {
        case 'text':
          return typeof chunk.text === 'string' && chunk.text !== ''
            ? wirePart('text', { text: chunk.text })
            : null;
        case 'reasoning':
        case 'thinking':
          return typeof chunk.text === 'string' && chunk.text !== ''
            ? wirePart('reasoning', { text: chunk.text })
            : null;
        case 'image_url':
        case 'image':
        case 'file':
          return mediaPart(chunk);
        default:
          return null;
      }
    })
    .filter((part) => part !== null);
};

const canonicalArguments = (raw) => {
  if (typeof raw !== 'string') return canonicalJson(raw ?? {});
  try {
    return canonicalJson(JSON.parse(raw));
  } catch {
    return raw;
  }
};

const toolCallParts = (message) =>
  (Array.isArray(message?.tool_calls) ? message.tool_calls : [])
    .map((call) => {
      const id = call?.id;
      const name = call?.function?.name ?? call?.name;
      if (typeof id !== 'string' || typeof name !== 'string') return null;
      return wirePart('tool-call', {
        callId: id,
        name,
        args: canonicalArguments(call?.function?.arguments),
      });
    })
    .filter((part) => part !== null);

const decodeMessage = (message) => {
  const role = typeof message?.role === 'string' ? message.role.toLowerCase() : '';
  if (role === 'tool') {
    const id = message?.tool_call_id;
    if (typeof id !== 'string') return null;
    const result = typeof message?.content === 'string'
      ? message.content
      : canonicalJson(message?.content ?? null);
    return { role, parts: [wirePart('tool-result', { callId: id, result })] };
  }

  const parts = [...contentParts(message?.content), ...toolCallParts(message)];
  if (role === '' && parts.length === 0) return null;
  return { role, parts };
};

/** Decode one OpenAI body into the owner surface's JS-native wire shape. */
export function wireOf(body) {
  const messages = Array.isArray(body?.messages) ? body.messages : [];
  const tools = (Array.isArray(body?.tools) ? body.tools : [])
    .map((tool) => tool?.function?.name ?? tool?.name)
    .filter((name) => typeof name === 'string');

  return {
    providerId: null,
    modelId: typeof body?.model === 'string' ? body.model : null,
    variant: null,
    tools,
    system: [],
    messages: messages.map(decodeMessage).filter((message) => message !== null),
  };
}

const withMetadata = (wire, semantic) => ({
  ...semantic,
  providerId: wire.providerId,
  modelId: wire.modelId,
  variant: wire.variant,
});

/** Apply the production semantic projection to decoded JS-native messages. */
export const semanticOf = (body) => {
  const wire = wireOf(body);
  return withMetadata(wire, ProjectionSurface.semanticProjection(wire.messages));
};

/** The semantic rendering is the stable fixture key. */
export const fixtureKeyOf = (body) => ProjectionSurface.renderSemantic(semanticOf(body));

const partText = (part) => {
  switch (part?.kind) {
    case 'text':
      return part.text ?? '';
    case 'reasoning':
      return `\u001freasoning\u001f${part.text ?? ''}`;
    case 'tool-call':
      return `\u001ftool-call\u001f${part.callId ?? ''}\u001f${part.name ?? ''}\u001f${part.args ?? ''}`;
    case 'tool-result':
      return `\u001ftool-result\u001f${part.callId ?? ''}\u001f${part.result ?? ''}`;
    case 'media':
      return `\u001fmedia\u001f${part.mediaType ?? ''}\u001f${part.contentDigest ?? ''}`;
    default:
      throw new Error(`unknown semantic part kind '${part?.kind ?? ''}'`);
  }
};

/** One message's semantic content, prefix-comparable and role-independent. */
export const messageText = (message) => (message?.parts ?? []).map(partText).join('\u001e');

/** Owner rendering and equality operations, kept as named adapter exports. */
export const renderWire = (projection) => {
  const wire = projection ?? {};
  const ownerWire = JSON.parse(ProjectionSurface.renderWire(wire.messages ?? []));
  return JSON.stringify({
    ...ownerWire,
    provider: wire.providerId ?? null,
    model: wire.modelId ?? null,
    variant: wire.variant ?? null,
    tools: wire.tools ?? [],
    system: wire.system ?? [],
  });
};
export const renderSemantic = (projection) => ProjectionSurface.renderSemantic(projection);
export const semanticallyEqual = (left, right) => ProjectionSurface.semanticallyEqual(left, right);
export const isAppendOnlyPrefix = (previous, next) => ProjectionSurface.isAppendOnlyPrefix(previous, next);
export const toSemantic = (wire) => withMetadata(wire, ProjectionSurface.semanticProjection(wire.messages ?? []));

/** ARCH-004: a first request establishes the seal; later requests must append. */
export const sealHolds = (previousWire, nextBody) =>
  previousWire === null || previousWire === undefined ? true : isAppendOnlyPrefix(previousWire, wireOf(nextBody));

const systemHead = (wire) => {
  const messages = wire?.messages ?? [];
  if (messages.length === 0 || messages[0].role !== 'system') return null;
  const parts = messages[0].parts ?? [];
  if (parts.length !== 1 || parts[0].kind !== 'text') return null;
  return { text: parts[0].text ?? '', tail: messages.slice(1) };
};

const normalizeModelIdentity = (text, modelId) =>
  typeof modelId === 'string' && modelId.length > 0
    ? text.split(modelId).join('<execution-model>')
    : text;

/** Admit only the measured fast→deep model substitution plus append-only growth. */
export function assistanceBindingPrefixHolds(previousWire, nextBody) {
  const nextWire = wireOf(nextBody);
  if (previousWire.modelId === nextWire.modelId) return false;

  const previousHead = systemHead(previousWire);
  const nextHead = systemHead(nextWire);
  if (previousHead === null || nextHead === null) return false;

  if (
    !previousHead.text.includes(previousWire.modelId)
    || !nextHead.text.includes(nextWire.modelId)
    || normalizeModelIdentity(previousHead.text, previousWire.modelId)
      !== normalizeModelIdentity(nextHead.text, nextWire.modelId)
  ) return false;

  const previousTail = {
    ...previousWire,
    modelId: nextWire.modelId,
    messages: previousHead.tail,
  };
  const nextTail = {
    ...nextWire,
    messages: nextHead.tail,
  };

  return isAppendOnlyPrefix(previousTail, nextTail);
}
