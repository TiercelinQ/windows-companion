import { app, BrowserWindow, nativeTheme, session } from "electron";
import { join } from "node:path";
import * as config from "../shared/config";
import { registerAllControllers } from "./controllers";
import {
  getPreferences,
  preferencesFileExists,
  setPreference,
} from "./models/preferences.model";

let mainWindow: BrowserWindow | null = null;

function createWindow(): void {
  const prefs = getPreferences();
  mainWindow = new BrowserWindow({
    width: prefs.windowWidth,
    height: prefs.windowHeight,
    x: prefs.windowX,
    y: prefs.windowY,
    minWidth: config.WINDOW_MIN_WIDTH,
    minHeight: config.WINDOW_MIN_HEIGHT,
    show: false,
    autoHideMenuBar: true,
    backgroundColor: prefs.theme === "dark" ? "#111827" : "#FFFFFF",
    icon: join(__dirname, "../../app_icon/app_icon.ico"),
    webPreferences: {
      preload: join(__dirname, "../preload/index.js"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      webSecurity: true,
    },
  });

  mainWindow.once("ready-to-show", () => mainWindow?.show());

  mainWindow.on("close", () => {
    if (!mainWindow) return;
    const b = mainWindow.getBounds();
    setPreference("windowWidth", b.width);
    setPreference("windowHeight", b.height);
    setPreference("windowX", b.x);
    setPreference("windowY", b.y);
  });

  mainWindow.on("closed", () => {
    mainWindow = null;
  });

  const rendererUrl = process.env["ELECTRON_RENDERER_URL"];
  if (!app.isPackaged && rendererUrl) {
    void mainWindow.loadURL(rendererUrl);
  } else {
    void mainWindow.loadFile(join(__dirname, "../renderer/index.html"));
  }
}

// Block unexpected navigation and new windows everywhere.
app.on("web-contents-created", (_e, contents) => {
  contents.on("will-navigate", (e) => e.preventDefault());
  contents.setWindowOpenHandler(() => ({ action: "deny" }));
});

// The app does not crash silently on an unexpected main error.
process.on("uncaughtException", (err) => {
  console.error("Uncaught exception (main):", err);
});

if (!app.requestSingleInstanceLock()) {
  app.quit();
} else {
  app.on("second-instance", () => {
    if (mainWindow) {
      if (mainWindow.isMinimized()) mainWindow.restore();
      mainWindow.focus();
    }
  });

  app.whenReady().then(() => {
    // First run: adopt the OS theme as the initial preference.
    if (!preferencesFileExists()) {
      setPreference("theme", nativeTheme.shouldUseDarkColors ? "dark" : "light");
    }

    session.defaultSession.setPermissionRequestHandler((_wc, _permission, callback) =>
      callback(false),
    );

    registerAllControllers();
    createWindow();

    app.on("activate", () => {
      if (BrowserWindow.getAllWindows().length === 0) createWindow();
    });
  });

  app.on("window-all-closed", () => {
    if (process.platform !== "darwin") app.quit();
  });
}
