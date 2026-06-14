import { Component, type ErrorInfo, type ReactNode } from "react";
import { t } from "../i18n";

interface Props {
  children: ReactNode;
}
interface State {
  message: string | null;
}

/**
 * Renderer-level error boundary. React requires a class component for this.
 * On an unexpected render error, shows a persistent danger message.
 */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { message: null };

  static getDerivedStateFromError(error: Error): State {
    return { message: error.message };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    console.error("Renderer error:", error, info.componentStack);
  }

  render(): ReactNode {
    if (this.state.message !== null) {
      return (
        <div className="error-boundary" role="alert">
          <i className="fa-solid fa-circle-exclamation error-boundary-icon" aria-hidden="true" />
          <span className="error-boundary-title">{t("error.boundaryTitle")}</span>
          <span className="error-boundary-detail">{this.state.message}</span>
        </div>
      );
    }
    return this.props.children;
  }
}
