import { ssrRenderAttrs } from "vue/server-renderer";
import { useSSRContext } from "vue";
import { _ as _export_sfc } from "./plugin-vue_export-helper.1tPrXgE0.js";
const __pageData = JSON.parse('{"title":"Freizeit-Cockpit","description":"","frontmatter":{},"headers":[],"relativePath":"index.md","filePath":"index.md"}');
const _sfc_main = { name: "index.md" };
function _sfc_ssrRender(_ctx, _push, _parent, _attrs, $props, $setup, $data, $options) {
  _push(`<div${ssrRenderAttrs(_attrs)}><h1 id="freizeit-cockpit" tabindex="-1">Freizeit-Cockpit <a class="header-anchor" href="#freizeit-cockpit" aria-label="Permalink to &quot;Freizeit-Cockpit&quot;">​</a></h1><p>Das Freizeit-Cockpit unterstützt Teams dabei, eine christliche Freizeit gemeinsam vorzubereiten und vor Ort übersichtlich zu begleiten.</p><h2 id="orientierung" tabindex="-1">Orientierung <a class="header-anchor" href="#orientierung" aria-label="Permalink to &quot;Orientierung&quot;">​</a></h2><p>Nach der Anmeldung zeigt die Übersicht den heutigen Tagesplan, eigene Verantwortungen, offenen Beschaffungsbedarf und die jüngsten Aktivitäten. Auf kleinen Bildschirmen findest du die wichtigsten Bereiche in der oberen Navigationsleiste; am Desktop steht links die vollständige Navigation.</p><p>Alle Änderungen benötigen eine Internetverbindung. Die ausdrücklich synchronisierten Tages-, Speise-, Material- und Einkaufspläne bleiben offline als deutlich gekennzeichneter, schreibgeschützter Stand verfügbar.</p></div>`);
}
const _sfc_setup = _sfc_main.setup;
_sfc_main.setup = (props, ctx) => {
  const ssrContext = useSSRContext();
  (ssrContext.modules || (ssrContext.modules = /* @__PURE__ */ new Set())).add("index.md");
  return _sfc_setup ? _sfc_setup(props, ctx) : void 0;
};
const index = /* @__PURE__ */ _export_sfc(_sfc_main, [["ssrRender", _sfc_ssrRender]]);
export {
  __pageData,
  index as default
};
