import { createContext, useContext } from "react";
import type { ToastType } from "../../../shared/types";

export interface ToastOptions {
  type: ToastType;
  message: string;
  description?: string;
}
export interface ToastItem extends ToastOptions {
  id: number;
}
export type ToastFn = (options: ToastOptions) => void;

export const ToastContext = createContext<ToastFn>(() => {
  /* default no-op until a provider mounts */
});

/** Access the toast dispatch function from within the ToastManager provider. */
export function useToast(): ToastFn {
  return useContext(ToastContext);
}
