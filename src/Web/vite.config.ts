import react from "@vitejs/plugin-react";
import { VitePWA } from "vite-plugin-pwa";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: "prompt",
      manifest: {
        id: "/",
        name: "Freizeit-Cockpit",
        short_name: "Freizeit",
        description: "Gemeinsam christliche Freizeiten planen",
        theme_color: "#0f766e",
        background_color: "#f4fbfa",
        display: "standalone",
        lang: "de-DE",
        start_url: "/",
        scope: "/",
        categories: ["productivity", "lifestyle"],
        icons: [
          {
            src: "/icons/freizeit-cockpit.svg",
            sizes: "any",
            type: "image/svg+xml",
            purpose: "any",
          },
          {
            src: "/icons/freizeit-cockpit.svg",
            sizes: "any",
            type: "image/svg+xml",
            purpose: "maskable",
          },
        ],
      },
      workbox: {
        cleanupOutdatedCaches: true,
        navigateFallbackDenylist: [/^\/api\//, /^\/hilfe\//],
      },
    }),
  ],
  build: {
    outDir: "../FreizeitCockpit.Web/wwwroot",
    emptyOutDir: false,
    rolldownOptions: {
      output: {
        codeSplitting: {
          groups: [
            {
              name: "react-vendor",
              test: /node_modules[\\/](react|react-dom|react-router)/,
              priority: 20,
            },
            {
              name: "planning-vendor",
              test: /node_modules[\\/](@fullcalendar|luxon)/,
              priority: 15,
            },
            {
              name: "vendor",
              test: /node_modules/,
              maxSize: 350 * 1024,
              priority: 10,
            },
          ],
        },
      },
    },
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
