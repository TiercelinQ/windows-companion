import { registerHardwareController } from "./hardware.controller";
import { registerNetworkController } from "./network.controller";
import { registerPreferencesController } from "./preferences.controller";
import { registerSystemController } from "./system.controller";
import { registerUpdateController } from "./update.controller";

/** Register every IPC controller. Called once from the main entry after app ready. */
export function registerAllControllers(): void {
  registerPreferencesController();
  registerUpdateController();
  registerHardwareController();
  registerSystemController();
  registerNetworkController();
}
