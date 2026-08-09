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
    sidebar: [
      {
        text: "Einstieg",
        items: [
          { text: "Überblick", link: "/" },
          { text: "Anmeldung", link: "/anmeldung" },
          { text: "Sitzungen", link: "/konto-sitzungen" },
          { text: "Konto verwalten", link: "/konto-verwalten" },
          {
            text: "Organisationen, Camps und Rollen",
            link: "/organisationen-camps-rollen",
          },
        ],
      },
      {
        text: "Planung",
        items: [
          {
            text: "Tages- und Wochenplan",
            link: "/tagesplanung",
          },
          {
            text: "Suche, Aktivität, Druck und CSV",
            link: "/suche-aktivitaet-export",
          },
        ],
      },
    ],
    search: { provider: "local" },
    footer: { message: "Freizeit-Cockpit · Hilfe auf Deutsch" },
  },
});
