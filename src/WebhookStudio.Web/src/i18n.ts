import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import en from "./locales/en-US";
import zh from "./locales/zh-CN";
export const resources = { "en-US": en, "zh-CN": zh } as const;
export const detectLanguage = () => {
  const saved = localStorage.getItem("language");
  if (saved === "zh-CN" || saved === "en-US") return saved;
  return navigator.language.toLowerCase().startsWith("zh") ? "zh-CN" : "en-US";
};
void i18n.use(initReactI18next).init({
  resources,
  lng: detectLanguage(),
  fallbackLng: "en-US",
  returnNull: false,
  interpolation: { escapeValue: false },
  saveMissing: import.meta.env.DEV,
  missingKeyHandler: (_lng, _ns, key) =>
    console.error(`Missing translation: ${key}`),
});
i18n.on("languageChanged", (language) => {
  document.documentElement.lang = language;
  localStorage.setItem("language", language);
});
document.documentElement.lang = i18n.language;
export default i18n;
