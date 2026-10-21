// Binds omp's own session-file lookup to the reader `sdk-children.ts` needs.
//
// The binding lives in its own module because `sdk-children.ts` is imported by
// the offline fixture, which must run without an omp installation: a static
// import of the omp package there would pull it in at module load. This module
// is the one place that knows the omp package at runtime; it is imported by
// the host extension that wires the adapter into a real omp process.
import { readSessionInfo } from "@oh-my-pi/pi-coding-agent/session/session-listing";
import type { SessionFileReader } from "./sdk-children";

export const ompSessionFileReader: SessionFileReader = async (file) => {
  const info = await readSessionInfo(file);
  return info ? { id: info.id } : undefined;
};
