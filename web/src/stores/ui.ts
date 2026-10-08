import { defineStore } from 'pinia'
import { computed, onScopeDispose, ref } from 'vue'

const storageKey = 'whaledeck-theme'
type ThemePreference = 'system' | 'light' | 'dark'

export const useUiStore = defineStore('ui', () => {
  const colorScheme = window.matchMedia?.('(prefers-color-scheme: dark)')
  const savedPreference = localStorage.getItem(storageKey)
  const preference = ref<ThemePreference>(
    savedPreference === 'light' || savedPreference === 'dark' ? savedPreference : 'system',
  )
  const systemPrefersDark = ref(colorScheme?.matches ?? false)
  const isDark = computed(() =>
    preference.value === 'system' ? systemPrefersDark.value : preference.value === 'dark',
  )

  function applyTheme() {
    document.documentElement.classList.toggle('dark', isDark.value)
    document
      .querySelector('meta[name="theme-color"]')
      ?.setAttribute('content', isDark.value ? '#141414' : '#ffffff')
  }

  function setTheme(dark: boolean) {
    preference.value = dark ? 'dark' : 'light'
    localStorage.setItem(storageKey, preference.value)
    applyTheme()
  }

  function handleSystemThemeChange(event: MediaQueryListEvent) {
    systemPrefersDark.value = event.matches
    if (preference.value === 'system') applyTheme()
  }

  colorScheme?.addEventListener('change', handleSystemThemeChange)
  onScopeDispose(() => colorScheme?.removeEventListener('change', handleSystemThemeChange))

  applyTheme()

  return { isDark, preference, setTheme }
})
