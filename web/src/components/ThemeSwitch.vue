<script setup lang="ts">
import { Moon, Sunny } from '@element-plus/icons-vue'
import { nextTick, ref } from 'vue'
import type { SwitchInstance } from 'element-plus'
import { useUiStore } from '@/stores/ui'

const uiStore = useUiStore()
const switchRef = ref<SwitchInstance>()

function beforeThemeChange() {
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches

  if (!('startViewTransition' in document) || prefersReducedMotion) {
    return true
  }

  return new Promise<boolean>((resolve) => {
    const switchElement = switchRef.value?.$el as HTMLElement | undefined
    const rect = switchElement?.getBoundingClientRect()
    const x = rect ? rect.left + rect.width / 2 : window.innerWidth - 44
    const y = rect ? rect.top + rect.height / 2 : 32
    const endRadius = Math.hypot(
      Math.max(x, window.innerWidth - x),
      Math.max(y, window.innerHeight - y),
    )
    const root = document.documentElement

    root.dataset.themeTransition = uiStore.isDark ? 'to-light' : 'to-dark'
    root.style.setProperty('--theme-transition-x', `${x}px`)
    root.style.setProperty('--theme-transition-y', `${y}px`)
    root.style.setProperty('--theme-transition-radius', `${endRadius}px`)

    const transition = document.startViewTransition(async () => {
      resolve(true)
      await nextTick()
    })

    transition.finished.finally(() => {
      delete root.dataset.themeTransition
      root.style.removeProperty('--theme-transition-x')
      root.style.removeProperty('--theme-transition-y')
      root.style.removeProperty('--theme-transition-radius')
    })
  })
}

function changeTheme(value: string | number | boolean) {
  uiStore.setTheme(Boolean(value))
}
</script>

<template>
  <div class="theme-switch">
    <el-switch
      ref="switchRef"
      :model-value="uiStore.isDark"
      :active-action-icon="Moon"
      :inactive-action-icon="Sunny"
      :before-change="beforeThemeChange"
      aria-label="切换明暗主题"
      @change="changeTheme"
    />
  </div>
</template>
