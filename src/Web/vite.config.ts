import react from "@vitejs/plugin-react";
import { VitePWA } from "vite-plugin-pwa";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: "prompt",
      manifest: {
        name: "Freizeit-Cockpit",
        short_name: "Cockpit",
        description: "Gemeinsam christliche Freizeiten planen",
        theme_color: "#0f766e",
        background_color: "#f4fbfa",
        display: "standalone",
        lang: "de-DE",
        start_url: "/",
        icons: [
          { src: "/icons/icon-192.png", sizes: "192x192", type: "image/png" },
          { src: "/icons/icon-512.png", sizes: "512x512", type: "image/png" },
        ],
      },
      workbox: {
        navigateFallbackDenylist: [/^\/api\//, /^\/hilfe\//],
      },
    }),
  ],
  build: {
    outDir: "../FreizeitCockpit.Web/wwwroot",
    emptyOutDir: false,
  },
  server: {
    port: 5173,
    proxy: { "/api": "http://localhost:5080" },
  },
  test: {
    environment: "jsdom",
    environmentOptions: { jsdom: { url: "https://localhost/" } },
    setupFiles: "./src/test-setup.ts",
    coverage: {
      provider: "v8",
      reporter: ["text", "json-summary"],
      thresholds: { lines: 80, branches: 75 },
    },
  },
});
