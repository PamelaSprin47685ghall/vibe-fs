/**
 * event-shape.js — Pure helpers for mapping OpenCode SSE payloads
 * to the EventProbe's internal event shape.
 *
 * Side-effect free: no I/O, no shared state. Extracted from
 * event-probe.js to keep that file under the 200-line Kolmogorov
 * line budget.
 */

function pickSessionID(parsed) {
  return parsed?.properties?.sessionID;
}

function pickMessageID(parsed) {
  return parsed?.properties?.messageID;
}

function pickPartID(parsed) {
  return parsed?.properties?.partID;
}

function pickToolCallID(parsed) {
  return parsed?.properties?.toolCallID;
}

function pickError(parsed) {
  return parsed?.properties?.error;
}

function pickFinishReason(parsed) {
  return parsed?.properties?.finishReason
    || parsed?.properties?.info?.finish
    || parsed?.properties?.info?.finishReason
    || parsed?.properties?.part?.reason;
}

function pickStatus(parsed) {
  return parsed?.properties?.status;
}

/**
 * Map a parsed SSE JSON payload to the internal eventObj shape used by
 * EventProbe. If `parsed.type` is missing, mark type as 'unknown' and stash
 * the raw payload under properties.
 */
export function shapeFromParsed(parsed) {
  if (parsed?.type) {
    return {
      type: parsed.type,
      properties: parsed.properties || {},
      sessionID: pickSessionID(parsed),
      messageID: pickMessageID(parsed),
      partID: pickPartID(parsed),
      toolCallID: pickToolCallID(parsed),
      error: pickError(parsed),
      finishReason: pickFinishReason(parsed),
      status: pickStatus(parsed),
    };
  }
  return {
    type: 'unknown',
    properties: parsed,
  };
}

/**
 * Build a parse-error event object for an unparseable data: line.
 */
export function shapeFromParseError(jsonStr, errorMessage) {
  return {
    type: 'parse-error',
    raw: jsonStr,
    error: errorMessage,
  };
}

/**
 * Build a probe-read-error event for a thrown read-loop exception.
 */
export function shapeFromReadError(message) {
  return {
    type: 'probe-read-error',
    error: message,
  };
}
