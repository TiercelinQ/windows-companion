/**
 * Centralized IPC channel names. Convention: `entity:action`.
 * Zero hardcoded channel string anywhere else in the codebase.
 */
export const IPC = {
  PREF_GET: "pref:get",
  PREF_SET: "pref:set",
  UPDATE_START: "update:start",
  UPDATE_CHECK: "update:check",
  UPDATE_CANCEL: "update:cancel",
  UPDATE_DATA: "update:data",
  UPDATE_END: "update:end",
  HARDWARE_SCAN: "hardware:scan",
  HARDWARE_EXPORT: "hardware:export",
  SYSTEM_SCAN: "system:scan",
  SYSTEM_EXPORT: "system:export",
  NETWORK_SCAN: "network:scan",
  NETWORK_EXPORT: "network:export",
} as const;
