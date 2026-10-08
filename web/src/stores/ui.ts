import { defineStore } from 'pinia'
import { ref } from 'vue'

const storageKey = 'dcfs-dashboard-theme'

export const useUiStore = defineStore('ui', () => {
  const prefersDark = window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false
  const isDark = ref(localStorage.getItem(storageKey) === 'dark' || prefersDark)

  function applyTheme() {
    document.documentElement.classList.toggle('dark', isDark.value)
    document
      .querySelector('meta[name="theme-color"]')
      ?.setAttribute('content', isDark.value ? '#141414' : '#ffffff')
  }

  function toggleTheme() {
    isDark.value = !isDark.value
    localStorage.setItem(storageKey, isDark.value ? 'dark' : 'light')
    applyTheme()
  }

  applyTheme()

  return { isDark, toggleTheme }
})
