import { defineConfig } from "vitepress";

export default defineConfig({
  lang: "de-DE",
  title: "Freizeit-Cockpit Hilfe",
  description: "Anwenderhilfe für das Freizeit-Cockpit",
  base: "/hilfe/",
  outDir: "../../FreizeitCockpit.Web/wwwroot/hilfe",
  cleanUrls: true,
  themeConfig: {
    nav: [{ text: "Zur Anwendung", link: "/" }],
    sidebar: [{ text: "Einstieg", items: [{ text: "Überblick", link: "/" }] }],
    search: { provider: "local" },
    footer: { message: "Freizeit-Cockpit · Hilfe auf Deutsch" },
  },
});
